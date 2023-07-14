namespace NAudio.Flac
{
    public unsafe class FlacSubFrameData
    {
        public int* DestBuffer;
        public int* ResidualBuffer;

        public FlacSubFrameData()
        {
        }
    }
}