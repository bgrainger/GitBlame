
using System.Collections.ObjectModel;
using System.Linq;
using GitBlame.Utility;

namespace GitBlame.Models
{
	internal sealed class Line
	{
		public Line(int lineNumber, string text, bool isNew)
		{
			m_lineNumber = lineNumber;
			m_isNew = isNew;
			m_parts = new[] { new LinePart(text, isNew ? LinePartStatus.New : LinePartStatus.Existing) }.AsReadOnly();
		}

		public Line(int lineNumber, ReadOnlyCollection<LinePart> parts)
		{
			m_lineNumber = lineNumber;
			m_isNew = parts.All(p => p.Status == LinePartStatus.New);
			m_parts = parts;
		}

		public int LineNumber
		{
			get { return m_lineNumber; }
		}

		public bool IsNew
		{
			get { return m_isNew; }
		}

		public ReadOnlyCollection<LinePart> Parts
		{
			get { return m_parts; }
		}

		readonly int m_lineNumber;
		readonly bool m_isNew;
		readonly ReadOnlyCollection<LinePart> m_parts;
	}
}
