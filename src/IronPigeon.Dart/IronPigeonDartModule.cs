namespace IronPigeon.Dart {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using Autofac;

	/// <summary>
	/// An Autofac module for Dart services.
	/// </summary>
	public class IronPigeonDartModule : Module {
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

			builder.RegisterType<PostalService>()
				.AsSelf()
				.PropertiesAutowired()
				.InstancePerLifetimeScope();
		}
	}
}
