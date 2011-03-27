
namespace GitBlame.Models
{
	internal struct LinePart
	{
		public LinePart(string text, LinePartStatus status)
		{
			m_text = text.Replace("\t", "	");
			m_status = status;
		}

		public string Text
		{
			get { return m_text; }
		}

		public LinePartStatus Status
		{
			get { return m_status; }
		}

		readonly string m_text;
		readonly LinePartStatus m_status;
	}
}
