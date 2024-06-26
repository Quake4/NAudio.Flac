﻿namespace NAudio.Flac
{
    internal static class FlacConstant
    {
        public static readonly int[] SampleRateTable =
        {
            -1, 88200, 176400, 192000,
            8000, 16000, 22050, 24000,
            32000, 44100, 48000, 96000,
            0, 0, 0
        };

        public static readonly int[] BitPerSampleTable =
        {
            -1, 8, 12, -1,
            16, 20, 24, 32
        };

        public static readonly int[] FlacBlockSizes =
        {
            -1, 192, 576, 1152,
            2304, 4608, 0, 0,
            256, 512, 1024, 2048,
            4096, 8192, 16384, 32768
        };

        public const int FrameHeaderSize = 16;

        public const int SubframeLpcQlpCoeffPrecisionLen = 4;

        public const int SubframeLpcQlpCoeffPrecisionMax = (1 << SubframeLpcQlpCoeffPrecisionLen) - 1;

        public const int SubframeLpcQlpShiftLen = 5;

        public const int EntropyCodingMethodTypeLen = 2;

        public const int EntropyCodingMethodPartitionedRiceOrderLen = 4;
    }
}