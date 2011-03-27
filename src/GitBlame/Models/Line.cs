
using System.Collections.ObjectModel;
using System.Linq;

namespace GitBlame.Models
{
	internal sealed class Line
	{
		public Line(int lineNumber, ReadOnlyCollection<LinePart> parts)
		{
			m_lineNumber = lineNumber;
			m_parts = parts;
		}

		public int LineNumber
		{
			get { return m_lineNumber; }
		}

		public bool IsNew
		{
			get { return m_parts.All(p => p.Status == LinePartStatus.New); }
		}

		public ReadOnlyCollection<LinePart> Parts
		{
			get { return m_parts; }
		}

		readonly int m_lineNumber;
		readonly ReadOnlyCollection<LinePart> m_parts;
	}
}
