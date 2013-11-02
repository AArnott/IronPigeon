namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using Validation;

	/// <summary>
	/// A wrapper of a readable stream that reports progress in terms of bytes read.
	/// </summary>
	internal class ReadStreamWithProgress : Stream {
		/// <summary>
		/// The stream to read from.
		/// </summary>
		private readonly Stream inner;

		/// <summary>
		/// The callback for reporting number of bytes read.
		/// </summary>
		private readonly IProgress<int> bytesReadProgress;

		/// <summary>
		/// The number of bytes read so far.
		/// </summary>
		private int totalBytesRead;

		/// <summary>
		/// The bytes read in the last read operation.
		/// </summary>
		private int lastBytesRead;

		/// <summary>
		/// Initializes a new instance of the <see cref="ReadStreamWithProgress"/> class.
		/// </summary>
		/// <param name="inner">The stream to read from when this new stream is read from.</param>
		/// <param name="bytesReadProgress">The receiver of progress updates.</param>
		internal ReadStreamWithProgress(Stream inner, IProgress<int> bytesReadProgress) {
			Requires.NotNull(inner, "inner");
			Requires.NotNull(bytesReadProgress, "bytesReadProgress");
			Requires.Argument(inner.CanRead, "inner", "Readable stream required.");

			this.inner = inner;
			this.bytesReadProgress = bytesReadProgress;
		}

		/// <inheritdoc/>
		public override bool CanRead {
			get { return true; }
		}

		/// <inheritdoc/>
		public override bool CanSeek {
			get { return false; }
		}

		/// <inheritdoc/>
		public override bool CanWrite {
			get { return false; }
		}

		/// <inheritdoc/>
		public override long Length {
			get { return this.inner.Length; }
		}

		/// <inheritdoc/>
		public override long Position {
			get { return this.inner.Position; }
			set { throw new NotSupportedException(); }
		}

		/// <inheritdoc/>
		public override void Flush() {
			throw new NotSupportedException();
		}

		/// <inheritdoc/>
		public override int Read(byte[] buffer, int offset, int count) {
			int bytesRead = this.inner.Read(buffer, offset, count);
			this.ReportProgress(bytesRead);
			return bytesRead;
		}

		/// <inheritdoc/>
		public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
			int bytesRead = await this.inner.ReadAsync(buffer, offset, count, cancellationToken);
			this.ReportProgress(bytesRead);
			return bytesRead;
		}

		/// <inheritdoc/>
		public override int ReadByte() {
			int value = base.ReadByte();
			if (value != -1) {
				this.ReportProgress(1);
			}

			return value;
		}

		/// <inheritdoc/>
		public override long Seek(long offset, SeekOrigin origin) {
			throw new NotSupportedException();
		}

		/// <inheritdoc/>
		public override void SetLength(long value) {
			throw new NotSupportedException();
		}

		/// <inheritdoc/>
		public override void Write(byte[] buffer, int offset, int count) {
			throw new NotSupportedException();
		}

		/// <inheritdoc/>
		protected override void Dispose(bool disposing) {
			if (disposing) {
				this.ReportProgress();
				this.inner.Dispose();
			}

			base.Dispose(disposing);
		}

		private void ReportProgress(int bytesJustRead = 0) {
			if (this.lastBytesRead > 0) {
				this.totalBytesRead += this.lastBytesRead;
				this.bytesReadProgress.Report(this.totalBytesRead);
			}

			this.lastBytesRead = bytesJustRead; // report this next time
		}
	}
}
