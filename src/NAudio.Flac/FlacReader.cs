//#define DIAGNOSTICS

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using NAudio.Wave;

namespace NAudio.Flac
{
    /// <summary>
    ///     Provides a decoder for decoding flac (Free Lostless Audio Codec) data.
    /// </summary>
    public class FlacReader : WaveStream, IDisposable, IWaveProvider
    {
        protected readonly Stream _stream;
        private readonly WaveFormat _sourceWaveFormat;
        private readonly WaveFormat _waveFormat;
        private readonly FlacMetadataStreamInfo _streamInfo;
        private readonly FlacSeekPoint[] _seekPoints;
        private FlacPreScan _scan;

        private readonly object _bufferLock = new object();
        private CancellationTokenSource _token;
        private long _position;
        protected long _dataStartPosition;

        //overflow:
        private byte[] _overflowBuffer;

        private int _overflowCount;
        private int _overflowOffset;

        private int _frameIndex;

        /// <summary>
        ///     Gets a list with all found metadata fields.
        /// </summary>
        public List<FlacMetadata> Metadata { get; protected set; }

        public WaveFormat SourceWaveFormat
        {
            get { return _sourceWaveFormat; }
        }

        /// <summary>
        ///     Gets the output <see cref="WaveFormat" /> of the decoder.
        /// </summary>
        public override WaveFormat WaveFormat
        {
            get { return _waveFormat; }
        }

        /// <summary>
        ///     Gets a value which indicates whether the seeking is supported. True means that seeking is supported; False means
        ///     that seeking is not supported.
        /// </summary>
        public override bool CanSeek
        {
            get { return _seekPoints != null || _scan != null; }
        }

        private FlacFrame _frame;

        private FlacFrame Frame
        {
            get { return _frame ?? (_frame = new FlacFrame(_stream, _streamInfo)); }
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="FlacReader" /> class.
        /// </summary>
        /// <param name="fileName">Filename which of a flac file which should be decoded.</param>
        public FlacReader(string fileName)
            : this(File.OpenRead(fileName))
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="FlacReader" /> class.
        /// </summary>
        /// <param name="stream">Stream which contains flac data which should be decoded.</param>
        public FlacReader(Stream stream)
            : this(stream, FlacPreScanMethodMode.Default)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="FlacReader" /> class.
        /// </summary>
        /// <param name="stream">Stream which contains flac data which should be decoded.</param>
        /// <param name="scanFlag">Scan mode which defines how to scan the flac data for frames.</param>
        public FlacReader(Stream stream, FlacPreScanMethodMode scanFlag)
            : this(stream, scanFlag, null)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="FlacReader" /> class.
        /// </summary>
        /// <param name="stream">Stream which contains flac data which should be decoded.</param>
        /// <param name="scanFlag">Scan mode which defines how to scan the flac data for frames.</param>
        /// <param name="onscanFinished">
        ///     Callback which gets called when the pre scan processes finished. Should be used if the
        ///     <paramref name="scanFlag" /> argument is set the <see cref="FlacPreScanMethodMode.Async" />.
        /// </param>
        public FlacReader(Stream stream, FlacPreScanMethodMode scanFlag,
            Action<FlacPreScanFinishedEventArgs> onscanFinished)
        {
            if (stream == null)
                throw new ArgumentNullException();
            if (!stream.CanRead)
                throw new ArgumentException("Stream is not readable.", "stream");

            _stream = stream;

            //skip ID3v2
            Id3v2Tag.ReadTag(stream);

            //read fLaC sync
            var beginSync = new byte[4];
            int read = stream.Read(beginSync, 0, beginSync.Length);
            if (read < beginSync.Length)
                throw new EndOfStreamException("Can not read \"fLaC\" sync.");
            if (beginSync[0] == 0x66 && beginSync[1] == 0x4C && //Check for 'fLaC' signature
                beginSync[2] == 0x61 && beginSync[3] == 0x43)
            {
                //read metadata
                List<FlacMetadata> metadata = FlacMetadata.ReadAllMetadataFromStream(stream);

                Metadata = metadata;
                if (metadata == null || metadata.Count <= 0)
                    throw new FlacException("No Metadata found.", FlacLayer.Metadata);

                var streamInfo =
                    metadata.First(x => x.MetaDataType == FlacMetaDataType.StreamInfo) as FlacMetadataStreamInfo;
                if (streamInfo == null)
                    throw new FlacException("No StreamInfo-Metadata found.", FlacLayer.Metadata);

                _seekPoints = (metadata.FirstOrDefault(x => x.MetaDataType == FlacMetaDataType.Seektable) as FlacMetadataSeekTable)?.SeekPoints;

                _streamInfo = streamInfo;
                _sourceWaveFormat = new WaveFormat(streamInfo.SampleRate, streamInfo.BitsPerSample, streamInfo.Channels);
                _waveFormat = new WaveFormat(streamInfo.SampleRate, (streamInfo.BitsPerSample + 7) / 8 * 8, streamInfo.Channels);
                _dataStartPosition = stream.Position;
                Debug.WriteLine("Flac StreamInfo found -> WaveFormat: " + _waveFormat);
                Debug.WriteLine("Flac-File-Metadata read.");
            }
            else
                throw new FlacException("Invalid Flac-File. \"fLaC\" Sync not found.", FlacLayer.Top);

            //prescan stream
            if (_seekPoints == null && scanFlag != FlacPreScanMethodMode.None)
            {
                var scan = new FlacPreScan(stream);
                scan.ScanFinished += (s, e) =>
                {
                    if (onscanFinished != null)
                        onscanFinished(e);
                };

                _token = new CancellationTokenSource();
                var token = _token.Token;
                if (scanFlag == FlacPreScanMethodMode.Async)
                    ThreadPool.QueueUserWorkItem(o => {
                        try
                        {
                            Thread.CurrentThread.Priority = ThreadPriority.Lowest;
                            if (!token.IsCancellationRequested)
                                _scan = scan.StartScan(_streamInfo, scanFlag, token);
                        }
                        finally
                        {
                            Thread.CurrentThread.Priority = ThreadPriority.Normal;
                        }
                    });
                else
                    _scan = scan.StartScan(_streamInfo, scanFlag, token);
            }
        }

        /// <summary>
        ///     Reads a sequence of bytes from the <see cref="FlacReader" /> and advances the position within the stream by the
        ///     number of bytes read.
        /// </summary>
        /// <param name="buffer">
        ///     An array of bytes. When this method returns, the <paramref name="buffer" /> contains the specified
        ///     byte array with the values between <paramref name="offset" /> and (<paramref name="offset" /> +
        ///     <paramref name="count" /> - 1) replaced by the bytes read from the current source.
        /// </param>
        /// <param name="offset">
        ///     The zero-based byte offset in the <paramref name="buffer" /> at which to begin storing the data
        ///     read from the current stream.
        /// </param>
        /// <param name="count">The maximum number of bytes to read from the current source.</param>
        /// <returns>The total number of bytes read into the buffer.</returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = 0;
            count -= (count % WaveFormat.BlockAlign);

            lock (_bufferLock)
            {
                read += GetOverflows(buffer, ref offset, count);

                while (read < count)
                {
                    FlacFrame frame = Frame;
                    if (frame == null)
                        return read;

                    while (!frame.NextFrame())
                    {
                        if (CanSeek) //go to next frame
                        {
                            if (++_frameIndex >= _scan.Frames.Length)
                                return read;
                            _stream.Position = _scan.Frames[_frameIndex].StreamOffset;
                        }
                        else if (_stream.Position == _stream.Length)
                            return read;
                        else
                            _stream.Position++;
                    }
                    _frameIndex++;

                    int bufferlength = frame.GetBuffer(ref _overflowBuffer);
                    int bytesToCopy = Math.Min(count - read, bufferlength);
                    Array.Copy(_overflowBuffer, 0, buffer, offset, bytesToCopy);
                    read += bytesToCopy;
                    offset += bytesToCopy;

                    _overflowCount = ((bufferlength > bytesToCopy) ? (bufferlength - bytesToCopy) : 0);
                    _overflowOffset = ((bufferlength > bytesToCopy) ? (bytesToCopy) : 0);
                }
            }
            _position += read;

            return read;
        }

        private int GetOverflows(byte[] buffer, ref int offset, int count)
        {
            if (_overflowCount != 0 && _overflowBuffer != null && count > 0)
            {
                int bytesToCopy = Math.Min(count, _overflowCount);
                Array.Copy(_overflowBuffer, _overflowOffset, buffer, offset, bytesToCopy);

                _overflowCount -= bytesToCopy;
                _overflowOffset += bytesToCopy;
                offset += bytesToCopy;
                return bytesToCopy;
            }
            return 0;
        }

        /// <summary>
        ///     Gets or sets the position of the <see cref="FlacReader" /> in bytes.
        /// </summary>
        public override long Position
        {
            get
            {
                lock (_bufferLock)
                    return _position;
            }
            set
            {
                if (!CanSeek)
                    return;
                lock (_bufferLock)
                {
                    value = Math.Max(0, value);
                    value += WaveFormat.BlockAlign - 1; // align to high
                    value = Math.Min(value, Length);

                    var sample = value / WaveFormat.BlockAlign;
                    if (_scan != null) // by scan
                    {
                        for (int i = value < _position ? 0 : _frameIndex; i < _scan.Frames.Length; i++)
                        {
                            var frame = _scan.Frames[i];
                            if (sample <= frame.SampleOffset)
                            {
                                _stream.Position = frame.StreamOffset;
                                _frameIndex = i;
                                if (_stream.Position >= _stream.Length - 16)
                                    throw new EndOfStreamException("Stream got EOF.");
                                _position = frame.SampleOffset * WaveFormat.BlockAlign;
                                _overflowCount = 0;
                                _overflowOffset = 0;
                                break;
                            }
                        }
                    }
                    else // by _seektable
                    {
                        FlacSeekPoint prevIndex = null;
                        for (int i = 0; i < _seekPoints.Length; i++)
                        {
                            FlacSeekPoint index = _seekPoints[i];
                            if ((ulong)sample <= index.Number || i == _seekPoints.Length - 1)
                            {
                                if (prevIndex != null)
                                    index = prevIndex;

                                _stream.Position = _dataStartPosition + (long)index.Offset;
                                if (_stream.Position >= _stream.Length - 16)
                                    throw new EndOfStreamException("Stream got EOF.");
                                var scan = new FlacPreScan(_stream);
                                _token = _token ?? new CancellationTokenSource();
                                var frames = scan.ScanThisShit(_streamInfo, _stream, _token.Token, (int)(sample - (long)index.Number));
                                if (frames.Length > 0)
                                {
                                    var frame = frames[0];
                                    _stream.Position = frame.StreamOffset;
                                    if (_stream.Position >= _stream.Length - 16)
                                        throw new EndOfStreamException("Stream got EOF.");
                                    _position = ((long)index.Number + frame.SampleOffset) * WaveFormat.BlockAlign;
                                }
                                _overflowCount = 0;
                                _overflowOffset = 0;
                                break;
                            }
                            prevIndex = index;
                        }
                    }
                }
            }
        }

        /// <summary>
        ///     Gets the length of the <see cref="FlacReader" /> in bytes.
        /// </summary>
        public override long Length
        {
            get { return (_scan != null ? _scan.TotalSamples : _streamInfo.TotalSamples) * WaveFormat.BlockAlign; }
        }

        /// <summary>
        ///     Disposes the <see cref="FlacReader" /> instance and disposes the underlying stream.
        /// </summary>
        public new void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Disposes the <see cref="FlacReader" /> instance and disposes the underlying stream.
        /// </summary>
        /// <param name="disposing">
        ///     True to release both managed and unmanaged resources; false to release only unmanaged
        ///     resources.
        /// </param>
        protected override void Dispose(bool disposing)
        {
            lock (_bufferLock)
            {
                if (_token != null)
                {
                    _token.Cancel();
                    _token.Dispose();
                    _token = null;
                }

                if (_frame != null)
                {
                    _frame.FreeBuffers();
                    _frame = null;
                }

                if (_stream != null)
                    _stream.Dispose();
            }
        }

        /// <summary>
        ///     Destructor which calls the <see cref="Dispose(bool)" /> method.
        /// </summary>
        ~FlacReader()
        {
            Dispose(false);
        }
    }
}