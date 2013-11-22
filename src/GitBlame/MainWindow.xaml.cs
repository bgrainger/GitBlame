using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using GitBlame.Models;
using GitBlame.ViewModels;
using Microsoft.Win32;

namespace GitBlame
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow(MainWindowModel model)
		{
			DataContext = m_model = model;
			InitializeComponent();
			RunBlame();
		}

		private void RunBlame()
		{
			string filePath = m_model.FilePath;
			if (filePath != null)
			{
				BlameResult blame = GitWrapper.GetBlameOutput(filePath);
				Blame.SetBlameResult(blame, m_model.LineNumber ?? 1);
			}
		}

		private void OpenCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			OpenFileDialog dialog = new OpenFileDialog
			{
				InitialDirectory = Path.GetDirectoryName(m_model.FilePath),
			};

			if (dialog.ShowDialog().GetValueOrDefault())
			{
				m_model.FilePath = dialog.FileName;
				RunBlame();
			}
		}

		private void OnBlamePrevious(object sender, ExecutedRoutedEventArgs e)
		{
			string filePath = m_model.FilePath;
			BlamePreviousModel blamePrevious = (BlamePreviousModel) e.Parameter;
			if (filePath != null && blamePrevious != null)
			{
				string repoPath = GitWrapper.GetRepositoryPath(filePath);
				BlameResult blame = GitWrapper.GetBlameOutput(repoPath, blamePrevious.FileName, blamePrevious.CommitId);
				Blame.SetBlameResult(blame, blamePrevious.LineNumber);
			}				
		}

		private void OnCanBlamePrevious(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = e.Parameter is BlamePreviousModel;
			e.Handled = true;
		}

		private void OnViewAtGitHub(object sender, ExecutedRoutedEventArgs e)
		{
			Uri uri = e.Parameter as Uri;
			if (uri != null)
			{
				try
				{
					Process.Start(uri.AbsoluteUri);
				}
				catch (Win32Exception)
				{
				}
			}
		}

		private void OnCanViewAtGitHub(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = e.Parameter is Uri;
			e.Handled = true;
		}

		readonly MainWindowModel m_model;
	}
}
