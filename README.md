IronPigeon
==========

*IronPigeon is a decentralized communication protocol that provides high
confidentiality and authenticity for the messages.*

Messages are signed for authenticity, encrypted for confidentiality,
and transmitted indirectly so that eavesdroppers find it difficult or
impossible to establish *whether* two parties have even communicated,
*what* was communicated or *how much* was communicated.

This project includes libraries that implement the protocol and a message
relay web service project that provides the cloud component necessary for
passing messages.

See the end of this file for instructions on contacting the author using
the IronPigeon protocol.

Installing IronPigeon
---------------------

The recommended way to acquire the binary is via the
[IronPigeon][1] NuGet package.

For email-like message exchange, the [IronPigeon.Dart][2]
NuGet package is recommended.

Hosting IronPigeon
------------------

### Composition

There are a small collection of objects that work together to provide the
functions required to send and receive messages.  These objects can be
manually and individually instantiated, and properties set on them to point
to the other objects you've created.  Or you can rely on MEF to do this work
for you.

When targeting .NET 4.0, use MEF as it is found in the framework under the
`System.ComponentModel.Composition` namespace.  
When targeting .NET 4.5 or Windows 8, you should use the MEF "v2" found in the
[Microsoft.Composition][3] NuGet package.  The code below assumes you're using
the MEF framework from NuGet.  Most fundamental IronPigeon services are in the
core IronPigeon assembly, so we add that entire assembly to the MEF catalog:

	var configuration = new ContainerConfiguration()
		.WithAssembly(typeof(Channel).Assembly);

	// When targeting desktop apps:
	configuration.WithPart(typeof(DesktopCryptoProvider));
	// When targeting WinRT apps:
	configuration.WithAssembly(typeof(WinRTChannel).Assembly);

According to standard MEF code, you'll need a container to begin using
instances of these services:

	var container = configuration.CreateContainer();

You will need to configure some of these services (setting properties, etc.)
and other services you'll call methods on to actually send and receive
messages.  You can acquire these services directly from the container using:

	var someService = container.GetExport<T>();

The preferred approach however is that you define your own MEF part with
importing properties that will automatically be set to instances of these
services, like this:

	[Export]
	public class MyImports {
		[Import]
		public /*WinRT*/Channel Channel { get; set; }

		[Import]
		public RelayCloudBlobStorageProvider MessageRelayService { get; set; }

		[Import]
		public ICryptoProvider CryptoProvider { get; set; }

		[Import]
		public OwnEndpointServices OwnEndpointServices { get; set; }
	}

The latter approach will require that you add your part to the MEF catalog
prior to creating the container:

	configuration.WithPart(typeof(MyImports));

### Configuration

IronPigeon has three settings that it needs to work:

1. A URL to post public blobs to
2. A URL to request new inboxes from
3. The length of asymmetric keys to generate for newly created endpoints.

Assuming the context of running inside the `MyImports` class as defined above,
the following code demonstrates configuring the above listed settings.

	this.MessageRelayService.BlobPostUrl = new Uri("https://ironpigeon.azurewebsites.net/api/blob/");
	this.MessageRelayService.InboxServiceUrl = new Uri("https://ironpigeon.azurewebsites.net/Inbox/");
	this.CryptoProvider.ApplySecurityLevel(SecurityLevel.Minimum); // minimum is good for testing as keys generate faster

The URLs above are examples.  They can point to any compatible cloud service.

Establishing a communications channel
-------------------------------------

To send a message via IronPigeon, a pair of endpoints must exist.
Endpoints have both public and private components, containing the public
and private cryptographic key pairs respectively.  When party *A* shares its
public endpoint with party *B*, party *B* can send party *A* messages.
When two parties each create their own endpoints and exchange their public
components, the two parties may communicate securely.

### Creating an endpoint
The following code creates a new endpoint.

	this.Channel.Endpoint = await this.OwnEndpointServices.CreateAsync();

An `OwnEndpoint` instance includes the private keys required for receiving
messages at that endpoint.  It is therefore usually advisable to persist the
private keys after creating the endpoint so that messages can be received
in a subsequent session of the application.

	using (var stream = File.OpenWrite("user private endpoint file")) {
		await this.Channel.Endpoint.SaveAsync(stream);
	}

### Publishing an endpoint so others may send messages to it

The `OwnEndpoint.PublicEndpoint` property contains the public data that a
remote party needs to send messages to your endpoint.  The public endpoint
may be transmitted to interested remote parties by any means.  One method is
to publish the public endpoint data to a web server and share the URL to that
data with the remote party.  Publishing the endpoint and obtaining the URL can
be done with a single line:

	Uri shareableAddress = await this.OwnEndpointServices.PublishAddressBookEntryAsync(this.Channel.Endpoint);

A remote party can turn this URL back into a public `Endpoint` like this:

	[Import]
	public DirectEntryAddressBook AddressBook { get; set; }

	Endpoint friend = await this.AddressBook.LookupAsync(shareableAddress);

Sending and receiving messages
------------------------------

### Sending a message

A simple text message can be sent to a remote party:

	var payload = new Payload(Encoding.UTF8.GetBytes("hello, world"), "text/plain");
	var recipients = new[] { friend };
	var expiration = DateTime.UtcNow + TimeSpan.FromMinutes(5);
	await this.Channel.PostAsync(payload, recipients, expiration);

### Receiving messages

Checking the cloud inbox for inbound messages to your endpoint can be done in
any of a few ways.  

This line will check for any incoming messages and immediately return with
the set of messages that were waiting.  If no messages were waiting, an empty
set is returned:

	var incoming = await this.Channel.ReceiveAsync();

To receive long-poll style push notification of any incoming messages, add a
`longPoll: true` parameter.  This will cause the receive operation to complete
only when a message is actually received.  In this way, you can use an
asynchronous loop to continuously receive and process messages as soon as they
arrive.

	var incoming = await this.Channel.ReceiveAsync(longPoll: true);

Either way, processing the incoming messages is simple:

	foreach (var payload in incoming) {
		var message = Encoding.UTF8.GetString(payload.Content);
		Console.WriteLine(message);
	}

Finally, if you're building a Windows 8 app, you can employ the 
Windows Push Notification service and avoid using a long poll connection
yourself, allowing your application to receive notifications even when it is
not running, or when the computer is in a low power state:

	[Import]
	public WinRTChannel Channel { get; set; }

	await this.Channel.RegisterPushNotificationChannelAsync(...);

Email-like communications
-------------------------

If the messages to exchange resemble emails, consider using Dart as the
message format.  This is facilitated by the `PostalService` and `Message`
types:

	configuration.WithAssembly(typeof(PostalService).Assembly);

	[Import]
	public PostalService PostalService { get; set; }

	var message = new Message(ownEndpoint, recipients, "subject", "body");
	await this.PostalService.PostAsync(message);
	var incoming = await this.PostalService.ReceiveAsync(longPoll: true|false);

Contact the author
==================

To contact the author using the IronPigeon protocol follow these steps:

1. Clone this project.
2. Open the IronPigeon.sln in Visual Studio 2013.
3. Set the Clients\WpfChatroom project as the startup project.
4. Press F5.
5. Create your own endpoint and save it to disk so you can open it next time.
6. Click the "Chat with author" button.
7. Send me a message. 

I may reply right away. But I may reply in a day or two. Check back occasionally
by re-launching the sample and opening the same endpoint file you used to send
me the message.

[1]: http://nuget.org/packages/IronPigeon            "IronPigeon NuGet package"
[2]: http://nuget.org/packages/IronPigeon.Dart       "IronPigeon.Dart NuGet package"
[3]: http://nuget.org/packages/Microsoft.Composition "Microsoft.Composition NuGet package"
