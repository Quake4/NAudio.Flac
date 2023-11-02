namespace NAudio.Flac
{
    public sealed class FlacSubFrameLPC : FlacSubFrameBase
    {
        private readonly int[] _qlpCoeffs;
        private readonly int _lpcShiftNeeded;
        private readonly int _qlpCoeffPrecision;

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
                System.Diagnostics.Debug.WriteLine("Invalid FlacLPC qlp coeff precision: {_qlpCoeffPrecision}.");
                HasError = true;
                return;
            }

            _lpcShiftNeeded = reader.ReadBitsSigned(FlacConstant.SubframeLpcQlpShiftLen);
            if (_lpcShiftNeeded < 0)
            {
                System.Diagnostics.Debug.WriteLine($"Negative shift in FlacLPC: {_lpcShiftNeeded}");
                HasError = true;
                return;
            }

            _qlpCoeffs = new int[order];

            //qlp coeffs
            for (int i = 0; i < order; i++)
            {
                _qlpCoeffs[i] = reader.ReadBitsSigned(_qlpCoeffPrecision);
            }

            // decode resudal
            new FlacResidual(reader, header, data, order);

            if (bps <= 16)
                RestoreLPCSignal(data.ResidualBuffer + order, data.DestBuffer + order, header.BlockSize - order, order);
            else
                RestoreLPCSignalWide(data.ResidualBuffer + order, data.DestBuffer + order, header.BlockSize - order, order);
        }

        private unsafe void RestoreLPCSignal(int* residual, int* destination, int length, int order)
        {
            int* history;
            int* dest = destination;
            int* ptrCoeff;
            int sum;
            int count;

            fixed (int* coeff = _qlpCoeffs)
            {
                for (int i = 0; i < length; i++)
                {
                    sum = 0;
                    history = dest;
                    ptrCoeff = coeff;

                    count = order;
                    // by four
                    while (count > 4)
                    {
                        sum += *ptrCoeff++ * *(--history) +
                            *ptrCoeff++ * *(--history) +
                            *ptrCoeff++ * *(--history) +
                            *ptrCoeff++ * *(--history);
                        count -= 4;
                    }
                    // rest
                    while (count-- > 0)
                        sum += *ptrCoeff++ * *(--history);

                    *(dest++) = *residual++ + (sum >> _lpcShiftNeeded);
                }
            }
        }

        private unsafe void RestoreLPCSignalWide(int* residual, int* destination, int length, int order)
        {
            int* history;
            int* dest = destination;
            int* ptrCoeff;
            long sum;
            int count;

            fixed (int* coeff = _qlpCoeffs)
            {
                for (int i = 0; i < length; i++)
                {
                    sum = 0;
                    history = dest;
                    ptrCoeff = coeff;

                    count = order;
                    // by four
                    while (count > 4)
                    {
                        sum += (long)*ptrCoeff++ * *(--history) +
                            (long)*ptrCoeff++ * *(--history) +
                            (long)*ptrCoeff++ * *(--history) +
                            (long)*ptrCoeff++ * *(--history);
                        count -= 4;
                    }
                    // rest
                    while (count-- > 0)
                        sum += (long)*ptrCoeff++ * *(--history);

                    var result = *residual++ + (sum >> _lpcShiftNeeded);
                    if (result > int.MaxValue || result < int.MinValue)
                        throw new FlacException($"Overflow restore lpc signal (repack flac file with fixed flac encoder): {int.MinValue} <= {result} <= {int.MaxValue} ", FlacLayer.SubFrame);

                    *(dest++) = (int)result;
                }
            }
        }
    }
}