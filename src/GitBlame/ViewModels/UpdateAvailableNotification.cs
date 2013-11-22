namespace GitBlame.ViewModels
{
	public sealed class UpdateAvailableNotification : NotificationBase
	{
		public UpdateAvailableNotification(string path)
		{
			CommandParameter = path;
		}

		public string CommandParameter
		{
			get; private set;
		}
	}
}
