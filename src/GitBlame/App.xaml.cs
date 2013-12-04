using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using Bugsense.WPF;
using Common.Logging;
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
			Log.DebugFormat("Starting new application; version {0}.", Assembly.GetExecutingAssembly().GetName().Version);

			m_analyticsClient = new GoogleAnalyticsClient("UA-25641987-2", "GitBlame", new GoogleAnalyticsStatisticsProvider());
			BugSense.Init("w8cfcffb");

			AppDomain.CurrentDomain.UnhandledException += (s, ea) =>
			{
				m_analyticsClient.SubmitExceptionAsync((Exception) ea.ExceptionObject, true);
				MessageBox.Show(ea.ExceptionObject.ToString());
			};

			m_app = new AppModel();
		}

		protected override async void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);

			MainWindowModel mainWindowModel = m_app.MainWindow;
			BlamePositionModel position = null;

			string filePath = e.Args.Length >= 1 ? e.Args[0] : null;
			if (filePath == "/restart" && e.Args.Length == 5)
			{
				position = new BlamePositionModel(e.Args[1], e.Args[2])
				{
					CommitId = e.Args[3] == "null" ? null : e.Args[3],
					LineNumber = int.Parse(e.Args[4]),
				};
			}
			else if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
			{
				position = new BlamePositionModel(filePath);

				int lineNumber;
				if (e.Args.Length >= 2 && int.TryParse(e.Args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out lineNumber))
					position.LineNumber = lineNumber;
			}
			mainWindowModel.NavigateTo(position);

			Window window = new MainWindow(mainWindowModel);
			window.Show();

			await m_analyticsClient.SubmitAppViewAsync("MainWindow");
		}

		protected override void OnExit(ExitEventArgs e)
		{
			m_analyticsClient.SubmitSessionEndAsync().Wait();
			Log.Debug("Shutting down application.");
			base.OnExit(e);
		}

		static readonly ILog Log = LogManager.GetLogger("App");

		readonly AppModel m_app;
		readonly GoogleAnalyticsClient m_analyticsClient;
	}
}
