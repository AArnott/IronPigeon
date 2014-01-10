namespace IronPigeon {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using Autofac;

	/// <summary>
	/// An Autofac module for IronPigeon services.
	/// </summary>
	public class IronPigeonPlatformModule : IronPigeonBaseModule {
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

			builder.RegisterType<WinPhoneChannel>()
				.AsSelf()
				.As<Channel>()
				.PropertiesAutowired()
				.InstancePerLifetimeScope();
			builder.RegisterType<Providers.WinPhone8CryptoProvider>()
				.As<ICryptoProvider>()
				.PropertiesAutowired()
				.InstancePerLifetimeScope();
		}
	}
}
