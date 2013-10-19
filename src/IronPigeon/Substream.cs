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
	/// Wraps an underlying stream with another that has a shorter length so that folks reading cannot read to the end.
	/// </summary>
	/// <remarks>
	/// The parent stream should not be repositioned while the substream is in use.
	/// </remarks>
	internal class Substream : Stream {
		private readonly Stream underlyingStream;
		private readonly long initialPosition;
		private readonly long length;
		private long positionRelativeToStart;

		public Substream(Stream underlyingStream, long length) {
			Requires.NotNull(underlyingStream, "underlyingStream");
			Requires.Range(length >= 0, "length");
			this.underlyingStream = underlyingStream;
			this.length = length;
			this.initialPosition = underlyingStream.CanSeek ? underlyingStream.Position : -1;
		}

		public override long Position {
			get {
				return this.underlyingStream.Position - this.initialPosition;
			}

			set {
				Requires.Range(value >= 0, "value");
				this.underlyingStream.Position = this.initialPosition + value;
				this.positionRelativeToStart = value;
			}
		}

		public override long Length {
			get { return this.length; }
		}

		public override bool CanRead {
			get { return this.underlyingStream.CanRead; }
		}

		public override bool CanWrite {
			get { return false; }
		}

		public override bool CanSeek {
			get { return this.underlyingStream.CanSeek; }
		}

		public override bool CanTimeout {
			get { return this.underlyingStream.CanTimeout; }
		}

		public override int ReadTimeout {
			get { return this.underlyingStream.ReadTimeout; }
			set { this.underlyingStream.ReadTimeout = value; }
		}

		public override int WriteTimeout {
			get { return this.underlyingStream.WriteTimeout; }
			set { this.underlyingStream.WriteTimeout = value; }
		}

		public override int Read(byte[] buffer, int offset, int count) {
			long bytesRemaining = this.length - this.positionRelativeToStart;
			int bytesRead = this.underlyingStream.Read(buffer, offset, Math.Min(count, (int)bytesRemaining));
			this.positionRelativeToStart += bytesRead;
			return bytesRead;
		}

		public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
			long bytesRemaining = this.length - this.positionRelativeToStart;
			int bytesRead = await this.underlyingStream.ReadAsync(buffer, offset, Math.Min(count, (int)bytesRemaining), cancellationToken);
			this.positionRelativeToStart += bytesRead;
			return bytesRead;
		}

		public override int ReadByte() {
			long bytesRemaining = this.length - this.positionRelativeToStart;
			if (bytesRemaining > 0) {
				int result = this.underlyingStream.ReadByte();
				this.positionRelativeToStart++;
				return result;
			} else {
				return -1;
			}
		}

		public override void Write(byte[] buffer, int offset, int count) {
			throw new NotSupportedException();
		}

		public override Task WriteAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken) {
			throw new NotSupportedException();
		}

		public override void WriteByte(byte value) {
			throw new NotSupportedException();
		}

		public override Task FlushAsync(CancellationToken cancellationToken) {
			throw new NotSupportedException();
		}

		public override void Flush() {
			throw new NotSupportedException();
		}

		public override void SetLength(long value) {
			throw new NotSupportedException();
		}

		public override long Seek(long offset, SeekOrigin origin) {
			// We could implement this if needed.
			throw new NotImplementedException();
		}

		protected override void Dispose(bool disposing) {
			// We don't dispose of the underlying stream.
			base.Dispose(disposing);
		}
	}
}
