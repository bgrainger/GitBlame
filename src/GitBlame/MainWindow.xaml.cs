
using System.IO;
using System.Windows;
using System.Windows.Input;
using GitBlame.Models;
using Microsoft.Win32;

namespace GitBlame
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
			RunBlame();
		}

		private void RunBlame()
		{
			string filePath = ((App) Application.Current).FilePath;
			if (filePath != null)
			{
				BlameResult blame = GitWrapper.GetBlameOutput(filePath);
				Blame.SetBlameResult(blame);
			}
		}

		private void OpenCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			App app = (App) Application.Current;
			OpenFileDialog dialog = new OpenFileDialog
			{
				InitialDirectory = Path.GetDirectoryName(app.FilePath),
			};

			if (dialog.ShowDialog().GetValueOrDefault())
			{
				app.FilePath = dialog.FileName;
				RunBlame();
			}
		}
	}
}
