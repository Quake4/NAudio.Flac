using System;
using System.Diagnostics;
using System.IO;

namespace NAudio.Flac
{
    public sealed class FlacFrameHeader
    {
        private int _blocksizeHint = 0; //if bsindex == 6 || 7
        private int _sampleRateHint = 0; //if sampleRateIndex == 12 || 13 || 14

        public int BlockSize { get; set; }

        public int SampleRate { get; set; }

        public int Channels { get; set; }

        public FlacChannelAssignment ChannelAssignment { get; set; }

        public int BitsPerSample { get; set; }

        //union
        public FlacNumberType NumberType { get; set; }

        public ulong SampleNumber { get; set; }

        public uint FrameNumber { get; set; }

        public byte CRC8 { get; set; }

        public bool DoCRC { get; private set; }

        public bool HasError { get; private set; }

        internal bool PrintErrors = true;

        public int Length { get; private set; }

        public FlacFrameHeader(Stream stream)
            : this(stream, null, true)
        {
        }

        public FlacFrameHeader(Stream stream, FlacMetadataStreamInfo streamInfo)
            : this(stream, streamInfo, true)
        {
        }

        //streamInfo can be null
        public FlacFrameHeader(Stream stream, FlacMetadataStreamInfo streamInfo, bool doCrc)
        {
            if (stream == null) throw new ArgumentNullException("stream");
            if (stream.CanRead == false) throw new ArgumentException("stream is not readable");
            //streamInfo can be null

            DoCRC = doCrc;
            var position = stream.Position;

            byte[] headerBuffer = new byte[FlacConstant.FrameHeaderSize];

            HasError = !ParseHeader(stream, headerBuffer, streamInfo);
            if (HasError)
                stream.Position = position;
        }

        public unsafe FlacFrameHeader(byte* buffer, FlacMetadataStreamInfo streamInfo, bool doCrc)
            : this(buffer, streamInfo, doCrc, true)
        {
        }

        internal unsafe FlacFrameHeader(byte* buffer, FlacMetadataStreamInfo streamInfo, bool doCrc, bool logError)
        {
            PrintErrors = logError; //optimized for prescan

            DoCRC = doCrc;

            HasError = !ParseHeader(buffer, streamInfo);
        }

        private unsafe bool ParseHeader(Stream stream, byte[] headerBuffer, FlacMetadataStreamInfo streamInfo)
        {
            const string loggerLocation = "FlacFrameHeader.ParseHeader(Stream, FlacMetadataStreamInfo)";

            if (stream.Read(headerBuffer, 0, headerBuffer.Length) == headerBuffer.Length)
            {
                fixed (byte* ptrBuffer = headerBuffer)
                {
                    bool result = ParseHeader(ptrBuffer, streamInfo);
                    stream.Position -= (headerBuffer.Length - Length);
                    return result;
                }
            }
            else
            {
                Error("Not able to read Flac header - EOF?", loggerLocation);
                return false;
            }
        }

        private unsafe bool ParseHeader(byte* headerBuffer, FlacMetadataStreamInfo streamInfo)
        {
            const string loggerLocation = "FlacFrameHeader.ParseHeader(byte*, FlacMetadataStreamInfo)";
            int x = -1; //tmp value to store in
            if (headerBuffer[0] == 0xFF && headerBuffer[1] >> 1 == 0x7C) //sync bits
            {
                if ((headerBuffer[1] & 0x02) != 0) // ...10 2. letzes bits muss 0 sein
                {
                    Error("Invalid FlacFrame. Reservedbit_0 is 1", loggerLocation);
                    return false;
                }

                FlacBitReader reader = new FlacBitReader(headerBuffer, 0);

                #region blocksize

                //blocksize
                x = headerBuffer[2] >> 4;

                if (x <= 0 || x >= FlacConstant.FlacBlockSizes.Length)
                {
                    Error("Invalid Blocksize value: " + x, loggerLocation);
                    return false;
                }
                else
                    BlockSize = FlacConstant.FlacBlockSizes[x];
                if (BlockSize == 0)
                    _blocksizeHint = x;

                #endregion blocksize

                #region samplerate

                //samplerate
                x = headerBuffer[2] & 0x0F;

                if (x <= 0 || x >= FlacConstant.SampleRateTable.Length)
                {
                    if (streamInfo != null)
                        SampleRate = streamInfo.SampleRate;
                    else
                    {
                        Error("Missing Samplerate. Samplerate = " + x + " && streamInfoMetaData == null.", loggerLocation);
                        return false;
                    }
                }
                else
                    SampleRate = FlacConstant.SampleRateTable[x];
                if (SampleRate == 0)
                    _sampleRateHint = x;

                #endregion samplerate

                #region channels

                x = headerBuffer[3] >> 4; //cc: unsigned
                int channels = -1;
                if ((x & 8) != 0)
                {
                    channels = 2;
                    x = x & 7;
                    if (x > 2 || x < 0)
                    {
                        Error("Invalid ChannelAssignment", loggerLocation);
                        return false;
                    }
                    else
                        ChannelAssignment = (FlacChannelAssignment)(x + 1);
                }
                else
                {
                    channels = x + 1;
                    ChannelAssignment = FlacChannelAssignment.Independent;
                }
                Channels = channels;

                #endregion channels

                #region bitspersample

                x = (headerBuffer[3] & 0x0E) >> 1;
                if (x == 0)
                {
                    if (streamInfo != null)
                        BitsPerSample = streamInfo.BitsPerSample;
                    else
                    {
                        Error("Missing BitsPerSample. Index = 0 && streamInfoMetaData == null.", loggerLocation);
                        return false;
                    }
                }
                else if (x == 3 || x >= FlacConstant.BitPerSampleTable.Length || x < 0)
                {
                    Error("Invalid BitsPerSampleIndex: " + x, loggerLocation);
                    return false;
                }
                else
                    BitsPerSample = FlacConstant.BitPerSampleTable[x];

                #endregion bitspersample

                if ((headerBuffer[3] & 0x01) != 0) // reserved bit -> 0
                {
                    Error("Invalid FlacFrame. Reservedbit_1 is 1", loggerLocation);
                    return false;
                }

                //reader.SkipBits(4 * 8); //erste 3 bytes headerbytes überspringen, da diese schon ohne reader verarbeitet
                reader.SeekBits(32);

                //BYTE 4

                #region utf8

                //variable blocksize
                if ((headerBuffer[1] & 0x01) != 0 ||
                    (streamInfo != null && streamInfo.MinBlockSize != streamInfo.MaxBlockSize))
                {
                    ulong samplenumber;
                    if (reader.ReadUTF8_64(out samplenumber) && samplenumber != ulong.MaxValue)
                    {
                        NumberType = FlacNumberType.SampleNumber;
                        SampleNumber = samplenumber;
                    }
                    else
                    {
                        Error("Invalid UTF8 Samplenumber coding.", loggerLocation);
                        return false;
                    }
                }
                else //fixed blocksize
                {
                    uint framenumber;// = reader.ReadUTF8();

                    if (reader.ReadUTF8_32(out framenumber) && framenumber != uint.MaxValue)
                    {
                        NumberType = FlacNumberType.FrameNumber;
                        FrameNumber = framenumber;
                    }
                    else
                    {
                        Error("Invalid UTF8 Framenumber coding.", loggerLocation);
                        return false;
                    }
                }

                #endregion utf8

                #region read hints

                //blocksize am ende des frameheaders
                if (_blocksizeHint != 0)
                {
                    x = (int)reader.ReadBits(8);
                    if (_blocksizeHint == 7)
                    {
                        x = (x << 8) | (int)reader.ReadBits(8);
                    }
                    BlockSize = x + 1;
                }

                //samplerate am ende des frameheaders
                if (_sampleRateHint != 0)
                {
                    x = (int)reader.ReadBits(8);
                    if (_sampleRateHint != 12)
                    {
                        x = (x << 8) | (int)reader.ReadBits(8);
                    }
                    if (_sampleRateHint == 12)
                        SampleRate = x * 1000;
                    else if (_sampleRateHint == 13)
                        SampleRate = x;
                    else
                        SampleRate = x * 10;
                }

                #endregion read hints

                if (DoCRC)
                {
                    var crc8 = Flac.CRC8.Instance.CalcCheckSum(reader.Buffer, reader.Position);
                    CRC8 = (byte)reader.ReadBits(8);
                    if (CRC8 != crc8)
                    {
                        Error("CRC8 missmatch", loggerLocation);
                        return false;
                    }
                }

                Length = reader.Position;

                reader.Dispose();

                return true;
            }

            Error("Invalid Syncbits", loggerLocation);
            return false;
        }

        internal void Error(string msg, string location)
        {
            if (PrintErrors)
                Debug.WriteLine(location + msg);
        }

        public bool CompareTo(FlacFrameHeader header)
        {
            return (BitsPerSample == header.BitsPerSample &&
                    Channels == header.Channels &&
                    SampleRate == header.SampleRate);
        }
    }
}