using System;
using System.IO;
using System.Text;

namespace NAudio.Flac
{
    public class FlacMetadataStreamInfo : FlacMetadata
    {
        public unsafe FlacMetadataStreamInfo(Stream stream, Int32 length, bool lastBlock)
            : base(FlacMetaDataType.StreamInfo, lastBlock, length)
        {
            //http://flac.sourceforge.net/format.html#metadata_block_streaminfo
            var reader = new BinaryReader(stream);
            int bytesToRead = 18; // 18 = 34 - 16 (md5)
            byte[] buffer = reader.ReadBytes(bytesToRead);
            if (buffer.Length != bytesToRead)
                throw new FlacException(new EndOfStreamException("Could not read StreamInfo-content"), FlacLayer.Metadata);

            fixed (byte* b = buffer)
            {
                FlacBitReader bitreader = new FlacBitReader(b, 0);
                MinBlockSize = bitreader.ReadUInt16();
                MaxBlockSize = bitreader.ReadUInt16();
                MinFrameSize = bitreader.ReadBits(24);
                MaxFrameSize = bitreader.ReadBits(24);
                SampleRate = (int)bitreader.ReadBits(20);
                Channels = 1 + (int)bitreader.ReadBits(3);
                BitsPerSample = 1 + (int)bitreader.ReadBits(5);
                TotalSamples = (long)bitreader.ReadBits64(36);
                MD5 = reader.ReadBytes(16);
                bitreader.Dispose();
            }

            if (MinFrameSize == 0)
                MinFrameSize = FlacConstant.FrameHeaderSize;
            if (MaxFrameSize == 0)
                MaxFrameSize = (uint)(MaxBlockSize * Channels * BitsPerSample >> 3);

            if (BitsPerSample > 24)
                throw new FlacException("Flac decoder support only 24bit audio", FlacLayer.Metadata);
        }

        public ushort MinBlockSize { get; private set; }

        public ushort MaxBlockSize { get; private set; }

        public uint MaxFrameSize { get; private set; }

        public uint MinFrameSize { get; private set; }

        public int SampleRate { get; private set; }

        public int Channels { get; private set; }

        public int BitsPerSample { get; private set; }

        /// <summary>
        /// 0 = Unknown
        /// </summary>
        public long TotalSamples { get; private set; }

        public byte[] MD5 { get; private set; }
    }
}