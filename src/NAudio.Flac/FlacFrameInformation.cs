﻿namespace NAudio.Flac
{
    /// <summary>
    /// Provides some basic information about a flac frame. This structure is typically used for implementing a seeking algorithm. 
    /// </summary>
    [System.Diagnostics.DebuggerDisplay("StreamOffset: {StreamOffset}")]
    public struct FlacFrameInformation
    {
        /// <summary>
        /// Gets the header of the flac frame.
        /// </summary>
        public FlacFrameHeader Header { get; set; }

        /// <summary>
        /// Gets the offset in bytes at which the frame starts in the flac stream (including the header of the frame).
        /// </summary>
        public long StreamOffset { get; set; }

        /// <summary>
        /// Gets the number samples which are contained by other frames before this frame occurs.
        /// </summary>
        public long SampleOffset { get; set; }
    }
}