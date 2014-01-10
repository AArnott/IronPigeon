namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Net.Http;
	using System.Text;
	using System.Threading.Tasks;
	using Autofac;

	/// <summary>
	/// A base class for platform-specific Autofac modules
	/// to derive from to offer automatic registration of services.
	/// </summary>
	public class IronPigeonBaseModule : Module {
		/// <summary>
		/// Initializes a new instance of the <see cref="IronPigeonBaseModule"/> class.
		/// </summary>
		/// <remarks>
		/// This constructor is protected because we expect every platform
		/// to derive from this module and add the required platform-specific exports.
		/// </remarks>
		protected IronPigeonBaseModule() {
			this.DefaultHttpTimeout = IronPigeon.Providers.HttpClientWrapper.DefaultTimeoutInitValue;
		}

		/// <summary>
		/// Gets or sets a value indicating whether the hosting app
		/// requires short URIs that support non-HTTP(s) schemes.
		/// </summary>
		public bool RequireShortUrlsWithUnusualSchemes { get; set; }

		/// <summary>
		/// Gets or sets the default timeout for HttpClient instances that may be imported.
		/// </summary>
		public TimeSpan DefaultHttpTimeout { get; set; }

		/// <summary>
		/// Override to add registrations to the container.
		/// </summary>
		/// <param name="builder">The builder through which components can be
		/// registered.</param>
		/// <remarks>
		/// Note that the ContainerBuilder parameter is unique to this module.
		/// </remarks>
		protected override void Load(ContainerBuilder builder) {
			base.Load(builder);

			var tinyUrl = builder.RegisterType<Providers.TinyUrlShortener>()
				.WithMetadata("SupportsUnusualSchemes", true)
				.AsSelf()
				.PropertiesAutowired()
				.SingleInstance();
			var googleUrl = builder.RegisterType<Providers.GoogleUrlShortener>()
				.AsSelf()
				.PropertiesAutowired()
				.SingleInstance();
			if (this.RequireShortUrlsWithUnusualSchemes) {
				tinyUrl.As<IUrlShortener>();
			} else {
				googleUrl.As<IUrlShortener>();
			}

			builder.RegisterType<Providers.RelayCloudBlobStorageProvider>()
				.AsSelf()
				.As<ICloudBlobStorageProvider>()
				.As<IronPigeon.Relay.IEndpointInboxFactory>()
				.PropertiesAutowired()
				.InstancePerLifetimeScope();

			builder.RegisterTypes(
				typeof(Providers.DirectEntryAddressBook),
				typeof(Providers.RelayServiceAddressBook),
				typeof(Providers.TwitterAddressBook),
				typeof(OwnEndpointServices))
				.AsSelf()
				.PropertiesAutowired()
				.InstancePerLifetimeScope();

			builder.Register<HttpClient>(c => new HttpClient(c.ResolveOptional<HttpMessageHandler>() ?? new HttpClientHandler()) {
				Timeout = this.DefaultHttpTimeout,
			});
		}
	}
}
