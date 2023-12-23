using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace NAudio.Flac
{
    public sealed partial class FlacFrame
    {
        private FlacSubFrameData[] _data;
        private Stream _stream;
        private FlacMetadataStreamInfo _streamInfo;
        private FlacBitReader _reader;

        private GCHandle _handle1, _handle2, _handle3;
        private int[] _destBuffer;
        private long[] _destBufferLong;
        private bool _isLong;
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

            _isLong = Header.BitsPerSample > 24 && Header.ChannelAssignment != FlacChannelAssignment.Independent;

            //alocateOutput
            _data = AllocOuputMemory();

            for (int c = 0; c < Header.Channels; c++)
            {
                int bps = Header.BitsPerSample;

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

        private unsafe void SamplesToBytes(FlacSubFrameData[] data)
        {
            if (Header.ChannelAssignment == FlacChannelAssignment.Independent && _isLong)
            {
                // optimize stereo
                if (data.Length == 2)
                    for (int i = 0; i < Header.BlockSize; i++)
                    {
                        data[0].DestBuffer[i] = (int)data[0].DestBufferLong[i];
                        data[1].DestBuffer[i] = (int)data[1].DestBufferLong[i];
                    }
                else
                    for (int c = 0; c < data.Length; c++)
                        for (int i = 0; i < Header.BlockSize; i++)
                            data[c].DestBuffer[i] = (int)data[c].DestBufferLong[i];
            }
            if (Header.ChannelAssignment == FlacChannelAssignment.LeftSide)
            {
                if (_isLong)
                    for (int i = 0; i < Header.BlockSize; i++)
                    {
                        data[0].DestBuffer[i] = (int)data[0].DestBufferLong[i];
                        data[1].DestBuffer[i] = (int)(data[0].DestBufferLong[i] - data[1].DestBufferLong[i]);
                    }
                else
                    for (int i = 0; i < Header.BlockSize; i++)
                        data[1].DestBuffer[i] = data[0].DestBuffer[i] - data[1].DestBuffer[i];
            }
            else if (Header.ChannelAssignment == FlacChannelAssignment.RightSide)
            {
                if (_isLong)
                    for (int i = 0; i < Header.BlockSize; i++)
                    {
                        data[0].DestBuffer[i] = (int)(data[0].DestBufferLong[i] + data[1].DestBufferLong[i]);
                        data[1].DestBuffer[i] = (int)data[1].DestBufferLong[i];
                    }
                else
                    for (int i = 0; i < Header.BlockSize; i++)
                        data[0].DestBuffer[i] += data[1].DestBuffer[i];
            }
            else if (Header.ChannelAssignment == FlacChannelAssignment.MidSide)
            {
                if (_isLong)
                    for (int i = 0; i < Header.BlockSize; i++)
                    {
                        long mid = data[0].DestBufferLong[i] << 1;
                        long side = data[1].DestBufferLong[i];

                        mid |= (side & 1);

                        data[0].DestBuffer[i] = (int)((mid + side) >> 1);
                        data[1].DestBuffer[i] = (int)((mid - side) >> 1);
                    }
                else
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

        private unsafe FlacSubFrameData[] AllocOuputMemory()
        {
            FreeBuffers();

            var channels = Header.Channels;
            var length = channels * Header.BlockSize;

            if (_destBuffer == null || _destBuffer.Length < length)
                _destBuffer = new int[length];
            if (_isLong)
                if (_destBufferLong == null || _destBufferLong.Length < length)
                    _destBufferLong = new long[length];
            if (_residualBuffer == null || _residualBuffer.Length < length)
                _residualBuffer = new int[length];

            _handle1 = GCHandle.Alloc(_destBuffer, GCHandleType.Pinned);
            _handle2 = GCHandle.Alloc(_destBufferLong, GCHandleType.Pinned);
            _handle3 = GCHandle.Alloc(_residualBuffer, GCHandleType.Pinned);

            var output = new FlacSubFrameData[channels];

            fixed (int* ptrDestBuffer = _destBuffer, ptrResidualBuffer = _residualBuffer)
            fixed (long* ptrDestBufferLong = _destBufferLong)
            {
                for (int c = 0; c < channels; c++)
                {
                    var offset = c * Header.BlockSize;
                    FlacSubFrameData data = new FlacSubFrameData
                    {
                        DestBuffer = ptrDestBuffer + offset,
                        DestBufferLong = ptrDestBufferLong + offset,
                        ResidualBuffer = ptrResidualBuffer + offset,
                        IsLong = _isLong
                    };
                    output[c] = data;
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
            if (_handle3.IsAllocated)
                _handle3.Free();
        }

        ~FlacFrame()
        {
            FreeBuffers();
        }
    }
}