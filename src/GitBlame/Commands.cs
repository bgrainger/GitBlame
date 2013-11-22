using System.Windows.Input;

namespace GitBlame
{
	public static class Commands
	{
		public static RoutedCommand BlamePreviousCommand = new RoutedCommand();

		public static RoutedCommand ViewAtGitHubCommand = new RoutedCommand();

		public static RoutedCommand ApplyUpdateCommand = new RoutedCommand();
	}
}
