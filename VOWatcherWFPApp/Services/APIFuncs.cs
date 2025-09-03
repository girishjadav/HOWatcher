using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Windows.Automation;

namespace VOWatcherWFPApp.Services
{
    public class APIFuncs
    {
        internal struct LASTINPUTINFO
        {
            public uint cbSize;

            public uint dwTime;
        }

        #region Windows API Functions Declarations
        //This Function is used to get Active Window Title...
        [DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        public static extern int GetWindowText(IntPtr hwnd, string lpString, int cch);

        //This Function is used to get Handle for Active Window...
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern IntPtr GetForegroundWindow();

        //This Function is used to get Active process ID...
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern Int32 GetWindowThreadProcessId(IntPtr hWnd, out Int32 lpdwProcessId);
        #endregion

        #region User-defined Functions
        public static Int32 GetWindowProcessID(IntPtr hwnd)
        {
            //This Function is used to get Active process ID...
            Int32 pid;
            GetWindowThreadProcessId(hwnd, out pid);
            return pid;
        }
        public static IntPtr getforegroundWindow()
        {
            //This method is used to get Handle for Active Window using GetForegroundWindow() method present in user32.dll
            return GetForegroundWindow();
        }
        public static string ActiveApplTitle()
        {
            //This method is used to get active application's title using GetWindowText() method present in user32.dll
            IntPtr hwnd = getforegroundWindow();
            if (hwnd.Equals(IntPtr.Zero)) return "";
            string lpText = new string((char)0, 100);
            int intLength = GetWindowText(hwnd, lpText, lpText.Length);
            if ((intLength <= 0) || (intLength > lpText.Length))
                return "unknown-apptitle";
            return lpText.Trim();
        }

        [DllImport("User32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [DllImport("Kernel32.dll")]
        private static extern uint GetLastError();

        public static uint GetIdleTime()
        {
            LASTINPUTINFO lastInPut = new LASTINPUTINFO();
            lastInPut.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(lastInPut);
            GetLastInputInfo(ref lastInPut);

            return ((uint)Environment.TickCount - lastInPut.dwTime);
        }

        public static TimeSpan RetrieveIdleTime()
        {
            LASTINPUTINFO lastInputInfo = new LASTINPUTINFO();
            lastInputInfo.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(lastInputInfo);
            GetLastInputInfo(ref lastInputInfo);

            int elapsedTicks = Environment.TickCount - (int)lastInputInfo.dwTime;

            if (elapsedTicks > 0) { return new TimeSpan(0, 0, 0, 0, elapsedTicks); }
            else { return new TimeSpan(0); }
        }
        public static long GetTickCount()
        {
            return Environment.TickCount;
        }

        public static long GetLastInputTime()
        {
            LASTINPUTINFO lastInPut = new LASTINPUTINFO();
            lastInPut.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(lastInPut);
            if (!GetLastInputInfo(ref lastInPut))
            {
                throw new Exception(GetLastError().ToString());
            }

            return lastInPut.dwTime;
        }

        [DllImport("Kernel32.dll")]
        private static extern bool QueryFullProcessImageName([In] IntPtr hProcess, [In] uint dwFlags, [Out] StringBuilder lpExeName, [In, Out] ref uint lpdwSize);

        public static string GetMainModuleFileName(Process process, int buffer = 1024)
        {
            var fileNameBuilder = new StringBuilder(buffer);
            uint bufferLength = (uint)fileNameBuilder.Capacity + 1;
            return QueryFullProcessImageName(process.Handle, 0, fileNameBuilder, ref bufferLength) ?
                fileNameBuilder.ToString() :
                null;
        }

        #endregion

        #region GetURL
        public static string GetChromeUrl(Process process)
        {
            if (process == null)
                throw new ArgumentNullException("process");

            if (process.MainWindowHandle == IntPtr.Zero)
                return null;
            try
            {
                AutomationElement element = AutomationElement.FromHandle(process.MainWindowHandle);
                if (element == null)
                    return null;

                AutomationElement edit = null;
                
                // Try multiple strategies to find the address bar
                string[] automationIds = { "Address and search bar", "addressBar", "omnibox" };
                string[] names = { "Address and search bar", "Address bar", "Search or enter address" };
                
                // Strategy 1: Try by AutomationId
                foreach (string id in automationIds)
                {
                    try
                    {
                        var cond = new AndCondition(
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
                            new PropertyCondition(AutomationElement.AutomationIdProperty, id));
                        edit = element.FindFirst(TreeScope.Descendants, cond);
                        if (edit != null) break;
                    }
                    catch { }
                }

                // Strategy 2: Try by Name
                if (edit == null)
                {
                    foreach (string name in names)
                    {
                        try
                        {
                            var cond = new AndCondition(
                                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
                                new PropertyCondition(AutomationElement.NameProperty, name));
                            edit = element.FindFirst(TreeScope.Descendants, cond);
                            if (edit != null) break;
                        }
                        catch { }
                    }
                }

                // Strategy 3: Try by ClassName (Chrome specific)
                if (edit == null)
                {
                    try
                    {
                        var cond = new AndCondition(
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
                            new PropertyCondition(AutomationElement.ClassNameProperty, "Chrome_OmniboxView"));
                        edit = element.FindFirst(TreeScope.Descendants, cond);
                    }
                    catch { }
                }

                // Strategy 4: Fallback to first Edit control
                if (edit == null)
                {
                    try
                    {
                        edit = element.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
                    }
                    catch { }
                }

                if (edit == null)
                    return null;

                try
                {
                    object patternObj;
                    if (edit.TryGetCurrentPattern(ValuePattern.Pattern, out patternObj))
                    {
                        var valuePattern = (ValuePattern)patternObj;
                        string url = valuePattern.Current.Value as String;
                        if (!string.IsNullOrWhiteSpace(url) && (url.StartsWith("http://") || url.StartsWith("https://")))
                        {
                            return url;
                        }
                    }
                    else if (edit.TryGetCurrentPattern(TextPattern.Pattern, out patternObj))
                    {
                        var textPattern = (TextPattern)patternObj;
                        string url = textPattern.DocumentRange.GetText(-1).TrimEnd('\r') as String;
                        if (!string.IsNullOrWhiteSpace(url) && (url.StartsWith("http://") || url.StartsWith("https://")))
                        {
                            return url;
                        }
                    }
                    else
                    {
                        string url = edit.Current.Name as String;
                        if (!string.IsNullOrWhiteSpace(url) && (url.StartsWith("http://") || url.StartsWith("https://")))
                        {
                            return url;
                        }
                    }
                }
                catch { return null; }

            }
            catch { return null; }

            return null;
        }
        public static string GetEdgeUrl(Process process)
        {
            // Edge structure similar to Chrome; reuse logic
            return GetChromeUrl(process);
        }
        public static string GetInternetExplorerUrl(Process process)
        {
            if (process == null)
                throw new ArgumentNullException("process");

            if (process.MainWindowHandle == IntPtr.Zero)
                return null;

            //AutomationElement element = AutomationElement.FromHandle(process.MainWindowHandle);
            //if (element == null)
            //    return null;

            //AutomationElement rebar = element.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ClassNameProperty, "ReBarWindow32"));
            //if (rebar == null)
            //    return null;

            //AutomationElement edit = rebar.FindFirst(TreeScope.Subtree, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));

            //return ((ValuePattern)edit.GetCurrentPattern(ValuePattern.Pattern)).Current.Value as string;
            AutomationElement element = AutomationElement.FromHandle(process.MainWindowHandle);
            if (element == null)
                return null;

            AutomationElement rebar = element.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ClassNameProperty, "ReBarWindow32"));
            if (rebar == null)
                return null;
            AutomationElement edit = rebar.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
            if (edit == null)
                return null;

            try
            {
                object patternObj;
                if (edit.TryGetCurrentPattern(ValuePattern.Pattern, out patternObj))
                {
                    var valuePattern = (ValuePattern)patternObj;
                    return valuePattern.Current.Value as String;
                }
                else if (element.TryGetCurrentPattern(TextPattern.Pattern, out patternObj))
                {
                    var textPattern = (TextPattern)patternObj;
                    return textPattern.DocumentRange.GetText(-1).TrimEnd('\r') as String;  // often there is an extra '\r' hanging off the end.
                }
                else
                {
                    return element.Current.Name as String;
                }
            }
            catch { return ""; }

        }

        public static string GetFirefoxUrl(Process process)
        {
            if (process == null)
                throw new ArgumentNullException("process");

            if (process.MainWindowHandle == IntPtr.Zero)
                return null;

            AutomationElement element = AutomationElement.FromHandle(process.MainWindowHandle);
            if (element == null)
                return null;

            AutomationElement doc = element.FindFirst(TreeScope.Subtree, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document));
            if (doc == null)
                return null;

            return ((ValuePattern)doc.GetCurrentPattern(ValuePattern.Pattern)).Current.Value as string;
        }
        #endregion
        public APIFuncs()
        {
        }

    }
}
