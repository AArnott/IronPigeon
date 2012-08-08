namespace IronPigeon.Tests.Mocks {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;

	internal class AddressBookMock : AddressBook {
		public override Task<Endpoint> LookupAsync(string identifier, CancellationToken cancellationToken = default(CancellationToken)) {
			throw new NotImplementedException();
		}

		public new Endpoint ExtractEndpoint(AddressBookEntry entry) {
			return base.ExtractEndpoint(entry);
		}
	}
}
