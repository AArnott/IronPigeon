namespace IronPigeon {
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// Stores and retrieves contacts from some public store.
	/// </summary>
	public interface IAddressBook {
		/// <summary>
		/// Retrieves a contact with some user supplied identifier.
		/// </summary>
		/// <param name="identifier">The user-supplied identifier for the contact.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <returns>A task whose result is the contact.</returns>
		/// <exception cref="KeyNotFoundException">Faults the task if no contact can be found for the given identifier.</exception>
		Task<Endpoint> LookupAsync(string identifier, CancellationToken cancellationToken = default(CancellationToken));

		/// <summary>
		/// Publishes the specified contact for public retrieval.
		/// </summary>
		/// <param name="recipient">The contact to store.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <returns>A task whose completion signals a successfully published contact.</returns>
		Task PublishAsync(Endpoint recipient, CancellationToken cancellationToken = default(CancellationToken));
	}
}
