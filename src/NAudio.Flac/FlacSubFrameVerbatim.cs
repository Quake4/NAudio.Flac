namespace NAudio.Flac
{
    public sealed class FlacSubFrameVerbatim : FlacSubFrameBase
    {
        public FlacSubFrameVerbatim(FlacBitReader reader, FlacFrameHeader header, FlacSubFrameData data, int bps)
            : base(header)
        {
            unsafe
            {
                int* ptrDest = data.DestBuffer;
                for (int i = 0; i < header.BlockSize; i++)
                    *ptrDest++ = (int)reader.ReadBits(bps);
            }
        }
    }
}