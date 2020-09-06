// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft;

    /// <summary>
    /// A stream that tracks bytes transferred over some underlying stream.
    /// </summary>
    internal class StreamWithProgress : Stream
    {
        /// <summary>
        /// The stream to read from or write to.
        /// </summary>
        private readonly Stream inner;

        /// <summary>
        /// The callback for reporting number of bytes transferred.
        /// </summary>
        private readonly IProgress<long>? progress;

        /// <summary>
        /// A value indicating whether to leave <see cref="inner"/> open when this instance is disposed of.
        /// </summary>
        private readonly bool leaveOpen;

        /// <summary>
        /// A value indicating whether we're locked into read mode, write mode, or indeterminate.
        /// </summary>
        private bool? readMode;

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamWithProgress"/> class.
        /// </summary>
        /// <param name="inner">The stream to read from or write to when this new stream is accessed. Whether this is a read or write stream depends on the first action taken on this wrapper.</param>
        /// <param name="bytesTransferredProgress">The receiver of progress updates.</param>
        /// <param name="leaveOpen"><c>true</c> to leave <paramref name="inner"/> open when this instance is disposed; <c>false</c> to dispose of <paramref name="inner"/> when this instance is disposed.</param>
        internal StreamWithProgress(Stream inner, IProgress<long>? bytesTransferredProgress, bool leaveOpen = false)
        {
            Requires.NotNull(inner, nameof(inner));

            this.inner = inner;
            this.progress = bytesTransferredProgress;
            this.leaveOpen = leaveOpen;
        }

        /// <inheritdoc/>
        public override bool CanRead => this.readMode != false && this.inner.CanRead;

        /// <inheritdoc/>
        public override bool CanSeek => false;

        /// <inheritdoc/>
        public override bool CanWrite => this.readMode != true && this.inner.CanWrite;

        /// <inheritdoc/>
        public override long Length => this.inner.Length;

        /// <inheritdoc/>
        public override long Position
        {
            get => this.inner.Position;
            set => throw new NotSupportedException();
        }

        /// <summary>
        /// Gets the bytes transferred over this stream.
        /// </summary>
        internal long BytesTransferred { get; private set; }

        /// <inheritdoc/>
        public override void Flush() => throw new NotSupportedException();

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count)
        {
            this.StartReadOperation();
            int bytesRead = this.inner.Read(buffer, offset, count);
            this.ReportProgress(bytesRead);
            return bytesRead;
        }

        /// <inheritdoc/>
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            this.StartReadOperation();
            int bytesRead = await this.inner.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
            this.ReportProgress(bytesRead);
            return bytesRead;
        }

        /// <inheritdoc/>
        public override int ReadByte()
        {
            this.StartReadOperation();
            int value = base.ReadByte();
            if (value != -1)
            {
                this.ReportProgress(1);
            }

            return value;
        }

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc/>
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count)
        {
            this.StartWriteOperation();
            this.inner.Write(buffer, offset, count);
            this.ReportProgress(count);
        }

        /// <inheritdoc/>
        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            this.StartWriteOperation();
            await this.inner.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
            this.ReportProgress(count);
        }

        /// <inheritdoc/>
        public override void WriteByte(byte value)
        {
            this.StartWriteOperation();
            this.inner.WriteByte(value);
            this.ReportProgress(1);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!this.leaveOpen)
                {
                    this.inner.Dispose();
                }
            }

            base.Dispose(disposing);
        }

        private void StartReadOperation()
        {
            Verify.Operation(this.readMode != false, "This stream is in write mode.");
            this.readMode = true;
        }

        private void StartWriteOperation()
        {
            Verify.Operation(this.readMode != true, "This stream is in read mode.");
            this.readMode = false;
        }

        /// <summary>
        /// Reports progress for the next segment of bytes transferred.
        /// </summary>
        /// <param name="bytesJustRead">The number of bytes transferred in the last operation.</param>
        private void ReportProgress(int bytesJustRead)
        {
            this.BytesTransferred += bytesJustRead;
            this.progress?.Report(this.BytesTransferred);
        }
    }
}
