/*

Copyright © 2023 Oleg Samsonov aka Quake4. All rights reserved.
https://github.com/Quake4/NAudio.Flac

This Source Code Form is subject to the terms of the Mozilla
Public License, v. 2.0. If a copy of the MPL was not distributed
with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

*/
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NAudio.Flac
{
	public class FlacMetadataVorbisComment : FlacMetadata
	{
		private readonly string[] comments;
		private readonly string vendor;

		public FlacMetadataVorbisComment(Stream stream, Int32 length, bool lastBlock)
			: base(FlacMetaDataType.VorbisComment, lastBlock, length)
		{
			var comm = new HashSet<string>();
			try
			{
				var position = stream.Position;
				BinaryReader reader = new BinaryReader(stream);
				var bytes = reader.ReadBytes((int)reader.ReadUInt32());
				vendor = Encoding.UTF8.GetString(bytes);
				var count = reader.ReadUInt32();
				while (count-- > 0)
				{
					bytes = reader.ReadBytes((int)reader.ReadUInt32());
					comm.Add(Encoding.UTF8.GetString(bytes));
				}
			}
			catch (IOException e)
			{
				throw new FlacException(e, FlacLayer.Metadata);
			}
			finally
			{
				comments = comm.ToArray();
			}
		}

		public string Vendor => vendor;
		public string[] Comments => comments;

		public string this[int index]
		{
			get
			{
				return comments[index];
			}
		}
	}
}