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
			var position = m_model.Position;
			if (position != null)
			{
				BlameResult blame = GitWrapper.GetBlameOutput(position.FilePath);
				Blame.SetBlameResult(blame, position.LineNumber ?? 1);
			}
		}

		private void OpenCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			OpenFileDialog dialog = new OpenFileDialog
			{
				InitialDirectory = m_model.Position == null ? null : Path.GetDirectoryName(m_model.Position.FilePath),
			};

			if (dialog.ShowDialog().GetValueOrDefault())
			{
				m_model.Position = new BlamePositionModel(dialog.FileName);
				RunBlame();
			}
		}

		private void OnBlamePrevious(object sender, ExecutedRoutedEventArgs e)
		{
			BlamePreviousModel blamePrevious = (BlamePreviousModel) e.Parameter;
			if (m_model.Position != null && blamePrevious != null)
			{
				string repoPath = GitWrapper.GetRepositoryPath(m_model.Position.FilePath);
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

		private void OnApplyUpdate(object sender, ExecutedRoutedEventArgs e)
		{
			string path = e.Parameter as string;
			if (path != null)
			{
				try
				{
					string arguments = m_model.Position == null ? null : "\"" + m_model.Position.FilePath + "\" " + (Blame.TopLineNumber.HasValue ? Blame.TopLineNumber.Value.ToString() : "");
					Process.Start(path, arguments);
					Application.Current.Shutdown(0);
				}
				catch (Win32Exception)
				{
				}
			}
		}

		readonly MainWindowModel m_model;
	}
}
