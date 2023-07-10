﻿using System;
using System.Diagnostics;

namespace NAudio.Flac
{
    public sealed class FlacSubFrameLPC : FlacSubFrameBase
    {
        private readonly int[] _qlpCoeffs;
        private readonly int _lpcShiftNeeded;
        private readonly int _qlpCoeffPrecision;

        public int QLPCoeffPrecision { get { return _qlpCoeffPrecision; } }

        public int LPCShiftNeeded { get { return _lpcShiftNeeded; } }

        public int[] QLPCoeffs { get { return _qlpCoeffs; } }

        public FlacResidual Residual { get; private set; }

        public unsafe FlacSubFrameLPC(FlacBitReader reader, FlacFrameHeader header, FlacSubFrameData data, int bps, int order)
            : base(header)
        {
            //warmup
            for (int i = 0; i < order; i++)
            {
                data.ResidualBuffer[i] = data.DestBuffer[i] = reader.ReadBitsSigned(bps);
            }

            //header
            _qlpCoeffPrecision = (int)reader.ReadBits(FlacConstant.SubframeLpcQlpCoeffPrecisionLen) + 1;
            if (_qlpCoeffPrecision >= (1 << FlacConstant.SubframeLpcQlpCoeffPrecisionLen))
            {
                System.Diagnostics.Debug.WriteLine("Invalid FlacLPC qlp coeff precision.");
                return; //return false;
            }

            _lpcShiftNeeded = reader.ReadBitsSigned(FlacConstant.SubframeLpcQlpShiftLen);
            if (_lpcShiftNeeded < 0)
                throw new Exception("negative shift");

            _qlpCoeffs = new int[order];

            //qlp coeffs
            for (int i = 0; i < order; i++)
            {
                _qlpCoeffs[i] = reader.ReadBitsSigned(_qlpCoeffPrecision);
            }

            Residual = new FlacResidual(reader, header, data, order);

            if (bps <= 16)
                RestoreLPCSignal(data.ResidualBuffer + order, data.DestBuffer + order, header.BlockSize - order, order);
            else
                RestoreLPCSignalWide(data.ResidualBuffer + order, data.DestBuffer + order, header.BlockSize - order, order);
        }

        private unsafe void RestoreLPCSignal(int* residual, int* destination, int length, int order)
        {
            int sum = 0;

            int* r = residual;
            int* history;
            int* dest = destination;

            for (int i = 0; i < length; i++)
            {
                sum = 0;
                history = dest;
                for (int j = 0; j < order; j++)
                {
                    sum += _qlpCoeffs[j] * *(--history);
                }

                *(dest++) = *(r++) + (sum >> _lpcShiftNeeded);
            }
        }

        private unsafe void RestoreLPCSignalWide(int* residual, int* destination, int length, int order)
        {
            long sum = 0;

            int* r = residual;
            int* history;
            int* dest = destination;

            for (int i = 0; i < length; i++)
            {
                sum = 0;
                history = dest;
                for (int j = 0; j < order; j++)
                {
                    sum += (long)_qlpCoeffs[j] * *(--history);
                }

                *(dest++) = (int)(*(r++) + (sum >> _lpcShiftNeeded));
            }
        }
    }
}