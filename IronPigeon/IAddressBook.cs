namespace IronPigeon {
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// Retrieves contacts from some public store.
	/// </summary>
	/// <remarks>
	/// This interface does not describe a method for publishing to an address book because
	/// each address book may have different authentication requirements.
	/// </remarks>
	public interface IAddressBook {
		/// <summary>
		/// Retrieves a contact with some user supplied identifier.
		/// </summary>
		/// <param name="identifier">The user-supplied identifier for the contact.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <returns>A task whose result is the contact.</returns>
		/// <exception cref="KeyNotFoundException">Faults the task if no contact can be found for the given identifier.</exception>
		Task<AddressBookEntry> LookupAsync(string identifier, CancellationToken cancellationToken = default(CancellationToken));
	}
}
