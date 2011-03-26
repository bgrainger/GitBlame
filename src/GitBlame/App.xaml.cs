
using System.IO;
using System.Windows;

namespace GitBlame
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		public string FilePath { get; private set; }

		protected override void OnStartup(StartupEventArgs e)
		{
			string filePath = e.Args.Length == 1 ? e.Args[0] : null;
			if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
			{
				MessageBox.Show("Usage: GitBlame file-path", "GitBlame", MessageBoxButton.OK, MessageBoxImage.Error);
				Shutdown(1);
			}
			else
			{
				FilePath = filePath;
			}

			base.OnStartup(e);
		}
	}
}
