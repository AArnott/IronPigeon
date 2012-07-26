namespace IronPigeon.Tests {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;

	internal static class Constants {
		internal const string ValidContentType = "some type";
		internal static readonly byte[] NonEmptyBuffer = new byte[1];
		internal static readonly byte[] EmptyBuffer = new byte[0];
		internal static readonly Uri ValidLocation = new Uri("http://localhost/");
		internal static readonly DateTime ValidExpirationUtc = DateTime.UtcNow;
		internal static readonly Contact ValidRecipient = new Contact();
		internal static readonly Contact[] OneValidRecipient = new Contact[] { ValidRecipient };
		internal static readonly Contact[] EmptyRecipients = new Contact[0];
		internal static readonly Stream ValidStream = new MemoryStream();
		internal static readonly Message ValidMessage = new Message(ValidStream, ValidExpirationUtc, ValidContentType);
	}
}
