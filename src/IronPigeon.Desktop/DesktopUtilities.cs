namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Text.RegularExpressions;
	using System.Threading.Tasks;
	using Microsoft.WindowsAzure.Storage.Blob;
	using Microsoft.WindowsAzure.StorageClient;
	using Validation;

	/// <summary>
	/// Utility methods for desktop IronPigeon apps.
	/// </summary>
	public static class DesktopUtilities {
		/// <summary>
		/// The recommended length of a randomly generated string used to name uploaded blobs.
		/// </summary>
		internal const int BlobNameLength = 15;

		/// <summary>
		/// Determines whether the specified string constitutes a valid Azure blob container name.
		/// </summary>
		/// <param name="containerName">Name of the container.</param>
		/// <returns>
		///   <c>true</c> if the string is a valid container name; otherwise, <c>false</c>.
		/// </returns>
		public static bool IsValidBlobContainerName(string containerName) {
			if (containerName == null) {
				return false;
			}

			// Rule #1: can only contain (lowercase) letters, numbers and dashes.
			if (!Regex.IsMatch(containerName, @"^[a-z0-9\-]+$")) {
				return false;
			}

			// Rule #2: all dashes must be preceded and followed by a letter or number.
			if (containerName.StartsWith("-") || containerName.EndsWith("-") || containerName.Contains("--")) {
				return false;
			}

			// Rule #3: all lowercase.
			if (containerName.ToLowerInvariant() != containerName) {
				return false;
			}

			// Rule #4: length is 3-63
			if (containerName.Length < 3 || containerName.Length > 63) {
				return false;
			}

			return true;
		}

		/// <summary>
		/// Creates a blob container and sets its permission to public blobs, if the container does not already exist.
		/// </summary>
		/// <param name="container">The container to create.</param>
		/// <returns>
		/// A task whose result is <c>true</c> if the container did not exist before this method;
		///  <c>false</c> otherwise.
		/// </returns>
		public static async Task<bool> CreateContainerWithPublicBlobsIfNotExistAsync(this CloudBlobContainer container) {
			Requires.NotNull(container, "container");

			if (await container.CreateIfNotExistAsync()) {
				var permissions = new BlobContainerPermissions {
					PublicAccess = BlobContainerPublicAccessType.Blob,
				};
				await container.SetPermissionsAsync(permissions);
				return true;
			} else {
				return false;
			}
		}
	}
}
