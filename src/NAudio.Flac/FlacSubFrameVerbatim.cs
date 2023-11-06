namespace NAudio.Flac
{
    public sealed class FlacSubFrameVerbatim : FlacSubFrameBase
    {
        public FlacSubFrameVerbatim(FlacBitReader reader, FlacFrameHeader header, FlacSubFrameData data, int bps)
            : base(header)
        {
            unsafe
            {
                if (data.IsLong)
                {
                    long* ptrDest = data.DestBufferLong;
                    for (int i = 0; i < header.BlockSize; i++)
                        *ptrDest++ = reader.ReadBits64Signed(bps);
                }
                else
                {
                    int* ptrDest = data.DestBuffer;
                    for (int i = 0; i < header.BlockSize; i++)
                        *ptrDest++ = reader.ReadBitsSigned(bps);
                }
            }
        }
    }
}