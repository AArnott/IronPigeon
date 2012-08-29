namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;

	/// <summary>
	/// A service that can shorten long URLs.
	/// </summary>
	public interface IUrlShortener {
		/// <summary>
		/// Shortens the specified long URL.
		/// </summary>
		/// <param name="longUrl">The long URL.</param>
		/// <returns>The short URL.</returns>
		Task<Uri> ShortenAsync(Uri longUrl);
	}
}
