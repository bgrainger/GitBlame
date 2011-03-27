
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GitBlame.Utility;

namespace GitBlame.Models
{
	internal sealed class GitWrapper
	{
		public static BlameResult GetBlameOutput(string filePath)
		{
		   // run "git blame"
			string directory = Path.GetDirectoryName(filePath);
			string fileName = Path.GetFileName(filePath);
			ExternalProcess git = new ExternalProcess(GetGitPath(), directory);
			var results = git.Run(new ProcessRunSettings("blame", "--incremental", "--encoding=utf-8", fileName));
			if (results.ExitCode != 0)
				throw new ApplicationException(string.Format(CultureInfo.InvariantCulture, "git blame exited with code {0}", results.ExitCode));

			// parse output
			List<Block> blocks = new List<Block>();
			Dictionary<string, Commit> commits = new Dictionary<string, Commit>();
			ParseBlameOutput(results.Output, directory, blocks, commits);

			// allocate a (1-based) array for all lines in the file
			int lineCount = blocks.Sum(b => b.LineCount);
			Line[] lines = new Line[lineCount + 1];
			string[] currentLines = File.ReadAllLines(filePath);

			// process the blocks for each unique commit
			foreach (var group in blocks.OrderBy(b => b.StartLine).GroupBy(b => b.Commit))
			{
				// check if this commit modifies a previous one
				Commit commit = group.Key;
				string commitId = commit.Id;
				string previousCommitId = commit.PreviousCommitId;

				if (previousCommitId != null)
				{
					// get the differences between this commit and the previous one
					// TODO: Use --word-diff-regex to be smarter about word-breaking
					results = git.Run(new ProcessRunSettings("diff", "-U0", "--word-diff=porcelain", previousCommitId + ".." + commitId, "--", fileName));
					if (results.ExitCode != 0)
						throw new ApplicationException(string.Format(CultureInfo.InvariantCulture, "git diff exited with code {0}", results.ExitCode));

					// process all the lines in the diff output, matching them to blocks
					using (IEnumerator<Line> lineEnumerator = ParseDiffOutput(results.Output).GetEnumerator())
					{
						// move to first line (which is expected to always be present)
						Invariant.Assert(lineEnumerator.MoveNext(), "Expected at least one line from diff output.");
						Line line = lineEnumerator.Current;

						// process all the blocks, finding the corresponding lines from the diff for each one
						foreach (Block block in group)
						{
							// skip all lines that occurred before the start of this block
							while (line.LineNumber < block.OriginalStartLine)
							{
								Invariant.Assert(lineEnumerator.MoveNext(), "diff does not contain the expected number of lines.");
								line = lineEnumerator.Current;
							}

							// process all lines in the current block
							while (line.LineNumber >= block.OriginalStartLine && line.LineNumber < block.OriginalStartLine + block.LineCount)
							{
								// assign this line to the correct index in the blamed version of the file
								Invariant.Assert(lines[line.LineNumber - block.OriginalStartLine + block.StartLine] == null, "Current line has already been created.");
								lines[line.LineNumber - block.OriginalStartLine + block.StartLine] = line;

								// move to the next line (if available)
								if (lineEnumerator.MoveNext())
									line = lineEnumerator.Current;
								else
									break;
							}
						}
					}
				}
				else
				{
					// this is the initial commit (but has not been modified since); grab its lines from the current version of the file
					foreach (Block block in group)
						for (int lineIndex = block.StartLine; lineIndex < block.StartLine + block.LineCount; lineIndex++)
							lines[lineIndex] = new Line(lineIndex, new List<LinePart> { new LinePart(currentLines[lineIndex - 1], LinePartStatus.New) }.AsReadOnly());
				}
			}

			return new BlameResult(blocks.AsReadOnly(), lines.Skip(1).ToList().AsReadOnly(), commits);
		}

		private static void ParseBlameOutput(string output, string directory, List<Block> blocks, Dictionary<string, Commit> commits)
		{
			// read entire output of "git blame"
			using (StringReader reader = new StringReader(output))
			{
				string line;
				while ((line = reader.ReadLine()) != null)
				{
					// read beginning line in block, with format "hash origLine startLine lineCount"
					string[] components = line.Split(' ');
					string commitId = components[0];
					int originalStartLine = int.Parse(components[1], CultureInfo.InvariantCulture);
					int startLine = int.Parse(components[2], CultureInfo.InvariantCulture);
					int lineCount = int.Parse(components[3], CultureInfo.InvariantCulture);

					// read "tag value" pairs from block header, knowing that "filename" tag always ends the block
					Dictionary<string, string> tagValues = new Dictionary<string, string>();
					Tuple<string, string> tagValue;
					do
					{
						line = reader.ReadLine();
						tagValue = line.SplitOnSpace();
						tagValues.Add(tagValue.Item1, tagValue.Item2);
					} while (tagValue.Item1 != "filename");

					// check if this is a new commit
					Commit commit;
					if (!commits.TryGetValue(commitId, out commit))
					{
						commit = CreateCommit(commitId, tagValues);
						commits.Add(commitId, commit);
					}

					// add this block to the output, ordered by its StartLine
					Block block = new Block(startLine, lineCount, commit, tagValues["filename"], originalStartLine);
					int index = blocks.BinarySearch(block, new GenericComparer<Block>((l, r) => l.StartLine.CompareTo(r.StartLine)));
					Debug.Assert(index < 0, "index < 0", "New block should not already be in the list.");
					if (index < 0)
						blocks.Insert(~index, block);
				}
			}
		}

		private static IEnumerable<Line> ParseDiffOutput(string output)
		{
			using (StringReader reader = new StringReader(output))
			{
				// ignore header
				reader.ReadLine();
				reader.ReadLine();
				reader.ReadLine();
				reader.ReadLine();

				string diffLine = reader.ReadLine();
				while (diffLine != null)
				{
					Match match = Regex.Match(diffLine, @"^@@ -([\d]+)(?:,(\d+))? \+([\d]+)(?:,(\d+))? @@");
					int startingNewLine = int.Parse(match.Groups[3].Value);
					int expectedNewLineCount = match.Groups[4].Success ? int.Parse(match.Groups[4].Value) : 1;

					List<LinePart> parts = new List<LinePart>();
					int newLineCount = 0;

					// read one logical line (split across multiple physical lines) at a time
					bool lastLineWasDeleted = false;
					while ((diffLine = reader.ReadLine()) != null)
					{
						char marker = diffLine[0];

						// check for end-of-line indicator
						if (marker == '~' && !lastLineWasDeleted)
						{
							yield return new Line(startingNewLine + newLineCount, parts.AsReadOnly());
							parts = new List<LinePart>();
							newLineCount++;
						}
						else if (marker == '@')
						{
							Invariant.Assert(newLineCount == expectedNewLineCount, "Didn't read expected number of diff lines.");
							break;
						}
						else if (marker == ' ' || marker == '+')
						{
							lastLineWasDeleted = false;
							parts.Add(new LinePart(diffLine.Substring(1), marker == ' ' ? LinePartStatus.Existing : LinePartStatus.New));
						}
						else if (marker == '-')
						{
							lastLineWasDeleted = true;
						}
					}
				}
			}
		}

		// Reads known values from 'tagValues' to create a Commit object.
		private static Commit CreateCommit(string commitId, Dictionary<string, string> tagValues)
		{
			// read standard values (that are assumed to always be present)
			Person author = new Person(tagValues["author"], GetEmail(tagValues["author-mail"]));
			DateTimeOffset authorDate = ConvertUnixTime(tagValues["author-time"], tagValues["author-tz"]);
			Person committer = new Person(tagValues["committer"], GetEmail(tagValues["committer-mail"]));
			DateTimeOffset commitDate = ConvertUnixTime(tagValues["committer-time"], tagValues["committer-tz"]);
			string summary = tagValues["summary"];

			// read optional "previous" value
			string previousCommitId = null;
			string previousFileName = null;
			string previous;
			if (tagValues.TryGetValue("previous", out previous))
			{
				var hashFileName = previous.SplitOnSpace();
				previousCommitId = hashFileName.Item1;
				previousFileName = hashFileName.Item2;
			}

			return new Commit(commitId, author, authorDate, committer, commitDate, summary, previousCommitId, previousFileName);
		}

		// Converts a Unix timestamp (seconds past the epoch) and time zone to a DateTimeOffset value.
		private static DateTimeOffset ConvertUnixTime(string seconds, string timeZone)
		{
			// parse timestamp to base date time
			DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified);
			DateTime dateTime = epoch.AddSeconds(double.Parse(seconds, CultureInfo.InvariantCulture));

			// parse time zone to hours and minutes
			int hours = int.Parse(timeZone.Substring(1, 2), CultureInfo.InvariantCulture);
			int minutes = int.Parse(timeZone.Substring(3, 2), CultureInfo.InvariantCulture);
			TimeSpan offset = TimeSpan.FromMinutes((hours * 60 + minutes) * (timeZone[0] == '-' ? -1 : 1));

			return new DateTimeOffset(dateTime + offset, offset);
		}

		// Extracts the email address from a well-formed "author-mail" or "committer-mail" value.
		private static string GetEmail(string email)
		{
			return (email[0] == '<' && email[email.Length - 1] == '>') ? email.Substring(1, email.Length - 2) : email;
		}

		private static string GetGitPath()
		{
			string[] parentFolders = new[]
			{
				Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
				@"D:\Program Files (x86)",
			};

			foreach (string folder in parentFolders)
			{
				string gitPath = Path.Combine(folder, @"Git\cmd\git.cmd");
				if (File.Exists(gitPath))
					return gitPath;
			}

			throw new ApplicationException("Can't find msysgit installed on the system.");
		}
	}
}
