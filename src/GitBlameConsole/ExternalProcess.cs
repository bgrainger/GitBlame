
using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace GitBlameConsole
{
	public sealed class ExternalProcess
	{
		public ExternalProcess(string executablePath) : this(executablePath, null)
		{
		}

		public ExternalProcess(string executablePath, string defaultWorkingDirectory)
		{
			if (executablePath == null)
				throw new ArgumentNullException("executablePath");
			if (!File.Exists(executablePath))
				throw new FileNotFoundException("Specified executable wasn't found.", "executablePath");

			m_executablePath = executablePath;
			m_defaultWorkingDirectory = defaultWorkingDirectory;
		}

		public ExternalProcessResults Run(string arguments)
		{
			return Run(new ProcessRunSettings(arguments));
		}

		public ExternalProcessResults Run(ProcessRunSettings settings)
		{
			StringBuilder errors = new StringBuilder();
			using (Process process = StartProcess(settings, errors))
			{
				// wait for process to end (must read all output first)
				string output = null;
				if (settings.StandardOutputStream == null)
					output = process.StandardOutput.ReadToEnd();
				else
					process.StandardOutput.BaseStream.CopyTo(settings.StandardOutputStream);

				process.WaitForExit();

				// return errors and process output
				return new ExternalProcessResults(output, errors.ToString(), process.ExitCode);
			}
		}

		private Process StartProcess(ProcessRunSettings settings, StringBuilder errorsBuilder, params string[] env)
		{
			Process process = new Process
			{
				StartInfo =
					{
						FileName = m_executablePath,
						Arguments = settings.Arguments,
						UseShellExecute = false,
						RedirectStandardOutput = true,
						StandardOutputEncoding = Encoding.UTF8,
						RedirectStandardError = true,
						WorkingDirectory = m_defaultWorkingDirectory,
					}
			};

			if (settings.EnvironmentModifier != null)
				settings.EnvironmentModifier(process.StartInfo.EnvironmentVariables);

			// add additional environment variables
			for (int n = 0; n < env.Length; n += 2)
				process.StartInfo.EnvironmentVariables[env[n]] = env[n + 1];

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
		public ProcessRunSettings(string arguments)
		{
			m_arguments = arguments;
		}

		public ProcessRunSettings(params string[] arguments)
		{
			m_arguments = string.Join(" ", arguments.Select(arg => arg.Trim()));
		}

		public string Arguments
		{
			get { return m_arguments; }
		}

		public Action<StringDictionary> EnvironmentModifier { get; set; }

		public Stream StandardOutputStream { get; set; }

		readonly string m_arguments;
	}

	public struct ExternalProcessResults
	{
		public ExternalProcessResults(string output, string errors, int exitCode)
		{
			m_output = output;
			m_errors = errors;
			m_exitCode = exitCode;
		}

		public string Output
		{
			get { return m_output; }
		}

		public string Errors
		{
			get { return m_errors; }
		}

		public int ExitCode
		{
			get { return m_exitCode; }
		}

		readonly string m_output;
		readonly string m_errors;
		readonly int m_exitCode;
	}
}
