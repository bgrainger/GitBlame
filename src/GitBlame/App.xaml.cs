// #define DEVELOPMENT

using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime;
using System.Windows;
using Common.Logging;
using GitBlame.Analytics;
using GitBlame.Utility;
using GitBlame.ViewModels;
using ReactiveUI;

namespace GitBlame
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		public App()
		{
			// this doesn't appear to be configured automatically for WPF in .NET Core 3.0
			RxApp.MainThreadScheduler = new DispatcherScheduler();

			string profilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"GitBlame\Profile");
			Directory.CreateDirectory(profilePath);
			ProfileOptimization.SetProfileRoot(profilePath);
			ProfileOptimization.StartProfile("Startup");

			Log.DebugFormat("Starting new application; version {0}.", Assembly.GetExecutingAssembly().GetName().Version);

			m_analyticsClient = new GoogleAnalyticsClient("UA-25641987-2", "GitBlame", new GoogleAnalyticsStatisticsProvider());

			AppDomain.CurrentDomain.UnhandledException += (s, ea) =>
			{
				if (ea.ExceptionObject is Exception exception)
				{
					Log.FatalFormat("Unhandled Exception: {0} {1}", exception, exception.GetType(), exception.Message);
#if !DEVELOPMENT
					m_analyticsClient.SubmitExceptionAsync(exception, true);
#endif
				}
				else
				{
					Log.FatalFormat("Unhandled Error: {0}", ea.ExceptionObject);
				}
			};

			m_app = new AppModel();
		}

		protected override async void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);

			var sessionStart = m_analyticsClient.SubmitSessionStartAsync();

			foreach (var arg in e.Args)
				Log.InfoFormat("Command-line arg: {0}", arg);

			MainWindowModel mainWindowModel = m_app.MainWindow;
			BlamePositionModel? position = null;

			string? filePath = e.Args.Length >= 1 ? e.Args[0] : null;
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

			await sessionStart.ConfigureAwait(true);
			await m_analyticsClient.SubmitAppViewAsync("MainWindow").ConfigureAwait(true);
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
