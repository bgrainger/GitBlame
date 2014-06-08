using System;
using System.Windows;
using GitBlame.Utility;

namespace GitBlame
{
	public partial class GoToLine : Window
	{
		public GoToLine()
		{
			WindowStartupLocation = WindowStartupLocation.CenterScreen;
			InitializeComponent();
		}

		protected override void OnActivated(EventArgs e)
		{
			base.OnActivated(e);

			BlameControl blame = (BlameControl)DataContext;
			Caption.Content = "Line number (1–{0}):".FormatInvariant(blame.TotalLines);

			LineNumber.Text = blame.TopLineNumber.ToString();
			LineNumber.Focus();
			LineNumber.SelectAll();
		}

		private void OnGoToLine(object sender, RoutedEventArgs e)
		{
			BlameControl blame = (BlameControl)DataContext;
			
			int lineNumber;	
			if (int.TryParse(LineNumber.Text, out lineNumber) && 
				lineNumber > 0 && 
				lineNumber <= blame.TotalLines)
			{
				blame.GoToLineNumber(lineNumber);
				Close();
			}
		}
	}
}
