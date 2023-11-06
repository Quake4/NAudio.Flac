using System;

namespace NAudio.Flac
{
    public class FlacPartitionedRice
    {
        private readonly int _partitionOrder;
        private readonly FlacEntropyCoding _codingMethod;

        public FlacPartitionedRice(int partitionOrder, FlacEntropyCoding codingMethod)
        {
            _partitionOrder = partitionOrder;
            _codingMethod = codingMethod;
        }

        public unsafe bool ProcessResidual(FlacBitReader reader, FlacFrameHeader header, FlacSubFrameData data, int order)
        {
            int psize = header.BlockSize >> _partitionOrder;
            int resCnt = psize - order;
            int riceLength = 4 + (int)_codingMethod; //4bit = RICE I | 5bit = RICE II
            int riceMax = (1 << riceLength) - 1;

            //residual
            int j = order;
            int* r = data.ResidualBuffer + j;

            int partitioncount = 1 << _partitionOrder;
            for (int p = 0; p < partitioncount; p++)
            {
                if (p == 1) resCnt = psize;
                int n = Math.Min(resCnt, header.BlockSize - j);

                int k = (int)reader.ReadBits(riceLength);
                if (k < riceMax)
                {
                    ReadFlacRiceBlock(reader, n, k, r);
                    r += n;
                }
                else
                {
                    k = (int)reader.ReadBits(5);
                    if (k == 0)
                        for (int i = n; i > 0; i--)
                            *(r++) = 0;
                    else
                        for (int i = n; i > 0; i--)
                            *(r++) = reader.ReadBitsSigned(k);
                }
                j += n;
            }

            return true;
        }

        private unsafe void ReadFlacRiceBlock(FlacBitReader reader, int nvals, int riceParameter, int* ptrDest)
        {
            fixed (byte* putable = FlacBitReader.UnaryTable)
            {
                if (riceParameter == 0)
                {
                    for (int i = 0; i < nvals; i++)
                    {
                        *(ptrDest++) = reader.ReadUnarySigned();
                    }
                }
                else
                {
                    uint mask = (1u << riceParameter) - 1;
                    for (int i = 0; i < nvals; i++)
                    {
                        uint bits = putable[reader.Cache >> 24];
                        uint msbs = bits;

                        while (bits == 8)
                        {
                            reader.SeekBits(8);
                            bits = putable[reader.Cache >> 24];
                            msbs += bits;
                        }

                        // bits | stop bit | riceParameter bits | next data bits | BitOffset (no data bits 0 - 7, see PeekCache)

                        uint uval = msbs << riceParameter;
                        int btsk = riceParameter + (int)bits + 1;
                        if (btsk <= 32 - reader.BitOffset)
                        {
                            // optimized code - one call of seekbits
                            uval |= (reader.Cache >> (32 - btsk)) & mask;
                            reader.SeekBits(btsk);
                        }
                        else
                        {
                            // sign bit/s outside cache value - or readable code
                            reader.SeekBits((int)bits + 1);
                            uval |= reader.Cache >> (32 - riceParameter);
                            reader.SeekBits(riceParameter);
                        }
                        *(ptrDest++) = (int)(uval >> 1 ^ -(int)(uval & 1));
                    }
                }
            }
        }
    }
}