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
	internal static class GitWrapper
	{
		public const string UncommittedChangesCommitId = "0000000000000000000000000000000000000000";

		public static BlameResult GetBlameOutput(string repositoryPath, string fileName, string blameCommitId)
		{
			string[] fileLines;

			if (blameCommitId is null)
			{
				fileLines = File.ReadAllLines(Path.Combine(Path.GetDirectoryName(repositoryPath), fileName));
				if (fileLines.Length > 0 && !string.IsNullOrEmpty(fileLines[0]) && fileLines[0][0] == '\uFEFF')
					fileLines[0] = fileLines[0][1..];
			}
			else
			{
				using var reader = new StringReader(GetFileContent(repositoryPath, blameCommitId, fileName));
				List<string> lines = new List<string>();
				while (reader.ReadLine() is string line)
					lines.Add(line);
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
						return m.Success ? new Uri($"https://{m.Groups["host"].Value}/{m.Groups["user"].Value}/{m.Groups["repo"].Value}/") : null;
					}).FirstOrDefault(x => x is object);

				var loadingPerson = new Person("Loading…", "loading");
				var commit = new Commit(UncommittedChangesCommitId, loadingPerson, DateTimeOffset.Now, loadingPerson, DateTimeOffset.Now, "", null, null);

				// create a fake blame result that assigns all the code to the HEAD revision
				blameResult = new BlameResult(webRootUrl, new[] { new Block(1, currentLines.Length, commit, fileName, 1) }.AsReadOnly(),
					currentLines.Select((l, n) => new Line(n + 1, l, true)).ToList(),
					new Dictionary<string, Commit> { { commit.Id, commit } });
			}

			Task.Run(() =>
			{
#if LIBGIT2SHARP
				List<Block> blocks2 = new List<Block>();
				Dictionary<string, Commit> commits2 = new Dictionary<string, Commit>();

				using (var repo = new Repository(repositoryPath))
				{
					var blameHunks = repo.Blame(fileName, new BlameOptions { StartingAt = blameCommitId });
					foreach (var blameHunk in blameHunks)
					{
						var gitCommit = blameHunk.FinalCommit;
						Commit commit;
						if (!commits2.TryGetValue(gitCommit.Sha, out commit))
						{
							var gitParents = gitCommit.Parents.ToList();
							commit = new Commit(gitCommit.Sha, new Person(gitCommit.Author.Name, gitCommit.Author.Email), gitCommit.Author.When,
								new Person(gitCommit.Committer.Name, gitCommit.Committer.Email), gitCommit.Committer.When, gitCommit.MessageShort,
								gitParents.Count == 0 ? null : gitParents[0].Sha, gitParents.Count == 0 ? null : blameHunk.InitialPath);
							commits2.Add(gitCommit.Sha, commit);
						}
						var block = new Block(blameHunk.FinalStartLineNumber + 1, blameHunk.LineCount, commit, blameHunk.InitialPath, blameHunk.InitialStartLineNumber + 1);
						blocks2.Add(block);
					}
				}
				string ToString(Block block) => $"{block.Commit.Id} {block.FileName} {block.Commit.Author.Email} {block.Commit.Committer.Email} {block.StartLine} {block.OriginalStartLine} {block.Commit.PreviousCommitId} {block.Commit.PreviousFileName}";
#endif

				// run "git blame"
				var git = new ExternalProcess(GetGitPath(), Path.GetDirectoryName(repositoryPath));
				var arguments = new List<string> { "blame", "--incremental", "--encoding=utf-8", "-w" };
				if (blameCommitId is object)
					arguments.Add(blameCommitId);
				arguments.AddRange(new[] { "--", fileName });
				var results = git.Run(new ProcessRunSettings(arguments.ToArray()));
				if (results.ExitCode != 0)
					return;

				// parse output
				var blocks = new List<Block>();
				var commits = new Dictionary<string, Commit>();
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

					if (previousCommitId is object)
					{
						// diff the old and new file contents when they become available
						var getOldFileContentTask = getFileContentTasks[previousCommitId];
						var getNewFileContentTask = getFileContentTasks[commitId];
						Task.Factory.ContinueWhenAll(new[] { getOldFileContentTask, getNewFileContentTask }, tasks =>
						{
							// diff the two versions
							var oldFileContents = tasks[0].Result;
							var newFileContents = tasks[1].Result;

							// diff_match_patch can generate incorrect output if there are more than 65536 lines being diffed
							var checkLines = GetLineCount(oldFileContents) < 65000 && GetLineCount(newFileContents) < 65000;

							var diff = new diff_match_patch { Diff_Timeout = 10 };
							var diffs = diff.diff_main(oldFileContents, newFileContents, checkLines);
							diff.diff_cleanupSemantic(diffs);

							// process all the lines in the diff output, matching them to blocks
							using var lineEnumerator = ParseDiffOutput(diffs).GetEnumerator();
							
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
			using StringReader reader = new StringReader(output);
			while (reader.ReadLine() is string line)
			{
				// read beginning line in block, with format "hash origLine startLine lineCount"
				var components = line.Split(' ');
				var commitId = components[0];
				int originalStartLine = int.Parse(components[1], CultureInfo.InvariantCulture);
				int startLine = int.Parse(components[2], CultureInfo.InvariantCulture);
				int lineCount = int.Parse(components[3], CultureInfo.InvariantCulture);

				// read "tag value" pairs from block header, knowing that "filename" tag always ends the block
				var tagValues = new Dictionary<string, string>();
				do
				{
					line = reader.ReadLine();
					var tagValue = line.SplitOnSpace();
					tagValues.Add(tagValue.Before, tagValue.After);
				} while (!tagValues.ContainsKey("filename"));

				// check if this is a new commit
				if (!commits.TryGetValue(commitId, out var commit))
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

		private static Dictionary<string, Task<string>> CreateGetFileContentTasks(string repositoryPath, List<Block> blocks, Dictionary<string, Commit> commits, string[] currentLines)
		{
			using var repo = new Repository(repositoryPath);
			var getFileContentTasks = blocks
				.SelectMany(b => new[]
				{
					new { CommitId = b.Commit.Id, b.FileName },
					new { CommitId = b.Commit.PreviousCommitId, FileName = b.Commit.PreviousFileName }
				})
				.Distinct()
				.Where(c => c.CommitId is object && c.CommitId != UncommittedChangesCommitId)
				.ToDictionary(
					c => c.CommitId,
					c => Task.FromResult(GetFileContent(repo, commits, c.CommitId, c.FileName)));

			// add a task that returns the current version of the file
			getFileContentTasks.Add(UncommittedChangesCommitId, Task.Run(() => string.Join("\n", currentLines)));

			return getFileContentTasks;
		}

		private static string GetFileContent(Repository repo, Dictionary<string, Commit> commits, string commitId, string fileName)
		{
			// look up commit in repo
			var gitCommit = repo.Lookup<LibGit2Sharp.Commit>(commitId);

			// save the commit message
			if (commits.TryGetValue(commitId, out var commit))
				commit.Message = gitCommit.Message;

			return GetFileContent(gitCommit, fileName);
		}

		private static string GetFileContent(string repositoryPath, string commitId, string fileName)
		{
			using var repo = new Repository(repositoryPath);
			var gitCommit = repo.Lookup<LibGit2Sharp.Commit>(commitId);
			return GetFileContent(gitCommit, fileName);
		}

		private static string GetFileContent(LibGit2Sharp.Commit commit, string fileName)
		{
			// get content from commit
			var blob = (Blob) commit.Tree[fileName].Target;
			string content = blob.GetContentText();

			// strip BOM (U+FEFF) if present
			if (content.Length > 0 && content[0] == '\uFEFF')
				content = content[1..];

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
						if (ch == '\n')
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
						else if (ch != '\r')
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
			var author = new Person(tagValues["author"], GetEmail(tagValues["author-mail"]));
			var authorDate = ConvertUnixTime(tagValues["author-time"], tagValues["author-tz"]);
			var committer = new Person(tagValues["committer"], GetEmail(tagValues["committer-mail"]));
			var commitDate = ConvertUnixTime(tagValues["committer-time"], tagValues["committer-tz"]);
			string summary = tagValues["summary"];

			// read optional "previous" value
			string previousCommitId = null;
			string previousFileName = null;
			if (tagValues.TryGetValue("previous", out var previous))
				(previousCommitId, previousFileName) = previous.SplitOnSpace();

			return new Commit(commitId, author, authorDate, committer, commitDate, summary, previousCommitId, previousFileName);
		}

		// Converts a Unix timestamp (seconds past the epoch) and time zone to a DateTimeOffset value.
		private static DateTimeOffset ConvertUnixTime(string seconds, string timeZone)
		{
			// parse timestamp to base date time
			var epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified);
			var dateTime = epoch.AddSeconds(double.Parse(seconds, CultureInfo.InvariantCulture));

			// parse time zone to hours and minutes
			int hours = int.Parse(timeZone[1..3], CultureInfo.InvariantCulture);
			int minutes = int.Parse(timeZone[3..5], CultureInfo.InvariantCulture);
			TimeSpan offset = TimeSpan.FromMinutes((hours * 60 + minutes) * (timeZone[0] == '-' ? -1 : 1));

			return new DateTimeOffset(dateTime + offset, offset);
		}

		// Extracts the email address from a well-formed "author-mail" or "committer-mail" value.
		private static string GetEmail(string email) => (email[0] == '<' && email[^1] == '>') ? email[1..^1] : email;

		private static string GetGitPath()
		{
			string[] gitPaths =
			{
				Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Git\bin\git.exe"),
				Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Git\bin\git.exe"),
				Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Git\bin\git.exe"),
				@"D:\Program Files (x86)\Git\bin\git.exe",
				@"C:\msysgit\bin\git.exe"
			};

			foreach (string gitPath in gitPaths.Where(x => File.Exists(x)))
				return gitPath;

			throw new ApplicationException("Can't find msysgit installed on the system.");
		}

		private static int GetLineCount(string text)
		{
			int lineCount = 0;
			foreach (var ch in text)
			{
				if (ch == '\n')
					lineCount++;
			}
			return lineCount;
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
					using Repository repo = new Repository(gitDirectory);
					var entry = repo.Index.FirstOrDefault(x => string.Equals(x.Path, probeFileName, StringComparison.OrdinalIgnoreCase));
					fileName = entry is object ? entry.Path : probeFileName;
					return true;
				}

				currentDirectory = Path.GetDirectoryName(currentDirectory);
			}
			while (currentDirectory is object);

			Log.WarnFormat("Can't find .git directory for {0}", filePath);
			gitDirectory = null;
			fileName = null;
			return false;
		}

		static readonly ILog Log = LogManager.GetLogger("GitWrapper");
	}
}
