namespace GitBlame.Models
{
	internal readonly struct LinePart
	{
		public LinePart(string text, LinePartStatus status)
		{
			Text = text;
			Status = status;
		}

		public string Text { get; }
		public LinePartStatus Status { get; }
	}
}
