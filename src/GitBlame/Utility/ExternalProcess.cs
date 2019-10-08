using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace GitBlame.Utility
{
	public sealed class ExternalProcess
	{
		public ExternalProcess(string executablePath)
			: this(executablePath, null)
		{
		}

		public ExternalProcess(string executablePath, string defaultWorkingDirectory)
		{
			if (executablePath is null)
				throw new ArgumentNullException(nameof(executablePath));
			if (!File.Exists(executablePath))
				throw new FileNotFoundException("Specified executable wasn't found.", executablePath);

			m_executablePath = executablePath;
			m_defaultWorkingDirectory = defaultWorkingDirectory;
		}

		public ExternalProcessResults Run(ProcessRunSettings settings)
		{
			StringBuilder errors = new StringBuilder();
			using var process = StartProcess(settings, errors);
			
			// wait for process to end (must read all output first)
			var output = process.StandardOutput.ReadToEnd();
			process.WaitForExit();

			// return errors and process output
			return new ExternalProcessResults(output, errors.ToString(), process.ExitCode);
		}

		private Process StartProcess(ProcessRunSettings settings, StringBuilder errorsBuilder)
		{
			var process = new Process
			{
				StartInfo =
				{
					FileName = m_executablePath,
					Arguments = settings.Arguments,
					UseShellExecute = false,
					RedirectStandardInput = true,
					RedirectStandardOutput = true,
					StandardOutputEncoding = Encoding.UTF8,
					RedirectStandardError = true,
					WorkingDirectory = m_defaultWorkingDirectory,
					CreateNoWindow = true,
				},
			};

			process.ErrorDataReceived += (sender, e) => errorsBuilder.Append(e.Data);

			// start process
			process.Start();

			// read asynchronously from the error stream
			process.BeginErrorReadLine();
			return process;
		}

		readonly string m_executablePath;
		readonly string m_defaultWorkingDirectory;
	}

	public class ProcessRunSettings
	{
		public ProcessRunSettings(string arguments) => Arguments = arguments;

		public ProcessRunSettings(params string[] arguments) => Arguments = string.Join(" ", arguments.Select(arg => arg.Trim()));

		public string Arguments { get; }
	}

#pragma warning disable CA1815 // implement Equals
    public readonly struct ExternalProcessResults
	{
		public ExternalProcessResults(string output, string errors, int exitCode)
		{
			Output = output;
			Errors = errors;
			ExitCode = exitCode;
		}

		public string Output { get; }
		public string Errors { get; }
		public int ExitCode { get; }
	}
}
