using System.Collections.Generic;
using System.Linq;
using GitBlame.Utility;

namespace GitBlame.Models
{
	internal sealed class Line
	{
		public Line(int lineNumber, string text, bool isNew)
		{
			LineNumber = lineNumber;
			IsNew = isNew;
			Parts = new[] { new LinePart(text, isNew ? LinePartStatus.New : LinePartStatus.Existing) };
		}

		public Line(int lineNumber, int oldLineNumber, IReadOnlyList<LinePart> parts)
		{
			LineNumber = lineNumber;
			OldLineNumber = oldLineNumber;
			IsNew = parts.All(p => p.Status == LinePartStatus.New);
			Parts = parts;
		}

		public int LineNumber { get; }
		public int OldLineNumber { get; }
		public bool IsNew { get; }
		public IReadOnlyList<LinePart> Parts { get; }
	}
}
