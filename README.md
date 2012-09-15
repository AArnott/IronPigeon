IronPigeon
==========

*IronPigeon is a decentralized communication protocol that provides high
confidentiality and authenticity for the messages.*

Messages are signed for authenticity, encrypted for confidentiality,
and transmitted indirectly so that eavesdroppers find it difficult or
impossible to establish *whether* two parties have even communicated,
*what* was communicated or *how much* was communicated.

Installing IronPigeon
---------------------

The recommended way to acquire the binary is via the
[IronPigeon][1] NuGet package.

For email-like message exchange, the [IronPigeon.Dart][2]
NuGet package is recommended.

Establishing a communications channel
-------------------------------------

To send a message via IronPigeon, a pair of endpoints must exist.
Endpoints have both public and private components, containing the public
and private cryptographic key pairs respectively.  When party *A* shares its
public endpoint with party *B*, party *B* can send party *A* messages.
When two parties each create their own endpoints and exchange their public
components, the two parties may communicate securely.

### Creating an endpoint
The following code creates an endpoint with asymmetric keys of minimal length.

	var cryptoProvider = new DesktopCryptoProvider(SecurityLevel.Minimum);
	OwnEndpoint ownEndpoint = await OwnEndpoint.CreateAsync(cryptoProvider);

An `OwnEndpoint` instance includes the private keys required for receiving
messages at that endpoint.  It is therefore usually advisable to persist the
private keys after creating the endpoint so that messages can be received
in a subsequent session of the application.

	using (var stream = File.OpenWrite("user private endpoint file")) {
		await ownEndpoint.SaveAsync(stream);
	}

### Blob storage

IronPigeon requires a service that can store blobs that are publicly
accessible via a returned URL.  This service implements the 
`ICloudBlobStorageProvider` interface.  IronPigeon comes with two
implementations of this interface: `RelayCloudBlobStorageProvider` and
`AzureBlobStorage` (the latter only being available in desktop apps).
The former implementation can be instantiated with a URL to the service.

	var blobStorage = new RelayCloudBlobStorageProvider(new Uri(ConfigurationManager.ConnectionStrings["RelayBlobService"].ConnectionString));

### Creating a channel

A channel provides the message send and receive methods.  It can be created
by passing in the objects created previously.

	var channel = new Channel(blobStorage, cryptoProvider, ownEndpoint);

Note that when running in a Windows 8 app, a more specialized `WinRTChannel`
class can be instantiated instead, which adds Windows push notification
service support for receiving messages.

### Publishing an endpoint so others may send messages to it

The `OwnEndpoint.PublicEndpoint` property contains the public data that a
remote party needs to send messages to your endpoint.  The public endpoint
may be transmitted to interested remote parties by any means.  One method is
to publish the public endpoint data to a web server and share the URL to that
data with the remote party.  Publishing the endpoint and obtaining the URL can
be done with a single line:

	Uri shareableAddress = await channel.PublishAddressBookEntryAsync();

A remote party can turn this URL back into a public `Endpoint` like this:

	var addressBook = new DirectEntryAddressBook(cryptoProvider);
	Endpoint friend = await addressBook.LookupAsync(shareableAddress);

Sending and receiving messages
------------------------------

### Sending a message

A simple text message can be sent to a remote party:

	var payload = new Payload(Encoding.UTF8.GetBytes("hello, world"), "text/plain");
	var recipients = new[] { friend };
	var expiration = DateTime.UtcNow + TimeSpan.FromMinutes(5);
	await channel.PostAsync(payload, recipients, expiration);

### Receiving messages

Checking the cloud inbox for inbound messages to your endpoint can be done in
any of a few ways.  

This line will check for any incoming messages and immediately return with
the set of messages that were waiting.  If no messages were waiting, an empty
set is returned:

	var incoming = await channel.ReceiveAsync();

To receive long-poll style push notification of any incoming messages, add a
`longPoll: true` parameter.  This will cause the receive operation to complete
only when a message is actually received.  In this way, you can use an
asynchronous loop to continuously receive and process messages as soon as they
arrive.

	var incoming = await channel.ReceiveAsync(longPoll: true);

Either way, processing the incoming messages is simple:

	foreach (var payload in incoming) {
		var message = Encoding.UTF8.GetString(payload.Content);
		Console.WriteLine(message);
	}

Finally, if you're building a Windows 8 app, you can employ the 
Windows Push Notification service and avoid using a long poll connection
yourself, allowing your application to receiving notifications even when it is
not running, or when the computer is in a low power state:

	WinRTChannel channel; // initialized above
	await channel.RegisterPushNotificationChannelAsync(...);

Email-like communications
-------------------------

If the messages to exchange resemble emails, consider using Dart as the
message format.  This is facilitated by the `PostalService` and `Message`
types:

	var postalService = new PostalService(channel);
	var message = new Message(ownEndpoint, recipients, "subject", "body");
	await postalService.PostAsync(message);
	var incoming = await postalService.ReceiveAsync(longPoll: true|false);

[1]: http://nuget.org/packages/IronPigeon      "IronPigeon NuGet package"
[2]: http://nuget.org/packages/IronPigeon.Dart "IronPigeon.Dart NuGet package"

