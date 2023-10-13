using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace NAudio.Flac
{
    public sealed class FlacFrame
    {
        private List<FlacSubFrameData> _data;
        private Stream _stream;
        private FlacMetadataStreamInfo _streamInfo;
        private FlacBitReader _reader;

        private GCHandle _handle1, _handle2;
        private int[] _destBuffer;
        private int[] _residualBuffer;
        private byte[] _buffer;

        public FlacFrameHeader Header { get; private set; }

        public ushort Crc16 { get; private set; }

        public bool HasError { get; private set; }

        public FlacFrame(Stream stream, FlacMetadataStreamInfo streamInfo)
        {
            if (stream == null) throw new ArgumentNullException("stream");
            if (stream.CanRead == false) throw new ArgumentException("Stream is not readable");

            _stream = stream;
            _streamInfo = streamInfo;
        }

        public bool NextFrame()
        {
            Decode();
            return !HasError;
        }

		private unsafe void Decode()
		{
			if (_buffer == null || _buffer.Length < _streamInfo.MaxFrameSize)
				_buffer = new byte[_streamInfo.MaxFrameSize];

			long frameStartPosition = _stream.Position;
			int read = _stream.Read(_buffer, 0, (int)Math.Min(_buffer.Length, _stream.Length - _stream.Position));
			_stream.Position = frameStartPosition;

			fixed (byte* ptrBuffer = _buffer)
			using (_reader = new FlacBitReader(ptrBuffer, 0))
			{
				try
				{
					Header = new FlacFrameHeader(ptrBuffer, _streamInfo, true);
					HasError = Header.HasError;
					if (!HasError)
					{
						_reader.SeekBytes(Header.Length);

						if (ReadSubFrames() != _streamInfo.Channels)
						{
							HasError = true;
							_stream.Position = frameStartPosition;
							return;
						}

						var crc = CRC16.Instance.CalcCheckSum(ptrBuffer, _reader.Position);
						if (crc != 0) //data + crc = 0
						{
							Debug.WriteLine($"Wrong frame CRC16: {crc} {Crc16}");
							HasError = true;
							_stream.Position = frameStartPosition;
							return;
						}

						_stream.Position = (frameStartPosition + _reader.Position) >= _stream.Length ? _stream.Length : (frameStartPosition + _reader.Position);

						SamplesToBytes(_data);
					}
				}
				catch
				{
					_stream.Position = frameStartPosition;
					//HasError = true;
					throw;
				}
				finally
				{
					FreeBuffers();
				}
			}
			_reader = null;
		}

        private unsafe int ReadSubFrames()
        {
            List<FlacSubFrameBase> subFrames = new List<FlacSubFrameBase>();

            //alocateOutput
            _data = AllocOuputMemory();

            for (int c = 0; c < Header.Channels; c++)
            {
                int bps = Header.BitsPerSample;
                if (bps == 32 && Header.ChannelAssignment != FlacChannelAssignment.Independent)
                    throw new FlacException("Only Independent channels must be in 32 bit!", FlacLayer.Frame);

                if (Header.ChannelAssignment == FlacChannelAssignment.MidSide || Header.ChannelAssignment == FlacChannelAssignment.LeftSide)
                    bps += c;
                else if (Header.ChannelAssignment == FlacChannelAssignment.RightSide)
                    bps += 1 - c;

                var subframe = FlacSubFrameBase.GetSubFrame(_reader, _data[c], Header, bps);

                if (subframe == null)
                    continue;

                subFrames.Add(subframe);
            }

            _reader.Flush();
            Crc16 = _reader.ReadUInt16();

            return subFrames.Count;
        }

        private unsafe void SamplesToBytes(List<FlacSubFrameData> data)
        {
            if (Header.ChannelAssignment == FlacChannelAssignment.LeftSide)
            {
                for (int i = 0; i < Header.BlockSize; i++)
                {
                    data[1].DestBuffer[i] = data[0].DestBuffer[i] - data[1].DestBuffer[i];
                }
            }
            else if (Header.ChannelAssignment == FlacChannelAssignment.RightSide)
            {
                for (int i = 0; i < Header.BlockSize; i++)
                {
                    data[0].DestBuffer[i] += data[1].DestBuffer[i];
                }
            }
            else if (Header.ChannelAssignment == FlacChannelAssignment.MidSide)
            {
                for (int i = 0; i < Header.BlockSize; i++)
                {
                    int mid = data[0].DestBuffer[i] << 1;
                    int side = data[1].DestBuffer[i];

                    mid |= (side & 1);

                    data[0].DestBuffer[i] = (mid + side) >> 1;
                    data[1].DestBuffer[i] = (mid - side) >> 1;
                }
            }
        }

        public unsafe int GetBuffer(ref byte[] buffer)
        {
            if (HasError) return 0;

            int desiredsize = Header.BlockSize * Header.Channels * ((Header.BitsPerSample + 7) / 8); // align to bytes
            if (buffer == null || buffer.Length < desiredsize)
                buffer = new byte[desiredsize];

            fixed (byte* ptrBuffer = buffer)
            {
                if (Header.BitsPerSample < 8) { };
                if (Header.BitsPerSample == 8)
                {
                    byte* ptr = ptrBuffer;
                    for (int i = 0; i < Header.BlockSize; i++)
                    {
                        for (int c = 0; c < Header.Channels; c++)
                        {
                            *(ptr++) = (byte)(_data[c].DestBuffer[i] + 0x80);
                        }
                    }
                    return (int)(ptr - ptrBuffer);
                }
                else if (Header.BitsPerSample <= 16)
                {
                    var shift = 16 - Header.BitsPerSample;
                    short* ptr = (short*)ptrBuffer;
                    for (int i = 0; i < Header.BlockSize; i++)
                    {
                        for (int c = 0; c < Header.Channels; c++)
                        {
                            int val = _data[c].DestBuffer[i];
                            val <<= shift;
                            *(ptr++) = (short)val;
                        }
                    }
                    return (int)((byte*)ptr - ptrBuffer);
                }
                else if (Header.BitsPerSample <= 24)
                {
                    var shift = 24 - Header.BitsPerSample;
                    byte* ptr = ptrBuffer;
                    for (int i = 0; i < Header.BlockSize; i++)
                    {
                        for (int c = 0; c < Header.Channels; c++)
                        {
                            int val = _data[c].DestBuffer[i];
                            val <<= shift;
                            *(ptr++) = (byte)(val & 0xFF);
                            *(ptr++) = (byte)((val >> 8) & 0xFF);
                            *(ptr++) = (byte)((val >> 16) & 0xFF);
                        }
                    }
                    return (int)(ptr - ptrBuffer);
                }
                else if (Header.BitsPerSample <= 32)
                {
                    var shift = 32 - Header.BitsPerSample;
                    int* ptr = (int*)ptrBuffer;
                    for (int i = 0; i < Header.BlockSize; i++)
                    {
                        for (int c = 0; c < Header.Channels; c++)
                        {
                            int val = _data[c].DestBuffer[i];
                            val <<= shift;
                            *(ptr++) = val;
                        }
                    }
                    return (int)((byte*)ptr - ptrBuffer);
                }
                string error = "FlacFrame::GetBuffer: Invalid Flac-BitsPerSample: " + Header.BitsPerSample + ".";
                Debug.WriteLine(error);
                throw new FlacException(error, FlacLayer.Frame);
            }
        }

        private unsafe List<FlacSubFrameData> AllocOuputMemory()
        {
            if (_destBuffer == null || _destBuffer.Length < (Header.Channels * Header.BlockSize))
                _destBuffer = new int[Header.Channels * Header.BlockSize];
            if (_residualBuffer == null || _residualBuffer.Length < (Header.Channels * Header.BlockSize))
                _residualBuffer = new int[Header.Channels * Header.BlockSize];

            List<FlacSubFrameData> output = new List<FlacSubFrameData>();

            for (int c = 0; c < Header.Channels; c++)
            {
                fixed (int* ptrDestBuffer = _destBuffer, ptrResidualBuffer = _residualBuffer)
                {
                    FreeBuffers();
                    _handle1 = GCHandle.Alloc(_destBuffer, GCHandleType.Pinned);
                    _handle2 = GCHandle.Alloc(_residualBuffer, GCHandleType.Pinned);

                    FlacSubFrameData data = new FlacSubFrameData
                    {
                        DestBuffer = (ptrDestBuffer + c * Header.BlockSize),
                        ResidualBuffer = (ptrResidualBuffer + c * Header.BlockSize)
                    };
                    output.Add(data);
                }
            }

            return output;
        }

        public void FreeBuffers()
        {
            if (_handle1.IsAllocated)
                _handle1.Free();
            if (_handle2.IsAllocated)
                _handle2.Free();
        }

        ~FlacFrame()
        {
            FreeBuffers();
        }
    }
}