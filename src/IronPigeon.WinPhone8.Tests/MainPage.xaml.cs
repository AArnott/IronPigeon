namespace IronPigeon.WinPhone8.Tests {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Net;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Navigation;
	using Microsoft.Phone.Controls;
	using Microsoft.Phone.Shell;
	using IronPigeon.WinPhone8.Tests.Resources;
	using System.Threading;
	using Microsoft.VisualStudio.TestPlatform.Core;
	using vstest_executionengine_platformbridge;
	using Microsoft.VisualStudio.TestPlatform.TestExecutor;
	using System.Reflection;

	public partial class MainPage : PhoneApplicationPage {
		public MainPage() {
			InitializeComponent();

			var wrapper = new TestExecutorServiceWrapper();
			new Thread(new ServiceMain((param0, param1) => wrapper.SendMessage((ContractName)param0, param1)).Run).Start();
		}
	}
}