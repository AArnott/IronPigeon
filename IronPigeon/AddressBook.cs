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
		/// <returns>A task whose result is the contact.</returns>
		/// <exception cref="KeyNotFoundException">Faults the task if no contact can be found for the given identifier.</exception>
		public abstract Task<Endpoint> LookupAsync(string identifier, CancellationToken cancellationToken = default(CancellationToken));

		/// <summary>
		/// Deserializes an endpoint from an address book entry and validates that the signatures are correct.
		/// </summary>
		/// <param name="entry">The address book entry to deserialize and validate.</param>
		/// <returns>The deserialized endpoint.</returns>
		/// <exception cref="BadAddressBookEntryException">Thrown if the signatures are invalid.</exception>
		protected Endpoint ExtractEndpoint(AddressBookEntry entry) {
			Requires.NotNull(entry, "entry");
			Verify.Operation(this.CryptoServices != null, Strings.CryptoServicesRequired);

			var reader = new BinaryReader(new MemoryStream(entry.SerializedEndpoint));
			Endpoint endpoint;
			try {
				endpoint = Utilities.DeserializeDataContract<Endpoint>(reader);
			} catch (SerializationException ex) {
				throw new BadAddressBookEntryException(ex.Message, ex);
			}

			try {
				if (!this.CryptoServices.VerifySignature(endpoint.SigningKeyPublicMaterial, entry.SerializedEndpoint, entry.Signature)) {
					throw new BadAddressBookEntryException(Strings.AddressBookEntrySignatureDoesNotMatch);
				}
			} catch (Exception ex) { // all those platform-specific exceptions that aren't available to portable libraries.
				throw new BadAddressBookEntryException(Strings.AddressBookEntrySignatureDoesNotMatch, ex);
			}
			return endpoint;
		}
	}
}
