/*

Copyright © 2023-2024 Oleg Samsonov aka Quake4. All rights reserved.
https://github.com/Quake4/SOV.NAudio

This Source Code Form is subject to the terms of the Mozilla
Public License, v. 2.0. If a copy of the MPL was not distributed
with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

*/
using NAudio.Wave;
using System;
using System.IO;

namespace NAudio.Flac
{
	/// <summary>
	/// Flac decoder for mp4/m4a
	/// </summary>
	public class FlacDecoder
	{
		public readonly WaveFormat SourceWaveFormat;
		public readonly WaveFormat WaveFormat;
		public readonly long TotalSamples;

		private readonly FlacMetadataStreamInfo _streamInfo;

		public FlacDecoder(byte[] data)
		{
			if (data == null)
				throw new ArgumentOutOfRangeException("data", "StreamInfo is null!");

			using (var ms = new MemoryStream(data))
			{
				_streamInfo = FlacMetadata.FromStream(ms) as FlacMetadataStreamInfo;
				if (_streamInfo == null)
					throw new ArgumentException("StreamInfo doesn't parsed!");

				SourceWaveFormat = new WaveFormat(_streamInfo.SampleRate, _streamInfo.BitsPerSample, _streamInfo.Channels);
				WaveFormat = new WaveFormat(_streamInfo.SampleRate, (_streamInfo.BitsPerSample + 7) / 8 * 8, _streamInfo.Channels);
				TotalSamples = _streamInfo.TotalSamples;
			}
		}

		public byte[] Decode(byte[] packet)
		{
			using (var ms = new MemoryStream(packet))
			{
				var frame = new FlacFrame(ms, _streamInfo);
				if (!frame.NextFrame())
					throw new FlacException("Decoding error", FlacLayer.Frame);
				byte[] buffer = null;
				int bufferlength = frame.GetBuffer(ref buffer);
				frame.FreeBuffers();
				Array.Resize(ref buffer, bufferlength);
				return buffer;
			}
		}
	}
}