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

		void OnBlamePrevious(object sender, ExecutedRoutedEventArgs e)
		{
			string filePath = ((App) Application.Current).FilePath;
			Commit commit = (Commit) e.Parameter;
			if (filePath != null && commit.PreviousCommitId != null)
			{
				string repoPath = GitWrapper.GetRepositoryPath(filePath);
				BlameResult blame = GitWrapper.GetBlameOutput(repoPath, commit.PreviousFileName, commit.PreviousCommitId);
				Blame.SetBlameResult(blame);
			}				
		}

		void OnCanBlamePrevious(object sender, CanExecuteRoutedEventArgs e)
		{
			Commit commit = e.Parameter as Commit;
			e.CanExecute = commit != null && commit.PreviousCommitId != null;
			e.Handled = true;
		}
	}
}
