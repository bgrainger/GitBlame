using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using GitBlame.Analytics;
using GitBlame.ViewModels;

namespace GitBlame
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		public App()
		{
			m_analyticsClient = new GoogleAnalyticsClient("UA-25641987-2", "GitBlame", new GoogleAnalyticsStatisticsProvider());

			AppDomain.CurrentDomain.UnhandledException += (s, ea) =>
			{
				m_analyticsClient.SubmitExceptionAsync((Exception) ea.ExceptionObject, true);
				MessageBox.Show(ea.ExceptionObject.ToString());
			};

			m_app = new AppModel();
		}

		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);

			MainWindowModel mainWindowModel = m_app.MainWindow;

			string filePath = e.Args.Length >= 1 ? e.Args[0] : null;
			if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
			{
				mainWindowModel.FilePath = filePath;

				int lineNumber;
				if (e.Args.Length >= 2 && int.TryParse(e.Args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out lineNumber))
					mainWindowModel.LineNumber = lineNumber;
			}

			Task ignored = m_analyticsClient.SubmitAppViewAsync("MainWindow");

			Window window = new MainWindow(mainWindowModel);
			window.Show();
		}

		protected override void OnExit(ExitEventArgs e)
		{
			m_analyticsClient.SubmitSessionEndAsync();

			base.OnExit(e);
		}

		readonly AppModel m_app;
		readonly GoogleAnalyticsClient m_analyticsClient;
	}
}
