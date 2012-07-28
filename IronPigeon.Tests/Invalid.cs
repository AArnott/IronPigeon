namespace IronPigeon.Tests {
	using System;
	using System.IO;

	internal static class Invalid {
		private static readonly byte[] EmptyBuffer = new byte[0];

		internal static readonly byte[] Hash = EmptyBuffer;
		internal static readonly byte[] Key = EmptyBuffer;
		internal static readonly byte[] IV = EmptyBuffer;
		
		internal static readonly string ContentType = string.Empty;
		internal static readonly DateTime ExpirationUtc = DateTime.Now;
	}
}
