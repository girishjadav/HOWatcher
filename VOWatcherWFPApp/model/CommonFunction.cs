using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VOWatcherWFPApp.Services;

namespace VOWatcherWFPApp.model
{
    public class CommonFunction
    {
        public string BytesToReadableValue(long number)
        {
            List<string> suffixes = new List<string> { " B", " KB", " MB", " GB", " TB", " PB" };

            for (int i = 0; i < suffixes.Count; i++)
            {
                long temp = number / (int)Math.Pow(1024, i + 1);

                if (temp == 0)
                {
                    return (number / (int)Math.Pow(1024, i)) + suffixes[i];
                }
            }
            return number.ToString();
        }

        public string GetBrowserUrl(Process process)
        {
            string url = "";
            switch (process.ProcessName.ToLower())
            {
                case "chrome":
                    url = APIFuncs.GetChromeUrl(process);
                    break;
                case "msedge":
                    url = APIFuncs.GetEdgeUrl(process);
                    break;
                case "firefox":
                    url = APIFuncs.GetFirefoxUrl(process);
                    break;
            }
            return url;
        }

        public bool IsBrowserProcess(string processName)
        {
            if (string.IsNullOrEmpty(processName))
                return false;

            string lowerProcessName = processName.ToLower();

            return lowerProcessName == "chrome" ||
                   lowerProcessName == "msedge" ||
                   lowerProcessName == "firefox" ||
                   lowerProcessName == "opera" ||
                   lowerProcessName == "brave" ||
                   lowerProcessName == "iexplore" ||
                   lowerProcessName == "safari" ||
                   lowerProcessName.Contains("browser");
        }

        public string SecondsToTime(double secs)
        {
            TimeSpan t = TimeSpan.FromSeconds(secs);
            return string.Format("{0:D2}:{1:D2}:{2:D2}",
                (int)t.TotalHours,
                t.Minutes,
                t.Seconds);
        }

        public string CreateAppKey(string appId, string appName, string appTitle)
        {
            // Check if this is a browser application
            if (IsBrowserProcess(appName))
            {
                // For browsers, create unique keys for each tab by including process ID and full title
                // This ensures each browser tab is tracked separately
                string normalizedTitle = appTitle ?? string.Empty;
                if (string.IsNullOrWhiteSpace(normalizedTitle)) normalizedTitle = appName;
                
                // Include process ID to make each tab unique
                return $"{appName}|{appId}|{normalizedTitle}";
            }
            else
            {
                // For other applications, group by normalized window title too (avoid per-process keys)
                string normalizedTitle = string.IsNullOrWhiteSpace(appTitle) ? appName : appTitle;
                return $"{appName}|{normalizedTitle}";
            }
        }
    }
}
