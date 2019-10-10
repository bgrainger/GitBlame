namespace GitBlame.ViewModels
{
	public class OpenFileNotification : NotificationBase
	{
		public OpenFileNotification()
		{
		}

		public OpenFileNotification(string? filePath) => FilePath = filePath;

		public string? FilePath { get; }
	}
}
