using System;
using System.Globalization;
using System.Net.Http;
using System.Reflection;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using GitBlame.Utility;
using Microsoft.Win32;

namespace GitBlame.Analytics
{
	/// <summary>
	/// <see cref="GoogleAnalyticsClient"/> reports basic usage statistics to Google Analytics.
	/// </summary>
	internal class GoogleAnalyticsClient
	{
		public GoogleAnalyticsClient(string trackingId, string appName, GoogleAnalyticsStatisticsProvider provider)
		{
			m_trackingId = trackingId;
			m_appName = appName;
			m_provider = provider;
			m_appVersion = Assembly.GetCallingAssembly().GetName().Version.ToString();
			m_clientId = LoadOrCreateClientId();
			m_httpClient = new HttpClient();

			m_httpClient.DefaultRequestHeaders.Add("User-Agent", m_provider.SystemUserAgent);
			m_httpClient.Timeout = TimeSpan.FromSeconds(3);
		}

		public Task SubmitAppViewAsync(string description)
		{
			return SubmitAsync("t", "appview", "cd", description);
		}

		public Task SubmitExceptionAsync(Exception ex, bool isFatal)
		{
			return SubmitAsync("t", "exception", "exd", ex.GetType().Name, "exf", isFatal ? "1" : "0");
		}

		public Task SubmitSessionEndAsync()
		{
			return SubmitAsync("t", "appview", "sc", "end");
		}

		private async Task SubmitAsync(params string[] namesAndValues)
		{
			// get system properties
			string language = CultureInfo.CurrentUICulture.Name;
			string screenResolution = "{0}x{1}".FormatInvariant(m_provider.ScreenWidth, m_provider.ScreenHeight);
			string screenBitDepth = "{0}-bits".FormatInvariant(m_provider.ScreenColorDepth);

			// build the payload; parameter names taken from https://developers.google.com/analytics/devguides/collection/protocol/v1/parameters
			StringBuilder sb = new StringBuilder();
			AddParameter(sb, "v", "1"); // version 1
			AddParameter(sb, "tid", m_trackingId);
			AddParameter(sb, "cid", m_clientId.ToString("d"));
			for (int i = 0;  i <namesAndValues.Length; i += 2)
				AddParameter(sb, namesAndValues[i], namesAndValues[i + 1]);
			AddParameter(sb, "an", m_appName);
			AddParameter(sb, "av", m_appVersion);
			AddParameter(sb, "cd1", m_provider.OperatingSystemVersion);
			AddParameter(sb, "sr", screenResolution);
			AddParameter(sb, "sd", screenBitDepth);
			AddParameter(sb, "ul", language);
			AddParameter(sb, "fl", m_provider.Is64BitOperatingSystem ? "64-bit" : "32-bit");
			if (!m_hasSubmittedSessionStart)
			{
				AddParameter(sb, "sc", "start");
				m_hasSubmittedSessionStart = true;
			}

			// build the URL; use SSL to prevent casual sniffing of the data
			Uri uri = new Uri("https://ssl.google-analytics.com/collect");
			await m_httpClient.PostAsync(uri, new StringContent(sb.ToString())).ConfigureAwait(false);
		}

		private static Guid LoadOrCreateClientId()
		{
			try
			{
				using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\GitBlame"))
				{
					if (key != null)
					{
						Guid clientId;
						string analyticsId = key.GetValue("AnalyticsId") as string;
						if (!Guid.TryParse(analyticsId, out clientId))
						{
							clientId = Guid.NewGuid();
							key.SetValue("AnalyticsId", clientId.ToString("d"));
						}
						return clientId;
					}
				}
			}
			catch (FormatException)
			{
			}
			catch (SecurityException)
			{
			}
			catch (UnauthorizedAccessException)
			{
			}

			// NOTE: using "new Guid" to create empty GUID for failure
			return new Guid();
		}

		// URL-encodes a query parameter name and value, and adds it to 'sb'.
		private static void AddParameter(StringBuilder sb, string name, string value)
		{
			if (sb.Length != 0)
				sb.Append('&');
			sb.Append(name);
			sb.Append('=');
			sb.Append(Uri.EscapeDataString(value));
		}

		readonly string m_trackingId;
		readonly string m_appName;
		readonly GoogleAnalyticsStatisticsProvider m_provider;
		readonly string m_appVersion;
		readonly Guid m_clientId;
		readonly HttpClient m_httpClient;
		bool m_hasSubmittedSessionStart;
	}
}
