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
using System.Text;

namespace NAudio.Flac
{
	public sealed class FlacMetadataPicture : FlacMetadata
	{
		public int Type { get; private set; }
		public string Mime { get; private set; }
		public string Description { get; private set; }
		public int Width { get; private set; }
		public int Height { get; private set; }
		public int BitDepth { get; private set; }
		public int IndexedColor { get; private set; }
		public byte[] Picture { get; private set; }

		public FlacMetadataPicture(Stream stream, Int32 length, bool lastBlock)
			: base(FlacMetaDataType.Picture, lastBlock, length)
		{
			try
			{
				var reader = new BinaryReader(stream);
				Type = ReadInt32R(reader);
				Mime = Encoding.UTF8.GetString(reader.ReadBytes(ReadInt32R(reader)));
				Description = Encoding.UTF8.GetString(reader.ReadBytes(ReadInt32R(reader)));
				Width = ReadInt32R(reader);
				Height = ReadInt32R(reader);
				BitDepth = ReadInt32R(reader);
				IndexedColor = ReadInt32R(reader);
				Picture = reader.ReadBytes(ReadInt32R(reader));
			}
			catch (IOException e)
			{
				throw new FlacException(e, FlacLayer.Metadata);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		static Int32 ReadInt32R(BinaryReader reader)
		{
			var bytes = reader.ReadBytes(4);
			Array.Reverse(bytes);
			return BitConverter.ToInt32(bytes, 0);
		}
	}
}