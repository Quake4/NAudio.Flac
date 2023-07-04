namespace NAudio.Flac
{
    public sealed class FlacSubFrameConstant : FlacSubFrameBase
    {
        public int Value { get; private set; }

        public FlacSubFrameConstant(FlacBitReader reader, FlacFrameHeader header, FlacSubFrameData data, int bps)
            : base(header)
        {
            Value = (int)reader.ReadBits(bps);

            unsafe
            {
                int* ptr = data.DestBuffer;
                for (int i = 0; i < header.BlockSize; i++)
                    *ptr++ = Value;
            }
        }
    }
}