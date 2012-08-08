namespace IronPigeon.Tests {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Runtime.Serialization;
	using System.Text;
	using System.Threading.Tasks;
	using NUnit.Framework;

	[TestFixture]
	public class AddressBookEntryTest {
		[Test]
		public void Ctor() {
			var entry = new AddressBookEntry();
			Assert.That(entry.SerializedEndpoint, Is.Null);
			Assert.That(entry.Signature, Is.Null);
		}

		[Test]
		public void PropertySetGet() {
			var serializedEndpoint = new byte[] { 0x1, 0x2 };
			var signature = new byte[] { 0x3, 0x4 };
			var entry = new AddressBookEntry() {
				SerializedEndpoint = serializedEndpoint,
				Signature = signature,
			};
			Assert.That(entry.SerializedEndpoint, Is.EqualTo(serializedEndpoint));
			Assert.That(entry.Signature, Is.EqualTo(signature));
		}

		[Test]
		public void Serializability() {
			var entry = new AddressBookEntry() {
				SerializedEndpoint = new byte[] { 0x1, 0x2 },
				Signature = new byte[] { 0x3, 0x4 },
			};

			var ms = new MemoryStream();
			var serializer = new DataContractSerializer(typeof(AddressBookEntry));
			serializer.WriteObject(ms, entry);
			ms.Position = 0;
			var deserializedEntry = (AddressBookEntry)serializer.ReadObject(ms);

			Assert.That(deserializedEntry.SerializedEndpoint, Is.EqualTo(entry.SerializedEndpoint));
			Assert.That(deserializedEntry.Signature, Is.EqualTo(entry.Signature));
		}
	}
}
