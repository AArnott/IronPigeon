namespace IronPigeon {
	using System;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// A service that can upload arbitrary data streams to cloud storage
	/// such that it is publically retrievable.
	/// </summary>
	public interface ICloudBlobStorageProvider {
		/// <summary>
		/// Uploads a blob to public cloud storage.
		/// </summary>
		/// <param name="content">The blob's content.</param>
		/// <param name="expirationUtc">The date after which this blob should be deleted.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <returns>A task whose result is the URL by which the blob's content may be accessed.</returns>
		Task<Uri> UploadMessageAsync(Stream content, DateTime expirationUtc, CancellationToken cancellationToken = default(CancellationToken));
	}
}
