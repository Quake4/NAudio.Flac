using System.Diagnostics;

namespace NAudio.Flac
{
    public class FlacSubFrameBase
    {
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

            if (x == 0)
            {
                //constant
                subFrame = new FlacSubFrameConstant(reader, header, data, bps);
            }
            else if (x == 1)
            {
                //verbatim
                subFrame = new FlacSubFrameVerbatim(reader, header, data, bps);
            }
            else if ((x & 0x20) > 0)
            {
                //lpc
                subFrame = new FlacSubFrameLPC(reader, header, data, bps, (int)((x & 31) + 1));
            }
            else if ((x & 0x08) > 0)
            {
                //fixed
                subFrame = new FlacSubFrameFixed(reader, header, data, bps, (int)(x & 7));
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

            subFrame.WastedBits = wastedBits;

            return subFrame;
        }

        public int WastedBits { get; protected set; }

        public FlacFrameHeader Header { get; protected set; }

        protected FlacSubFrameBase(FlacFrameHeader header)
        {
            Header = header;
        }
    }
}