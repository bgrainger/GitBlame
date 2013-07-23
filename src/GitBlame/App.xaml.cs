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
		public string FilePath { get; set; }

		public int? LineNumber { get; set; }

		protected override void OnStartup(StartupEventArgs e)
		{
			AppDomain.CurrentDomain.UnhandledException += (s, ea) => MessageBox.Show(ea.ExceptionObject.ToString());

			string filePath = e.Args.Length >= 1 ? e.Args[0] : null;
			if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
			{
				MessageBox.Show("Usage: GitBlame file-path [line-number]", "GitBlame", MessageBoxButton.OK, MessageBoxImage.Error);
				Shutdown(1);
			}
			else
			{
				FilePath = filePath;
			}

			int lineNumber;
			if (e.Args.Length >= 2 && int.TryParse(e.Args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out lineNumber))
				LineNumber = lineNumber;

			base.OnStartup(e);
		}
	}
}
