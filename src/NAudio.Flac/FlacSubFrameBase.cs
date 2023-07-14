using System.Diagnostics;

namespace NAudio.Flac
{
    public class FlacSubFrameBase
    {
        public const int Constant = 0x00;
        public const int Verbatim = 0x01;
        public const int FixedMask = 0x08;
        public const int LPCMask = 0x20;

        public unsafe static FlacSubFrameBase GetSubFrame(FlacBitReader reader, FlacSubFrameData data, FlacFrameHeader header, int bps)
        {
            int wastedBits = 0;

            uint x = reader.ReadBits(8);
            bool hasWastedBits = (x & 1) != 0;

            if ((x & 0x80) != 0)
            {
                Debug.WriteLine("Flacdecoder lost sync while reading FlacSubFrameHeader. [x & 0x80].");
                return null;
            }

            x = (x & 0xFE) >> 1;

            if (hasWastedBits)
            {
                int u = (int)reader.ReadUnary();
                wastedBits = u + 1;
                bps -= wastedBits;
            }

            FlacSubFrameBase subFrame;

            if (x == Constant)
            {
                subFrame = new FlacSubFrameConstant(reader, header, data, bps);
            }
            else if (x == Verbatim)
            {
                subFrame = new FlacSubFrameVerbatim(reader, header, data, bps);
            }
            else if ((x & LPCMask) > 0)
            {
                subFrame = new FlacSubFrameLPC(reader, header, data, bps, (int)((x & (LPCMask - 1)) + 1));
            }
            else if ((x & FixedMask) > 0)
            {
                var order = (int)(x & (FixedMask - 1));
                if (order > 4)
                {
                    Debug.WriteLine("Invalid FlacFixedSubFrame predictororder: method = " + order + ".");
                    return null;
                }
                subFrame = new FlacSubFrameFixed(reader, header, data, bps, order);
            }
            else
            {
                Debug.WriteLine("Invalid Flac-SubframeType: x = " + x + ".");
                return null;
            }

            if (hasWastedBits)
            {
                int* ptrDest = data.DestBuffer;
                for (int i = 0; i < header.BlockSize; i++)
                {
                    *(ptrDest++) <<= wastedBits;
                }
            }

            return subFrame;
        }

        public FlacFrameHeader Header { get; protected set; }

        protected FlacSubFrameBase(FlacFrameHeader header)
        {
            Header = header;
        }
    }
}