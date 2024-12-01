using System;
using System.IO;
using System.Runtime.CompilerServices;

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
                    seekPoints[i] = new FlacSeekPoint(ReadUInt64R(reader), ReadUInt64R(reader), reader.ReadUInt16());
            }
            catch (IOException e)
            {
                throw new FlacException(e, FlacLayer.Metadata);
            }
        }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		static ulong ReadUInt64R(BinaryReader reader)
		{
			var bytes = reader.ReadBytes(8);
			Array.Reverse(bytes);
			return BitConverter.ToUInt64(bytes, 0);
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