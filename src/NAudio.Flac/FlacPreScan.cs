using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace NAudio.Flac
{
    public sealed class FlacPreScan
    {
        private const int BufferSize = 524288;
        private readonly Stream _stream;
        private bool _isRunning;

        public event EventHandler<FlacPreScanFinishedEventArgs> ScanFinished;

        public List<FlacFrameInformation> Frames { get; private set; }

        public long TotalLength { get; private set; }

        public long TotalSamples { get; private set; }

        public FlacPreScan(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException("stream");
            if (!stream.CanRead) throw new ArgumentException("stream is not readable");

            _stream = stream;
        }

        public void ScanStream(FlacMetadataStreamInfo streamInfo, FlacPreScanMethodMode mode)
        {
            long saveOffset = _stream.Position;
            StartScan(streamInfo, mode);
            _stream.Position = saveOffset;

            long totalLength = 0, totalsamples = 0;
            foreach (var frame in Frames)
            {
                totalLength += frame.Header.BlockSize * frame.Header.BitsPerSample * frame.Header.Channels;
                totalsamples += frame.Header.BlockSize;
            }
            TotalLength = totalLength;
            TotalSamples = totalsamples;

            if (TotalSamples != streamInfo.TotalSamples)
                throw new Exception($"Scan failed. Total samples isn't equal in streaminfo and scaned.");
        }

        private void StartScan(FlacMetadataStreamInfo streamInfo, FlacPreScanMethodMode method)
        {
            if (_isRunning)
                throw new Exception("Scan is already running.");

            _isRunning = true;

            if (method == FlacPreScanMethodMode.Async)
            {
                ThreadPool.QueueUserWorkItem(o =>
                {
                    Frames = RunScan(streamInfo);
                    _isRunning = false;
                });
            }
            else
            {
                Frames = RunScan(streamInfo);
                _isRunning = false;
            }
        }

        private List<FlacFrameInformation> RunScan(FlacMetadataStreamInfo streamInfo)
        {
#if DEBUG
            Stopwatch watch = new Stopwatch();
            watch.Start();
#endif
            var result = ScanThisShit(streamInfo);

#if DEBUG
            watch.Stop();
            Debug.WriteLine(String.Format("FlacPreScan finished: {0} Bytes processed in {1} ms.",
                _stream.Length, watch.ElapsedMilliseconds));
#endif
            RaiseScanFinished(result);
            return result;
        }

        private void RaiseScanFinished(List<FlacFrameInformation> frames)
        {
            if (ScanFinished != null)
                ScanFinished(this, new FlacPreScanFinishedEventArgs(frames));
        }

        private unsafe List<FlacFrameInformation> ScanThisShit(FlacMetadataStreamInfo streamInfo)
        {
            Stream stream = _stream;

            //if (!(stream is BufferedStream))
            //    stream = new BufferedStream(stream);

            byte[] buffer = new byte[BufferSize];
            int read = 0;

            if (stream.Position <= (4 + streamInfo.Length))
            {
                stream.Position = 4; //fLaC
                FlacMetadata.ReadAllMetadataFromStream(stream);
            }

            List<FlacFrameInformation> frames = new List<FlacFrameInformation>();
            FlacFrameInformation frameInfo = new FlacFrameInformation();
            frameInfo.IsFirstFrame = true;

            FlacFrameHeader baseHeader = null;

            while (true)
            {
                read = stream.Read(buffer, 0, buffer.Length);
                if (read <= FlacConstant.FrameHeaderSize)
                    break;

                fixed (byte* bufferPtr = buffer)
                {
                    byte* ptr = bufferPtr;
                    //for (int i = 0; i < read - FlacConstant.FrameHeaderSize; i++)
                    while ((bufferPtr + read - FlacConstant.FrameHeaderSize) > ptr)
                    {
                        if (*ptr++ == 0xFF && (*ptr & 0xFE) == 0xF8) //check sync
                        {
                            byte* ptrSafe = ptr;
                            ptr--;
                            FlacFrameHeader tmp = null;
                            if (IsFrame(ref ptr, streamInfo, baseHeader, out tmp))
                            {
                                FlacFrameHeader header = tmp;
                                if (frameInfo.IsFirstFrame)
                                {
                                    baseHeader = header;
                                    frameInfo.IsFirstFrame = false;
                                }

                                if (baseHeader.CompareTo(header))
                                {
                                    frameInfo.StreamOffset = stream.Position - read + ((ptrSafe - 1) - bufferPtr);
                                    frameInfo.Header = header;

                                    if (frames.Count > 0)
                                    {
                                        var last = frames.Last();
                                        if (last.Header.FrameNumber + 1 != header.FrameNumber)
                                        {
                                            Debug.WriteLineIf(last.Header.FrameNumber + 1 != header.FrameNumber, $"Sequence missmatch: previous {last.Header.FrameNumber}, current {header.FrameNumber}");
                                            ptr = ptrSafe;
                                            continue;
                                        }
                                    }

                                    frames.Add(frameInfo);

                                    frameInfo.SampleOffset += header.BlockSize;
                                    var newPtr = (ptrSafe - 1) + streamInfo.MinFrameSize;
                                    if (bufferPtr + read <= newPtr)
                                        ptr = bufferPtr + read - 1;
                                    else
                                        ptr = newPtr;
                                }
                                else
                                {
                                    ptr = ptrSafe;
                                }
                                //todo:
                            }
                            else
                            {
                                ptr = ptrSafe;
                            }
                        }
                    }
                }

                stream.Position -= FlacConstant.FrameHeaderSize;
            }

            return frames;
        }

        private unsafe bool IsFrame(ref byte* buffer, FlacMetadataStreamInfo streamInfo, FlacFrameHeader baseHeader, out FlacFrameHeader header)
        {
            header = new FlacFrameHeader(ref buffer, streamInfo, true, false);
            return !header.HasError;
        }
    }
}