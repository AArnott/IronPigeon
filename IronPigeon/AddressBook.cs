namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Net.Http;
	using System.Runtime.Serialization;
	using System.Threading;
	using System.Threading.Tasks;
	using Microsoft;

	/// <summary>
	/// Retrieves contacts from some public store.
	/// </summary>
	/// <remarks>
	/// This class does not describe a method for publishing to an address book because
	/// each address book may have different authentication requirements.
	/// </remarks>
	public abstract class AddressBook {
		/// <summary>
		/// Gets or sets the cryptographic services provider.
		/// </summary>
		public ICryptoProvider CryptoServices { get; set; }

		/// <summary>
		/// Retrieves a contact with some user supplied identifier.
		/// </summary>
		/// <param name="identifier">The user-supplied identifier for the contact.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <returns>
		/// A task whose result is the contact, or null if no match is found.
		/// </returns>
		/// <exception cref="BadAddressBookEntryException">Thrown when a validation error occurs while reading the address book entry.</exception>
		public abstract Task<Endpoint> LookupAsync(string identifier, CancellationToken cancellationToken = default(CancellationToken));
	}
}
