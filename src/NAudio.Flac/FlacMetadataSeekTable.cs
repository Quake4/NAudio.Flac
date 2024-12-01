/*

Copyright © 2023 Oleg Samsonov aka Quake4. All rights reserved.
https://github.com/Quake4/NAudio.Flac

This Source Code Form is subject to the terms of the Mozilla
Public License, v. 2.0. If a copy of the MPL was not distributed
with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

*/
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace NAudio.Flac
{
    public sealed class FlacMetadataSeekTable : FlacMetadata
    {
        private readonly FlacSeekPoint[] seekPoints;

        public FlacMetadataSeekTable(Stream stream, Int32 length, bool lastBlock)
            : base(FlacMetaDataType.Seektable, lastBlock, length)
        {
            int entryCount = length / 18;
            seekPoints = new FlacSeekPoint[entryCount];
            BinaryReader reader = new BinaryReader(stream);
            try
            {
                for (int i = 0; i < entryCount; i++)
                    seekPoints[i] = new FlacSeekPoint(ReadUInt64R(reader), ReadUInt64R(reader), reader.ReadUInt16());
            }
            catch (IOException e)
            {
                throw new FlacException(e, FlacLayer.Metadata);
            }
        }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		static ulong ReadUInt64R(BinaryReader reader)
		{
			var bytes = reader.ReadBytes(8);
			Array.Reverse(bytes);
			return BitConverter.ToUInt64(bytes, 0);
		}

        public FlacSeekPoint[] SeekPoints => seekPoints;

        public FlacSeekPoint this[int index]
        {
            get
            {
                return seekPoints[index];
            }
        }
    }
}