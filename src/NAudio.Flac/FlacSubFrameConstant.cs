namespace NAudio.Flac
{
    public sealed class FlacSubFrameConstant : FlacSubFrameBase
    {
        public FlacSubFrameConstant(FlacBitReader reader, FlacFrameHeader header, FlacSubFrameData data, int bps)
            : base(header)
        {
            if (data.IsLong)
            {
                var value = reader.ReadBits64Signed(bps);

                unsafe
                {
                    long* ptr = data.DestBufferLong;
                    for (int i = 0; i < header.BlockSize; i++)
                        *ptr++ = value;
                }
            }
            else
            {
                var value = reader.ReadBitsSigned(bps);

                unsafe
                {
                    int* ptr = data.DestBuffer;
                    for (int i = 0; i < header.BlockSize; i++)
                        *ptr++ = value;
                }
            }
        }
    }
}