namespace NAudio.Flac
{
    public sealed class FlacSubFrameFixed : FlacSubFrameBase
    {
        public unsafe FlacSubFrameFixed(FlacBitReader reader, FlacFrameHeader header, FlacSubFrameData data, int bps, int order)
            : base(header)
        {
            if (data.IsLong)
                for (int i = 0; i < order; i++)
                {
                    var value = reader.ReadBits64Signed(bps);
                    data.DestBufferLong[i] = value;
                    data.ResidualBuffer[i] = (int)value;
                }
            else
                for (int i = 0; i < order; i++)
                    data.ResidualBuffer[i] = data.DestBuffer[i] = reader.ReadBitsSigned(bps);

            // resudal decoding
            new FlacResidual(reader, header, data, order);

            if (data.IsLong)
                RestoreSignalLong(data, header.BlockSize - order, order);
            else if (bps + order <= 32)
                RestoreSignal(data, header.BlockSize - order, order);
            else
                RestoreSignalWide(data, header.BlockSize - order, order);
        }

        //http://www.hpl.hp.com/techreports/1999/HPL-1999-144.pdf
        private unsafe void RestoreSignal(FlacSubFrameData subframeData, int length, int predictorOrder)
        {
            int* residual = subframeData.ResidualBuffer + predictorOrder;
            int* data = subframeData.DestBuffer + predictorOrder;

            switch (predictorOrder)
            {
                case 0:
                    for (int i = 0; i < length; i++)
                    {
                        *(data++) = *(residual++);
                    }
                    break;

                case 1:
                    for (int i = 0; i < length; i++)
                    {
                        *(data) = *(residual++) + data[-1];
                        data++;
                    }
                    break;

                case 2:
                    for (int i = 0; i < length; i++)
                    {
                        *(data) = *(residual++) + (data[-1] << 1) - data[-2];
                        data++;
                    }
                    break;

                case 3:
                    for (int i = 0; i < length; i++)
                    {
                        *(data) = *(residual++) +
                                    ((data[-1] - data[-2]) << 1) + (data[-1] - data[-2]) +
                                    data[-3];
                        data++;
                    }
                    break;

                case 4:
                    for (int i = 0; i < length; i++)
                    {
                        *(data) = *(residual++) +
                                    ((data[-1] + data[-3]) << 2) -
                                    ((data[-2] << 2) + (data[-2] << 1)) -
                                    data[-4];
                        data++;
                    }
                    break;

                default:
                    throw new FlacException($"Invalid FlacFixedSubFrame predictororder: {predictorOrder}.", FlacLayer.SubFrame);
            }
        }

		private unsafe void RestoreSignalWide(FlacSubFrameData subframeData, int length, int predictorOrder)
		{
			int* residual = subframeData.ResidualBuffer + predictorOrder;
			int* data = subframeData.DestBuffer + predictorOrder;

			long sum = 0;

			switch (predictorOrder)
			{
				case 0:
					for (int i = 0; i < length; i++)
					{
						*(data++) = *(residual++);
					}
					break;

				case 1:
					for (int i = 0; i < length; i++)
					{
						sum = *(residual++) + (long)data[-1];
						if (sum > int.MaxValue || sum < int.MinValue)
							throw new FlacException($"Overflow restore fixed signal (repack flac file with fixed flac encoder): {int.MinValue} <= {sum} <= {int.MaxValue} ", FlacLayer.SubFrame);
						*(data++) = (int)sum;
					}
					break;

				case 2:
					for (int i = 0; i < length; i++)
					{
						sum = *(residual++) + ((long)data[-1] << 1) - data[-2];
						if (sum > int.MaxValue || sum < int.MinValue)
							throw new FlacException($"Overflow restore fixed signal (repack flac file with fixed flac encoder): {int.MinValue} <= {sum} <= {int.MaxValue} ", FlacLayer.SubFrame);
						*(data++) = (int)sum;
					}
					break;

				case 3:
					for (int i = 0; i < length; i++)
					{
						sum = *(residual++) +
									(((long)data[-1] - data[-2]) << 1) + ((long)data[-1] - data[-2]) +
									data[-3];
						if (sum > int.MaxValue || sum < int.MinValue)
							throw new FlacException($"Overflow restore fixed signal (repack flac file with fixed flac encoder): {int.MinValue} <= {sum} <= {int.MaxValue} ", FlacLayer.SubFrame);
						*(data++) = (int)sum;
					}
					break;

				case 4:
					for (int i = 0; i < length; i++)
					{
						sum = *(residual++) +
									(((long)data[-1] + data[-3]) << 2) -
									(((long)data[-2] << 2) + ((long)data[-2] << 1)) -
									data[-4];
						if (sum > int.MaxValue || sum < int.MinValue)
							throw new FlacException($"Overflow restore fixed signal (repack flac file with fixed flac encoder): {int.MinValue} <= {sum} <= {int.MaxValue} ", FlacLayer.SubFrame);
						*(data++) = (int)sum;
					}
					break;

				default:
					throw new FlacException($"Invalid FlacFixedSubFrame predictororder: {predictorOrder}.", FlacLayer.SubFrame);
			}
		}

		private unsafe void RestoreSignalLong(FlacSubFrameData subframeData, int length, int predictorOrder)
		{
			int* residual = subframeData.ResidualBuffer + predictorOrder;
			long* data = subframeData.DestBufferLong + predictorOrder;

			long sum = 0;

			switch (predictorOrder)
			{
				case 0:
					for (int i = 0; i < length; i++)
					{
						*(data++) = *(residual++);
					}
					break;

				case 1:
					for (int i = 0; i < length; i++)
					{
						sum = *(residual++) + data[-1];
						*(data++) = sum;
					}
					break;

				case 2:
					for (int i = 0; i < length; i++)
					{
						sum = *(residual++) + (data[-1] << 1) - data[-2];
						*(data++) = sum;
					}
					break;

				case 3:
					for (int i = 0; i < length; i++)
					{
						sum = *(residual++) +
									((data[-1] - data[-2]) << 1) + (data[-1] - data[-2]) +
									data[-3];
						*(data++) = sum;
					}
					break;

				case 4:
					for (int i = 0; i < length; i++)
					{
						sum = *(residual++) +
									((data[-1] + data[-3]) << 2) -
									((data[-2] << 2) + (data[-2] << 1)) -
									data[-4];
						*(data++) = sum;
					}
					break;

				default:
					throw new FlacException($"Invalid FlacFixedSubFrame predictororder: {predictorOrder}.", FlacLayer.SubFrame);
			}
		}
	}
}