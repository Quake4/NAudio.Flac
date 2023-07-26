namespace NAudio.Flac
{
    public class FlacSeekPoint
    {
        public ulong Number { get; private set; }

        public ulong Offset { get; private set; }

        public ushort FrameSize { get; private set; }

        public FlacSeekPoint()
        {
        }

        public FlacSeekPoint(ulong number, ulong offset, ushort frameSize)
        {
            Number = number;
            Offset = offset;
            FrameSize = frameSize;
        }
    }
}