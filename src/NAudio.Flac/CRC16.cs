/*

Copyright © 2023 Oleg Samsonov aka Quake4. All rights reserved.
https://github.com/Quake4/NAudio.Flac

This Source Code Form is subject to the terms of the Mozilla
Public License, v. 2.0. If a copy of the MPL was not distributed
with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

*/
namespace NAudio.Flac
{
	/// <summary>
	///     https://www.xiph.org/flac/format.html#frame_footer
	///     CRC-16 (polynomial = x^16 + x^15 + x^2 + x^0, initialized with 0) of everything before the
	///     crc, including the sync code
	/// </summary>
	internal class CRC16 : CRCBase<ushort>
    {
        private static CRC16 _instance;

        public CRC16()
        {
            CalcTable(16);
        }

        public static CRC16 Instance
        {
            get { return _instance ?? (_instance = new CRC16()); }
        }

        public override ushort CalcCheckSum(byte[] buffer, int offset, int count)
        {
            int crc = 0;
            for (int i = offset; i < offset + count; i++)
				crc = ((crc << 8) ^ crc_table[(crc >> 8) ^ buffer[i]]) & 0xffff;
			return (ushort)crc;
        }

        public unsafe ushort CalcCheckSum(byte* buffer, int count)
        {
            int crc = 0;
			byte* ptr = buffer;
            for (int i = 0; i < count; i++)
				crc = ((crc << 8) ^ crc_table[(crc >> 8) ^ *(ptr++)]) & 0xffff;
            return (ushort)crc;
        }
    }
}