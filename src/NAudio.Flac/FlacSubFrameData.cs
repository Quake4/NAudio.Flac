namespace NAudio.Flac
{
    public unsafe class FlacSubFrameData
    {
        public int* DestBuffer;
        public long* DestBufferLong;
        public bool IsLong = false;
        
        public int* ResidualBuffer;
    }
}