using System.Diagnostics;

namespace NAudio.Flac
{
	public sealed partial class FlacFrame
	{
		public unsafe int GetBuffer(ref byte[] buffer)
		{
			if (HasError) return 0;

			int desiredsize = Header.BlockSize * Header.Channels * ((Header.BitsPerSample + 7) / 8); // align to bytes
			if (buffer == null || buffer.Length < desiredsize)
				buffer = new byte[desiredsize];

			fixed (byte* ptrBuffer = buffer)
			{
				int val;
				if (Header.BitsPerSample < 8) { };
				if (Header.BitsPerSample == 8)
				{
					byte* ptr = ptrBuffer;
					// optimized for stereo
					if (Header.Channels == 2)
						for (int i = 0; i < Header.BlockSize; i++)
						{
							*(ptr++) = (byte)(_data[0].DestBuffer[i] + 0x80);
							*(ptr++) = (byte)(_data[1].DestBuffer[i] + 0x80);
						}
					else
						for (int i = 0; i < Header.BlockSize; i++)
							for (int c = 0; c < Header.Channels; c++)
								*(ptr++) = (byte)(_data[c].DestBuffer[i] + 0x80);
					return (int)(ptr - ptrBuffer);
				}
				else if (Header.BitsPerSample <= 16)
				{
					var shift = 16 - Header.BitsPerSample;
					short* ptr = (short*)ptrBuffer;
					// optimized for stereo
					if (Header.Channels == 2)
						for (int i = 0; i < Header.BlockSize; i++)
						{
							val = _data[0].DestBuffer[i];
							val <<= shift;
							*(ptr++) = (short)val;

							val = _data[1].DestBuffer[i];
							val <<= shift;
							*(ptr++) = (short)val;
						}
					else
						for (int i = 0; i < Header.BlockSize; i++)
							for (int c = 0; c < Header.Channels; c++)
							{
								val = _data[c].DestBuffer[i];
								val <<= shift;
								*(ptr++) = (short)val;
							}
					return (int)((byte*)ptr - ptrBuffer);
				}
				else if (Header.BitsPerSample <= 24)
				{
					var shift = 24 - Header.BitsPerSample;
					byte* ptr = ptrBuffer;
					// optimized for stereo
					if (Header.Channels == 2)
						for (int i = 0; i < Header.BlockSize; i++)
						{
							val = _data[0].DestBuffer[i];
							val <<= shift;
							*(ptr++) = (byte)(val & 0xFF);
							*(ptr++) = (byte)((val >> 8) & 0xFF);
							*(ptr++) = (byte)((val >> 16) & 0xFF);

							val = _data[1].DestBuffer[i];
							val <<= shift;
							*(ptr++) = (byte)(val & 0xFF);
							*(ptr++) = (byte)((val >> 8) & 0xFF);
							*(ptr++) = (byte)((val >> 16) & 0xFF);
						}
					else
						for (int i = 0; i < Header.BlockSize; i++)
							for (int c = 0; c < Header.Channels; c++)
							{
								val = _data[c].DestBuffer[i];
								val <<= shift;
								*(ptr++) = (byte)(val & 0xFF);
								*(ptr++) = (byte)((val >> 8) & 0xFF);
								*(ptr++) = (byte)((val >> 16) & 0xFF);
							}
					return (int)(ptr - ptrBuffer);
				}
				else if (Header.BitsPerSample <= 32)
				{
					var shift = 32 - Header.BitsPerSample;
					int* ptr = (int*)ptrBuffer;
					// optimized for stereo
					if (Header.Channels == 2)
						for (int i = 0; i < Header.BlockSize; i++)
						{
							val = _data[0].DestBuffer[i];
							val <<= shift;
							*(ptr++) = val;

							val = _data[1].DestBuffer[i];
							val <<= shift;
							*(ptr++) = val;
						}
					else
						for (int i = 0; i < Header.BlockSize; i++)
							for (int c = 0; c < Header.Channels; c++)
							{
								val = _data[c].DestBuffer[i];
								val <<= shift;
								*(ptr++) = val;
							}
					return (int)((byte*)ptr - ptrBuffer);
				}
				string error = "FlacFrame::GetBuffer: Invalid Flac-BitsPerSample: " + Header.BitsPerSample + ".";
				Debug.WriteLine(error);
				throw new FlacException(error, FlacLayer.Frame);
			}
		}
	}
}