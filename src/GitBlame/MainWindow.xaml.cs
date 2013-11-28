using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Input;
using GitBlame.Models;
using GitBlame.Utility;
using GitBlame.ViewModels;
using Microsoft.Win32;
using ReactiveUI;

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

			model.WhenAny(x => x.Position, x => x.Value).Where(x => x != null).Subscribe(RunBlame);
		}

		private void RunBlame(BlamePositionModel position)
		{
			BlameResult blame = GitWrapper.GetBlameOutput(position.RepoPath, position.FileName, position.CommitId);
			Blame.SetBlameResult(blame, position.LineNumber ?? 1);
		}

		private void OnOpen(object sender, ExecutedRoutedEventArgs e)
		{
			var position = m_model.Position;
			OpenFileDialog dialog = new OpenFileDialog
			{
				InitialDirectory = position == null ? null : Path.GetDirectoryName(position.GetFilePath()),
			};

			if (dialog.ShowDialog().GetValueOrDefault())
				m_model.NavigateTo(new BlamePositionModel(dialog.FileName));
		}

		void OnBrowseBack(object sender, ExecutedRoutedEventArgs e)
		{
			m_model.NavigateBack();
		}

		void OnBrowseForward(object sender, ExecutedRoutedEventArgs e)
		{
			m_model.NavigateForward();
		}

		private void OnBlamePrevious(object sender, ExecutedRoutedEventArgs e)
		{
			BlamePreviousModel blamePrevious = (BlamePreviousModel) e.Parameter;
			if (m_model.Position != null && blamePrevious != null)
			{
				m_model.NavigateTo(new BlamePositionModel(m_model.Position.RepoPath, blamePrevious.FileName)
				{
					CommitId = blamePrevious.CommitId,
					LineNumber = blamePrevious.LineNumber,
				});
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
					var position = m_model.Position;
					string arguments = position == null ? null : "/restart \"{0}\" \"{1}\" {2} {3}".FormatInvariant(position.RepoPath, position.FileName, position.CommitId ?? "null", Blame.TopLineNumber ?? 1);
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
