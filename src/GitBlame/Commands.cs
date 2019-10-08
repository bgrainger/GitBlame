using System.Windows.Input;

namespace GitBlame
{
	public static class Commands
	{
		public static RoutedCommand BlamePreviousCommand { get; } = new RoutedCommand();

		public static RoutedCommand ViewAtGitHubCommand { get; } = new RoutedCommand();

		public static RoutedCommand ApplyUpdateCommand { get; } = new RoutedCommand();

		public static RoutedCommand ExitApplicationCommand { get; } = new RoutedCommand();

		public static RoutedCommand ShowGoToLineInputCommand { get; } = new RoutedCommand();
	}
}
