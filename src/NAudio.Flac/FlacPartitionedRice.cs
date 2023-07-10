﻿using System;

namespace NAudio.Flac
{
    public class FlacPartitionedRice
    {
        public int PartitionOrder { get; private set; }

        public FlacEntropyCoding CodingMethod { get; private set; }

        public FlacPartitionedRice(int partitionOrder, FlacEntropyCoding codingMethod)
        {
            PartitionOrder = partitionOrder;
            CodingMethod = codingMethod;
        }

        public unsafe bool ProcessResidual(FlacBitReader reader, FlacFrameHeader header, FlacSubFrameData data, int order)
        {
            //data.Content.UpdateSize(PartitionOrder);

            int porder = PartitionOrder;
            FlacEntropyCoding codingMethod = CodingMethod;

            int psize = header.BlockSize >> porder;
            int resCnt = psize - order;

            int ricelength = 4 + (int)codingMethod; //4bit = RICE I | 5bit = RICE II

            //residual
            int j = order;
            int* r = data.ResidualBuffer + j;

            int partitioncount = 1 << porder;

            for (int p = 0; p < partitioncount; p++)
            {
                if (p == 1) resCnt = psize;
                int n = Math.Min(resCnt, header.BlockSize - j);

                int k = /*data.Content.Parameters[p] =*/ (int)reader.ReadBits(ricelength);
                if (k == (1 << ricelength) - 1)
                {
                    k = (int)reader.ReadBits(5);
                    for (int i = n; i > 0; i--)
                        *(r++) = k == 0 ? 0 : reader.ReadBitsSigned(k);
                }
                else
                {
                    ReadFlacRiceBlock(reader, n, k, r);
                    r += n;
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

                        // bits | stop bit | riceParameter bits | next data bits

                        uint uval = 0;
                        // no any speed up
                        /*int btsk = riceParameter + (int)bits + 1;
                        if (riceParameter <= 32 - btsk)
                        {
                            // optimized code - one call of seekbits
                            uval = (msbs << riceParameter) | ((reader.Cache >> (32 - btsk)) & mask);
                            reader.SeekBits(btsk);
                        }
                        else*/
                        {
                            // sign bit/s outside cache value - or readable code
                            reader.SeekBits((int)bits + 1);
                            uval = (msbs << riceParameter) | (reader.Cache >> (32 - riceParameter));
                            reader.SeekBits(riceParameter);
                        }
                        *(ptrDest++) = (int)(uval >> 1 ^ -(int)(uval & 1));
                    }
                }
            }
        }
    }
}