
using System.Windows;
using GitBlame.Models;

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
			string filePath = @"D:\Projects\git\cache.h";
			BlameResult blame = GitWrapper.GetBlameOutput(filePath);
			Blame.SetBlameResult(blame);
		}
	}
}
