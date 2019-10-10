using System;
using System.Globalization;
using System.Net.Http;
using System.Reflection;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Common.Logging;
using GitBlame.Utility;
using Microsoft.Win32;

namespace GitBlame.Analytics
{
#pragma warning disable CA1001 // disposable HttpClient field
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
			m_appVersion = Assembly.GetCallingAssembly().GetName().Version!.ToString();
			m_clientId = LoadOrCreateClientId();
			m_httpClient = new HttpClient();

			m_httpClient.DefaultRequestHeaders.Add("User-Agent", m_provider.SystemUserAgent);
			m_httpClient.Timeout = TimeSpan.FromSeconds(3);
		}

		public Task SubmitAppViewAsync(string description) => SubmitAsync("t", "appview", "cd", description);

		public Task SubmitExceptionAsync(Exception ex, bool isFatal) => SubmitAsync("t", "exception", "exd", ex.GetType().Name, "exf", isFatal ? "1" : "0");

		public Task SubmitSessionStartAsync() => SubmitAsync("t", "event", "sc", "start", "ec", "Session", "ea", "Start");

		public Task SubmitSessionEndAsync() => SubmitAsync("t", "event", "sc", "end", "ec", "Session", "ea", "End");

		private async Task SubmitAsync(params string[] namesAndValues)
		{
			// get system properties
			var language = CultureInfo.CurrentUICulture.Name;
			var screenResolution = "{0}x{1}".FormatInvariant(m_provider.ScreenWidth, m_provider.ScreenHeight);
			var screenBitDepth = "{0}-bits".FormatInvariant(m_provider.ScreenColorDepth);

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

			// build the URL; use SSL to prevent casual sniffing of the data
			Uri uri = new Uri("https://ssl.google-analytics.com/collect");
			try
			{
                using var content = new StringContent(sb.ToString());
				await m_httpClient.PostAsync(uri, content).ConfigureAwait(false);
				Log.InfoFormat("Successfully posted {0}", sb.ToString());
			}
			catch (HttpRequestException ex)
			{
				Log.WarnFormat("Couldn't POST to Google Analytics: {0}", ex, ex);
			}
			catch (TaskCanceledException ex)
			{
				Log.WarnFormat("Couldn't POST to Google Analytics: {0}.", ex, ex);
			}
		}

		private static Guid LoadOrCreateClientId()
		{
			try
			{
				using RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\GitBlame");
				if (key is object)
				{
					if (!Guid.TryParse(key.GetValue("AnalyticsId") as string, out var clientId))
					{
						clientId = Guid.NewGuid();
						key.SetValue("AnalyticsId", clientId.ToString("d"));
					}
					return clientId;
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

			// NOTE: using "default" to create empty GUID for failure
			return default;
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

		static readonly ILog Log = LogManager.GetLogger("GoogleAnalytics");

		readonly string m_trackingId;
		readonly string m_appName;
		readonly GoogleAnalyticsStatisticsProvider m_provider;
		readonly string m_appVersion;
		readonly Guid m_clientId;
		readonly HttpClient m_httpClient;
	}
}
