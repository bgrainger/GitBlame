using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Common.Logging;
using DiffMatchPatch;
using GitBlame.Utility;
using LibGit2Sharp;

namespace GitBlame.Models
{
	internal sealed class GitWrapper
	{
		public const string UncommittedChangesCommitId = "0000000000000000000000000000000000000000";

		public static BlameResult GetBlameOutput(string repositoryPath, string fileName, string blameCommitId)
		{
			string[] fileLines;

			if (blameCommitId == null)
			{
				fileLines = File.ReadAllLines(Path.Combine(Path.GetDirectoryName(repositoryPath), fileName));
			}
			else
			{
				List<string> lines = new List<string>();
				using (StringReader reader = new StringReader(GetFileContent(repositoryPath, blameCommitId, fileName)))
				{
					string line;
					while ((line = reader.ReadLine()) != null)
						lines.Add(line);
				}
				fileLines = lines.ToArray();
			}

			return GetBlameOutput(repositoryPath, fileName, blameCommitId, fileLines);
		}

		private static BlameResult GetBlameOutput(string repositoryPath, string fileName, string blameCommitId, string[] currentLines)
		{
			BlameResult blameResult;
			using (var repo = new Repository(repositoryPath))
			{
				// try to determine if the remote URL is plausibly a github.com or GitHub Enterprise URL
				Uri webRootUrl = repo.Network.Remotes
					.OrderBy(x => x.Name == "origin" ? 0 : 1)
					.ThenBy(x => x.Name)
					.Select(x =>
					{
						Match m = Regex.Match(x.Url, @"^(git@(?'host'[^:]+):(?'user'[^/]+)/(?'repo'[^/]+)\.git|(git|https?)://(?'host'[^/]+)/(?'user'[^/]+)/(?'repo'[^/]+)\.git)$", RegexOptions.ExplicitCapture);
						if (m.Success)
						{
							string host = m.Groups["host"].Value;
							return new Uri(string.Format("http{0}://{1}/{2}/{3}/", host == "github.com" ? "s" : "", host, m.Groups["user"].Value, m.Groups["repo"].Value));
						}
						else
						{
							return null;
						}
					}).FirstOrDefault(x => x != null);

				var notCommittedPerson = new Person("Not Committed Yet", "not.committed.yet");
				var commit = new Commit(UncommittedChangesCommitId, notCommittedPerson, DateTimeOffset.Now, notCommittedPerson, DateTimeOffset.Now, "", null, null);

				// create a fake blame result that assigns all the code to the HEAD revision
				blameResult = new BlameResult(webRootUrl, new[] { new Block(1, currentLines.Length, commit, fileName, 1) }.AsReadOnly(),
					currentLines.Select((l, n) => new Line(n + 1, l, true)).ToList(),
					new Dictionary<string, Commit> { { commit.Id, commit } });
			}

			Task.Run(() =>
			{
				// run "git blame"
				ExternalProcess git = new ExternalProcess(GetGitPath(), Path.GetDirectoryName(repositoryPath));
				List<string> arguments = new List<string> { "blame", "--incremental", "--encoding=utf-8" };
				if (blameCommitId != null)
					arguments.Add(blameCommitId);
				arguments.AddRange(new[] { "--", fileName });
				var results = git.Run(new ProcessRunSettings(arguments.ToArray()));
				if (results.ExitCode != 0)
					return;

				// parse output
				List<Block> blocks = new List<Block>();
				Dictionary<string, Commit> commits = new Dictionary<string, Commit>();
				ParseBlameOutput(results.Output, blocks, commits);

				// allocate a (1-based) array for all lines in the file
				int lineCount = blocks.Sum(b => b.LineCount);
				Invariant.Assert(lineCount == currentLines.Length, "Unexpected number of lines in file.");

				// initialize all lines from current version
				Line[] lines = currentLines
					.Select((l, n) => new Line(n + 1, l, false))
					.ToArray();

				blameResult.SetData(blocks, lines, commits);
				Dictionary<string, Task<string>> getFileContentTasks = CreateGetFileContentTasks(repositoryPath, blocks, commits, currentLines);

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
			});

			return blameResult;
		}

		private static void ParseBlameOutput(string output, List<Block> blocks, Dictionary<string, Commit> commits)
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

		private static Dictionary<string, Task<string>> CreateGetFileContentTasks(string repositoryPath, List<Block> blocks, Dictionary<string, Commit> commits, string[] currentLines)
		{
			// start a task to get the content of this file from each commit in its history
			var getFileContentTasks = blocks
				.SelectMany(b => new[]
					{
						new { CommitId = b.Commit.Id, b.FileName },
						new { CommitId = b.Commit.PreviousCommitId, FileName = b.Commit.PreviousFileName }
					})
				.Distinct()
				.Where(c => c.CommitId != null && c.CommitId != UncommittedChangesCommitId)
				.ToDictionary(
					c => c.CommitId,
					c => Task.Run(() =>
					{
						using (var repo = new Repository(repositoryPath))
						{
							// look up commit in repo
							var gitCommit = repo.Lookup<LibGit2Sharp.Commit>(c.CommitId);

							// save the commit message
							Commit commit;
							if (commits.TryGetValue(c.CommitId, out commit))
								commit.SetMessage(gitCommit.Message);

							return GetFileContent(gitCommit, c.FileName);
						}
					}));

			// add a task that returns the current version of the file
			getFileContentTasks.Add(UncommittedChangesCommitId, Task.Run(() => string.Join("\n", currentLines)));

			return getFileContentTasks;
		}

		private static string GetFileContent(string repositoryPath, string commitId, string fileName)
		{
			using (var repo = new Repository(repositoryPath))
			{
				var gitCommit = repo.Lookup<LibGit2Sharp.Commit>(commitId);
				return GetFileContent(gitCommit, fileName);
			}
		}

		private static string GetFileContent(LibGit2Sharp.Commit commit, string fileName)
		{
			// get content from commit
			var blob = (Blob) commit.Tree[fileName].Target;
			string content = blob.GetContentText();

			// strip BOM (U+FEFF) if present
			if (content.Length > 0 && content[0] == '\uFEFF')
				content = content.Substring(1);

			return content;
		}

		private static IEnumerable<Line> ParseDiffOutput(List<DiffMatchPatch.Diff> diffs)
		{
			int lineNumber = 1;
			int firstOldLineNumber = 1, lastOldLineNumber = 1;
			StringBuilder currentPart = new StringBuilder();
			List<LinePart> currentParts = new List<LinePart>();
			char lastChar = default(char);

			// process all "equal" or "insert" diffs
			foreach (var diff in diffs)
			{
				if (diff.operation == Operation.DELETE)
				{
					lastOldLineNumber += diff.text.Count(x => x == '\n');
				}
				else
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
								yield return new Line(lineNumber, firstOldLineNumber, currentParts.ToArray().AsReadOnly());

								// reset for next line
								currentPart.Length = 0;
								currentParts.Clear();
								if (diff.operation == Operation.EQUAL)
									lastOldLineNumber++;
								lineNumber++;
								firstOldLineNumber = lastOldLineNumber;
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
			}

			// handle any remaining text at the end of the file
			if (currentParts.Count != 0)
				yield return new Line(lineNumber, firstOldLineNumber, currentParts.ToArray().AsReadOnly());
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
			string[] gitPaths =
			{
				Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Git\bin\git.exe"),
				Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Git\bin\git.exe"),
				@"D:\Program Files (x86)\Git\bin\git.exe",
				@"C:\msysgit\bin\git.exe"
			};

			foreach (string gitPath in gitPaths.Where(x => File.Exists(x)))
				return gitPath;

			throw new ApplicationException("Can't find msysgit installed on the system.");
		}

		public static bool SplitRepositoryPath(string filePath, out string gitDirectory, out string fileName)
		{
			string currentDirectory = filePath;
			do
			{
				gitDirectory = Path.Combine(currentDirectory, ".git");
				if (Directory.Exists(gitDirectory))
				{
					string probeFileName = filePath.Substring(currentDirectory.Length + 1);
					using (Repository repo = new Repository(gitDirectory))
					{
						var entry = repo.Index.FirstOrDefault(x => string.Equals(x.Path, probeFileName, StringComparison.OrdinalIgnoreCase));
						fileName = entry != null ? entry.Path : probeFileName;
						return true;
					}
				}

				currentDirectory = Path.GetDirectoryName(currentDirectory);
			}
			while (currentDirectory != null);

			Log.WarnFormat("Can't find .git directory for {0}", filePath);
			fileName = null;
			return false;
		}

		static readonly ILog Log = LogManager.GetLogger("GitWrapper");
	}
}
