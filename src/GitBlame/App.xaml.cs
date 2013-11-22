using System;
using System.Globalization;
using System.IO;
using System.Windows;

namespace GitBlame
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		public App()
		{
			m_app = new AppModel();
		}

		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);

			AppDomain.CurrentDomain.UnhandledException += (s, ea) => MessageBox.Show(ea.ExceptionObject.ToString());

			MainWindowModel mainWindowModel = m_app.MainWindow;

			string filePath = e.Args.Length >= 1 ? e.Args[0] : null;
			if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
			{
				MessageBox.Show("Usage: GitBlame file-path [line-number]", "GitBlame", MessageBoxButton.OK, MessageBoxImage.Error);
				Shutdown(1);
			}
			else
			{
				mainWindowModel.FilePath = filePath;
			}

			int lineNumber;
			if (e.Args.Length >= 2 && int.TryParse(e.Args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out lineNumber))
				mainWindowModel.LineNumber = lineNumber;

			Window window = new MainWindow(mainWindowModel);
			window.Show();
		}

		readonly AppModel m_app;
	}
}
