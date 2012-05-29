using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DiffMatchPatch;
using GitBlame.Utility;
using LibGit2Sharp;

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
			string[] currentLines = File.ReadAllLines(filePath);
			int lineCount = blocks.Sum(b => b.LineCount);
			Invariant.Assert(lineCount == currentLines.Length, "Unexpected number of lines in file.");

			// initialize all lines from current version
			Line[] lines = currentLines
				.Select((l, n) => new Line(n + 1, l, false))
				.ToArray();

			BlameResult blameResult = new BlameResult(blocks.AsReadOnly(), lines, commits);
			Dictionary<string, Task<string>> getFileContentTasks = CreateGetFileContentTasks(directory, blocks, commits, currentLines);

			// process the blocks for each unique commit
			foreach (var groupLoopVariable in blocks.OrderBy(b => b.StartLine).GroupBy(b => b.Commit))
			{
				// check if this commit modifies a previous one
				var group = groupLoopVariable;
				Commit commit = group.Key;
				string commitId = commit.Id;
				string previousCommitId = commit.PreviousCommitId;

				if (previousCommitId != null)
				{
					// diff the old and new file contents when they become available
					Task<string> getOldFileContentTask = getFileContentTasks[previousCommitId];
					Task<string> getNewFileContentTask = getFileContentTasks[commitId];
					Task.Factory.ContinueWhenAll(new[] { getOldFileContentTask, getNewFileContentTask }, tasks =>
					{
						// diff the two versions
						string oldFileContents = tasks[0].Result;
						string newFileContents = tasks[1].Result;
						var diff = new diff_match_patch();
						var diffs = diff.diff_main(oldFileContents, newFileContents);
						diff.diff_cleanupSemantic(diffs);

						// process all the lines in the diff output, matching them to blocks
						using (IEnumerator<Line> lineEnumerator = ParseDiffOutput(diffs).GetEnumerator())
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
									blameResult.SetLine(line.LineNumber - block.OriginalStartLine + block.StartLine, line);

									// move to the next line (if available)
									if (lineEnumerator.MoveNext())
										line = lineEnumerator.Current;
									else
										break;
								}
							}
						}
					});
				}
				else
				{
					// this is the initial commit (but has not been modified since); grab its lines from the current version of the file
					foreach (Block block in group)
						for (int lineNumber = block.StartLine; lineNumber < block.StartLine + block.LineCount; lineNumber++)
							blameResult.SetLine(lineNumber, new Line(lineNumber, currentLines[lineNumber - 1], true));
				}
			}

			return blameResult;
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

		private static Dictionary<string, Task<string>> CreateGetFileContentTasks(string directory, List<Block> blocks, Dictionary<string, Commit> commits, string[] currentLines)
		{
			const string uncommittedChangesCommitId = "0000000000000000000000000000000000000000";

			// start a task to get the content of this file from each commit in its history
			string repositoryPath = GetRepositoryPath(directory);
			var getFileContentTasks = blocks
				.SelectMany(b => new[]
					{
						new { CommitId = b.Commit.Id, b.FileName },
						new { CommitId = b.Commit.PreviousCommitId, FileName = b.Commit.PreviousFileName }
					})
				.Distinct()
				.Where(c => c.CommitId != null && c.CommitId != uncommittedChangesCommitId)
				.ToDictionary(
					c => c.CommitId,
					c => Task.Factory.StartNew(() =>
					{
						using (var repo = new Repository(repositoryPath))
						{
							// look up commit in repo
							var gitCommit = repo.Lookup<LibGit2Sharp.Commit>(c.CommitId);

							// save the commit message
							Commit commit;
							if (commits.TryGetValue(c.CommitId, out commit))
								commit.SetMessage(gitCommit.Message);

							// get content from commit
							var blob = (Blob) gitCommit.Tree[c.FileName].Target;
							string content = blob.ContentAsUtf8();

							// strip BOM (U+FEFF) if present
							if (content.Length > 0 && content[0] == '\uFEFF')
								content = content.Substring(1);

							return content;
						}
					}));

			// add a task that returns the current version of the file
			getFileContentTasks.Add(uncommittedChangesCommitId, Task.Factory.StartNew(() => string.Join("\n", currentLines)));

			return getFileContentTasks;
		}

		private static IEnumerable<Line> ParseDiffOutput(List<DiffMatchPatch.Diff> diffs)
		{
			int lineNumber = 1;
			StringBuilder currentPart = new StringBuilder();
			List<LinePart> currentParts = new List<LinePart>();
			char lastChar = default(char);

			// process all "equal" or "insert" diffs
			foreach (var diff in diffs.Where(d => d.operation != Operation.DELETE))
			{
				// walk the text, breaking it into lines
				foreach (char ch in diff.text)
				{
					if ((ch == '\r' || ch == '\n'))
					{
						// treat "\r\n" as one newline
						if (!(lastChar == '\r' && ch == '\n'))
						{
							if (currentPart.Length != 0)
								currentParts.Add(new LinePart(currentPart.ToString(), diff.operation == Operation.EQUAL ? LinePartStatus.Existing : LinePartStatus.New));

							// found another line
							yield return new Line(lineNumber, currentParts.ToArray().AsReadOnly());

							// reset for next line
							currentPart.Length = 0;
							currentParts.Clear();
							lineNumber++;
						}
					}
					else
					{
						// add this character to the current part
						currentPart.Append(ch);
					}

					lastChar = ch;
				}

				// handle any remaining text at the end of a diff
				if (currentPart.Length != 0)
				{
					currentParts.Add(new LinePart(currentPart.ToString(), diff.operation == Operation.EQUAL ? LinePartStatus.Existing : LinePartStatus.New));
					currentPart.Length = 0;
				}
			}

			// handle any remaining text at the end of the file
			if (currentParts.Count != 0)
				yield return new Line(lineNumber, currentParts.ToArray().AsReadOnly());
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
				string gitPath = Path.Combine(folder, @"Git\bin\git.exe");
				if (File.Exists(gitPath))
					return gitPath;
			}

			throw new ApplicationException("Can't find msysgit installed on the system.");
		}

		private static string GetRepositoryPath(string directory)
		{
			do
			{
				string gitDirectory = Path.Combine(directory, ".git");
				if (Directory.Exists(gitDirectory))
					return gitDirectory;

				directory = Path.GetDirectoryName(directory);
			}
			while (directory != null);

			throw new ApplicationException("Can't find .git directory for " + directory);
		}
	}
}
