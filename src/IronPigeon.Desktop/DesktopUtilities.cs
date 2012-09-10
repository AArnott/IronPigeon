namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Text.RegularExpressions;
	using System.Threading.Tasks;

	public static class DesktopUtilities {
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
	}
}
