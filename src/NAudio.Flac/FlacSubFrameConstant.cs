namespace NAudio.Flac
{
    public sealed class FlacSubFrameConstant : FlacSubFrameBase
    {
        public FlacSubFrameConstant(FlacBitReader reader, FlacFrameHeader header, FlacSubFrameData data, int bps)
            : base(header)
        {
            var value = (int)reader.ReadBits(bps);

            unsafe
            {
                int* ptr = data.DestBuffer;
                for (int i = 0; i < header.BlockSize; i++)
                    *ptr++ = value;
            }
        }
    }
}