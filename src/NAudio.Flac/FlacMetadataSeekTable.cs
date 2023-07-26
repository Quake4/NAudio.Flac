using System;
using System.IO;

namespace NAudio.Flac
{
    public class FlacMetadataSeekTable : FlacMetadata
    {
        private readonly FlacSeekPoint[] seekPoints;

        public FlacMetadataSeekTable(Stream stream, Int32 length, bool lastBlock)
            : base(FlacMetaDataType.Seektable, lastBlock, length)
        {
            int entryCount = length / 18;
            seekPoints = new FlacSeekPoint[entryCount];
            BinaryReader reader = new BinaryReader(stream);
            try
            {
                for (int i = 0; i < entryCount; i++)
                {
                    var bytes = reader.ReadBytes(8);
                    Array.Reverse(bytes);
                    var number = BitConverter.ToUInt64(bytes, 0);
                    bytes = reader.ReadBytes(8);
                    Array.Reverse(bytes);
                    var offset = BitConverter.ToUInt64(bytes, 0);

                    seekPoints[i] = new FlacSeekPoint(number, offset, reader.ReadUInt16());
                }
            }
            catch (IOException e)
            {
                throw new FlacException(e, FlacLayer.Metadata);
            }
        }

        public FlacSeekPoint[] SeekPoints => seekPoints;

        public FlacSeekPoint this[int index]
        {
            get
            {
                return seekPoints[index];
            }
        }
    }
}