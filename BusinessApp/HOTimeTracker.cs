using System;
using System.Collections.Generic;
using System.Data;
using System.Windows.Forms;
using System.Diagnostics;
using System.Management;
using System.Dynamic;

using VOWatcher.model;
using BusinessData.DataModel;
using BusinessService.Common;
using Dapper;
using TimeTrackerHO.model;


namespace VOWatcher
{
    public partial class HOTimeTracker : Form
    {
        #region Variables Declaration
        // Application tracking variables
        private static string appName;
        private static string prevAppKey;
        private static Dictionary<string, AppTimeData> appTimeData = new Dictionary<string, AppTimeData>();

        // Time tracking variables
        private DateTime lastActivityTime;
        private DateTime lastTrackingTime;
        private const int IDLE_THRESHOLD_SECONDS = 60;

        // Database connection
        private IDapper dapper;

        // UI and state management
        private bool allowShowDisplay = false;
        public static bool isPaused = false;
        private string dateParam;

        // Keep track of active application to properly handle idle time
        private string activeAppKey = string.Empty;

        // Track idle state between timer ticks
        private bool wasIdleLastTick = false;
        private DateTime idleStartTime;
        private Dictionary<string, double> accumulatedIdleTime = new Dictionary<string, double>();

        // Track last idle check to ensure we don't double count
        private DateTime lastIdleCheck;

        private CommonFunction cf = new CommonFunction();
        #endregion

        public HOTimeTracker()
        {
            dapper = new Dapperr();
            dateParam = DateTime.Now.ToString("yyyy-MM-dd");
            InitializeComponent();
            dataGridView1.AutoGenerateColumns = false;
            dataGridView2.AutoGenerateColumns = false;

            // Initialize tracking times
            lastActivityTime = DateTime.Now;
            lastTrackingTime = DateTime.Now;
            idleStartTime = DateTime.Now;
            lastIdleCheck = DateTime.Now;
            // Setup system tray context menu
            SetupSystemTrayMenu();
            // Setup calendar event handler
            monthCalendar1.DateSelected += MonthCalendar1_DateSelected;

            // Load initial data for today
            LoadActiveApps();
        }

        /// <summary>
        /// Handles calendar date selection change event
        /// </summary>
        private void MonthCalendar1_DateSelected(object sender, DateRangeEventArgs e)
        {
            try
            {
                // Update the date parameter to the selected date
                dateParam = e.Start.ToString("yyyy-MM-dd");

                // Update the label or status to show which date is being viewed
                UpdateDateDisplay(e.Start);

                // Load data for the selected date
                LoadActiveApps();

                // If viewing a past date, you might want to pause tracking
                // to avoid confusion (optional)
                DateTime selectedDate = e.Start.Date;
                DateTime today = DateTime.Now.Date;

                if (selectedDate < today)
                {
                    // Viewing past date - optionally show a status
                    this.Text = $"HOTimeTracker - Viewing {selectedDate.ToString("MMMM dd, yyyy")}";
                }
                else if (selectedDate == today)
                {
                    // Viewing today - normal operation
                    this.Text = "HOTimeTracker - Live Tracking";
                }
                else
                {
                    // Future date selected - show appropriate message
                    this.Text = $"HOTimeTracker - Future Date Selected";

                    // Clear data grids for future dates
                    dataGridView1.DataSource = null;
                    dataGridView2.DataSource = null;
                }
            }
            catch (Exception ex)
            {
               MessageBox.Show($"Error loading data for selected date: {ex.Message}",
                               "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Debug.WriteLine($"Error in MonthCalendar1_DateSelected: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the display to show which date is currently being viewed
        /// </summary>
        private void UpdateDateDisplay(DateTime selectedDate)
        {
            // If you have a label to show the current date being viewed, update it here
            // For example: lblCurrentDate.Text = selectedDate.ToString("MMMM dd, yyyy");

            // Update the form title to show current date
            DateTime today = DateTime.Now.Date;
            if (selectedDate.Date == today)
            {
                this.Text = "HOTimeTracker - Live Tracking (Today)";
            }
            else if (selectedDate.Date < today)
            {
                TimeSpan difference = today - selectedDate.Date;
                if (difference.Days == 1)
                {
                    this.Text = "HOTimeTracker - Yesterday";
                }
                else
                {
                    this.Text = $"HOTimeTracker - {selectedDate.ToString("MMM dd, yyyy")} ({difference.Days} days ago)";
                }
            }
            else
            {
                this.Text = $"HOTimeTracker - {selectedDate.ToString("MMM dd, yyyy")} (Future Date)";
            }
        }

        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(allowShowDisplay ? value : allowShowDisplay);
        }

        /// <summary>
        /// Method that converts bytes to its human readable value
        /// </summary>
        /// <param name="number"></param>
        /// <returns></returns>
        //public string BytesToReadableValue(long number)
        //{
        //    List<string> suffixes = new List<string> { " B", " KB", " MB", " GB", " TB", " PB" };

        //    for (int i = 0; i < suffixes.Count; i++)
        //    {
        //        long temp = number / (int)Math.Pow(1024, i + 1);

        //        if (temp == 0)
        //        {
        //            return (number / (int)Math.Pow(1024, i)) + suffixes[i];
        //        }
        //    }

        //    return number.ToString();
        //}

        /// <summary>
        /// Returns an Expando object with the description and username of a process from the process ID.
        /// </summary>
        /// <param name="processId"></param>
        /// <returns></returns>
        public ExpandoObject GetProcessExtraInformation(int processId)
        {
            // Query the Win32_Process
            string query = "Select * From Win32_Process Where ProcessID = " + processId;
            ManagementObjectSearcher searcher = new ManagementObjectSearcher(query);
            ManagementObjectCollection processList = searcher.Get();

            // Create a dynamic object to store some properties on it
            dynamic response = new ExpandoObject();
            response.Description = "";
            response.Username = "Unknown";

            foreach (ManagementObject obj in processList)
            {
                // Retrieve username 
                string[] argList = new string[] { string.Empty, string.Empty };
                int returnVal = Convert.ToInt32(obj.InvokeMethod("GetOwner", argList));
                if (returnVal == 0)
                {
                    // return Username
                    response.Username = argList[0];
                }

                // Retrieve process description if exists
                if (obj["ExecutablePath"] != null)
                {
                    try
                    {
                        FileVersionInfo info = FileVersionInfo.GetVersionInfo(obj["ExecutablePath"].ToString());
                        response.Description = info.FileDescription;
                    }
                    catch { }
                }
            }

            return response;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            TrackRunningAppTime();
            LoadActiveApps();
        }

        /// <summary>
        /// This method renders active processes of Windows and tracks their time
        /// </summary>
        public void TrackRunningAppTime()
        {
            try
            {
                // Get current active window info
                IntPtr hwnd = APIFuncs.getforegroundWindow();
                Int32 pid = APIFuncs.GetWindowProcessID(hwnd);
                Process process = Process.GetProcessById(pid);

                // Get application info
                appName = process.ProcessName;
                string appTitle = APIFuncs.ActiveApplTitle().Trim().Replace("\0", "");
                string appId = process.Id.ToString();

                // Skip certain windows
                if (appTitle == "System tray overflow window." || string.IsNullOrEmpty(appTitle))
                {
                    return;
                }

                // Get extra process info
                dynamic extraProcessInfo = GetProcessExtraInformation(process.Id);
                string programPath = APIFuncs.GetMainModuleFileName(process);

                // Get URL if browser
                string url = cf.GetBrowserUrl(process);

                // Create the unique app key using the modified method
                string currentAppKey = cf.CreateAppKey(appId, appName, appTitle);

                // Check if system is idle
                TimeSpan idleTime = APIFuncs.RetrieveIdleTime();
                bool isIdle = idleTime.TotalSeconds > IDLE_THRESHOLD_SECONDS;

                // Store current time for calculations
                DateTime currentTime = DateTime.Now;

                // Calculate time since last check to avoid double counting
                double timeSinceLastCheck = (currentTime - lastIdleCheck).TotalSeconds;
                lastIdleCheck = currentTime;

                // Handle idle time tracking state transitions
                if (isIdle && !wasIdleLastTick)
                {
                    // Transition from active to idle
                    idleStartTime = currentTime;
                    wasIdleLastTick = true;
                    Debug.WriteLine("Entered idle state at " + idleStartTime.ToString("HH:mm:ss"));
                }
                else if (!isIdle && wasIdleLastTick)
                {
                    // Transition from idle to active - calculate accumulated idle time
                    TimeSpan idleDuration = currentTime - idleStartTime;

                    // Cap idle duration to actual elapsed time to prevent overcounting
                    double idleSeconds = Math.Min(idleDuration.TotalSeconds, timeSinceLastCheck);

                    // Store the accumulated idle time for the previously active app
                    if (!string.IsNullOrEmpty(activeAppKey))
                    {
                        if (!accumulatedIdleTime.ContainsKey(activeAppKey))
                        {
                            accumulatedIdleTime[activeAppKey] = 0;
                        }
                        accumulatedIdleTime[activeAppKey] += idleSeconds;
                        Debug.WriteLine($"Added {idleSeconds:F1} seconds of idle time to app: {activeAppKey}");
                    }

                    wasIdleLastTick = false;
                    Debug.WriteLine("Exited idle state, was idle for " + idleDuration.TotalSeconds.ToString("F1") + " seconds");
                }
                else if (isIdle && wasIdleLastTick)
                {
                    // Continuing idle state - only add incremental idle time
                    if (!string.IsNullOrEmpty(activeAppKey))
                    {
                        if (!accumulatedIdleTime.ContainsKey(activeAppKey))
                        {
                            accumulatedIdleTime[activeAppKey] = 0;
                        }

                        // Only add the time since last check to prevent accumulating duplicate idle time
                        accumulatedIdleTime[activeAppKey] += timeSinceLastCheck;
                        Debug.WriteLine($"Added {timeSinceLastCheck:F1} seconds of continuing idle time to app: {activeAppKey}");
                    }
                }

                // If user activity detected, update last activity time
                if (!isIdle)
                {
                    lastActivityTime = currentTime;
                }

                // Record elapsed time since last tracking
                TimeSpan elapsedTime = currentTime - lastTrackingTime;

                // Handle application switching or update current app
                if (prevAppKey != currentAppKey)
                {
                    // Handle previous app if it exists
                    if (!string.IsNullOrEmpty(prevAppKey) && appTimeData.ContainsKey(prevAppKey))
                    {
                        UpdateAppTimeAndPersist(prevAppKey, elapsedTime, currentTime);
                    }

                    // Initialize new app if needed
                    if (!appTimeData.ContainsKey(currentAppKey))
                    {
                        // Use the new initialization method
                        InitializeNewApp(currentAppKey, process, appTitle, programPath, url, extraProcessInfo, currentTime);

                        // Create initial database record
                        CreateInitialDatabaseRecord(currentAppKey, currentTime);
                    }

                    // Update active app tracking 
                    activeAppKey = currentAppKey;
                    prevAppKey = currentAppKey;
                }
                else
                {
                    // Update the same app
                    UpdateAppTimeAndPersist(currentAppKey, elapsedTime, currentTime);
                }
                // Update last tracking time
                lastTrackingTime = currentTime;
            }
            catch (Exception ex)
            {
                // Log exception
                Debug.WriteLine($"Error in TrackRunningAppTime: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates application time data and persists to database
        /// </summary>
        private void UpdateAppTimeAndPersist(string appKey, TimeSpan elapsedTime, DateTime currentTime)
        {
            if (!appTimeData.ContainsKey(appKey))
                return;

            var appData = appTimeData[appKey];

            // Calculate idle time for this app
            double idleTimeIncrement = 0;

            // Apply accumulated idle time if available
            if (accumulatedIdleTime.ContainsKey(appKey) && accumulatedIdleTime[appKey] > 0)
            {
                idleTimeIncrement = accumulatedIdleTime[appKey];
                // Reset accumulated idle time after using it
                accumulatedIdleTime[appKey] = 0;

                Debug.WriteLine($"Applying {idleTimeIncrement:F1} seconds of accumulated idle time to {appKey}");
            }

            // Update time records - ensure idle time doesn't exceed total time
            appData.TotalTimeSeconds += elapsedTime.TotalSeconds;
            appData.IdleTimeSeconds += idleTimeIncrement;

            // Ensure idle time never exceeds total time
            appData.IdleTimeSeconds = Math.Min(appData.IdleTimeSeconds, appData.TotalTimeSeconds);

            appData.EndTime = currentTime;

            // Create parameters for database - use the accumulated totals
            var activeAppParams = new ActiveAppProcessPara
            {
                processid = appData.ProcessId,
                processname = appData.ProcessName,
                processusername = appData.ProcessUsername,
                appdescription = appData.AppDescription,
                apptitle = appData.AppTitle,
                appexepath = appData.AppExePath,
                url = appData.Url,
                endtime = currentTime,
                totaltime_secounds = appData.TotalTimeSeconds,  // Total accumulated time
                idletime_secounds = appData.IdleTimeSeconds     // Total accumulated idle time
            };

            // Persist to database - this will determine if it's an update or insert
            LogActiveApps(activeAppParams);

            Debug.WriteLine($"Updated {appKey}: Total={appData.TotalTimeSeconds:F1}s, Idle={appData.IdleTimeSeconds:F1}s, Active={appData.TotalTimeSeconds - appData.IdleTimeSeconds:F1}s");
        }

        /// <summary>
        /// Creates a unique key for an application
        /// </summary>
        //private string CreateAppKey(string appId, string appName, string appTitle)
        //{
        //    // Check if this is a browser application
        //    if (IsBrowserProcess(appName))
        //    {
        //        // For browsers, include appTitle to distinguish between tabs
        //        return $"{appId}|{appName}|{appTitle}";
        //    }
        //    else
        //    {
        //        // For other applications, just use appId and appName
        //        return $"{appId}|{appName}";
        //    }
        //}

        /// <summary>
        /// Gets browser URL based on browser type
        /// </summary>
        //private string GetBrowserUrl(Process process)
        //{
        //    string url = "";
        //    switch (process.ProcessName.ToLower())
        //    {
        //        case "chrome":
        //            url = APIFuncs.GetChromeUrl(process);
        //            break;
        //        case "msedge":
        //            url = APIFuncs.GetInternetExplorerUrl(process);
        //            break;
        //        case "firefox":
        //            url = APIFuncs.GetFirefoxUrl(process);
        //            break;
        //    }
        //    return url;
        //}

        /// <summary>
        /// Saves application usage data to database
        /// </summary>
        private void LogActiveApps(ActiveAppProcessPara model)
        {
            if (string.IsNullOrEmpty(model.apptitle) || model.apptitle.ToLower() == "unknown-apptitle")
                return;

            // Determine if this is a browser based on process name
            bool isBrowser = cf.IsBrowserProcess(model.processname);

            // Use different SQL queries based on application type
            string sql;
            var parameters = new DynamicParameters();

            if (isBrowser)
            {
                // For browsers, use composite key with apptitle to distinguish tabs
                sql = @"SELECT id, processid, processname, processusername, appdescription, apptitle, 
            appexepath, url, start_time, end_time, totaltime_secounds, idletime_secounds, 
            votrackerid, projectid, subprojectid, subprojectbranchid 
            FROM tbl_activeapplicactions 
            WHERE processid = @processid 
            AND processname = @processname 
            AND apptitle = @apptitle 
            AND date(start_time) = @dateParam           
            ORDER BY start_time DESC 
            LIMIT 1";

                parameters.Add("@processid", model.processid);
                parameters.Add("@processname", model.processname);
                parameters.Add("@apptitle", model.apptitle);
                parameters.Add("@dateParam", dateParam);
            }
            else
            {
                // For non-browsers, use only processid and processname for identification
                sql = @"SELECT id, processid, processname, processusername, appdescription, apptitle, 
            appexepath, url, start_time, end_time, totaltime_secounds, idletime_secounds, 
            votrackerid, projectid, subprojectid, subprojectbranchid 
            FROM tbl_activeapplicactions 
            WHERE processid = @processid 
            AND processname = @processname 
            AND date(start_time) = @dateParam           
            ORDER BY start_time DESC 
            LIMIT 1";

                parameters.Add("@processid", model.processid);
                parameters.Add("@processname", model.processname);
                parameters.Add("@dateParam", dateParam);
            }

            var data = dapper.Get<ActiveAppProcessModel>(sql, parameters, commandType: CommandType.Text);

            if (data != null)
            {
                // Calculate the time difference since the last update
                DateTime lastEndTime = Convert.ToDateTime(data.end_time);
                DateTime currentTime = model.endtime;

                // Only update if this is a continuous session (within reasonable time gap)
                TimeSpan timeSinceLastUpdate = currentTime - lastEndTime;

                // If more than 2 minutes have passed, consider it a new session
                // This prevents merging unrelated sessions
                if (timeSinceLastUpdate.TotalMinutes > 2)
                {
                    InsertNewRecord(model);
                    return;
                }

                // For updates, we need to recalculate time based on database start_time and current end_time
                DateTime dbStartTime = Convert.ToDateTime(data.start_time);
                double totalTimeFromDb = (currentTime - dbStartTime).TotalSeconds;

                // Get the accumulated idle time from our model
                double idleTimeFromModel = model.idletime_secounds;

                // Ensure idle time doesn't exceed total time
                double finalIdleTime = Math.Min(idleTimeFromModel, totalTimeFromDb);

                // Update the existing record
                var dbparams = new DynamicParameters();
                dbparams.Add("end_time", currentTime, DbType.DateTime);

                // Use recalculated total time and controlled idle time
                dbparams.Add("totaltime_secounds", totalTimeFromDb, DbType.Double);
                dbparams.Add("idletime_secounds", finalIdleTime, DbType.Double);
                dbparams.Add("id", data.id, DbType.Int32);

                // Update active flag based on activity status
                int activeFlag = isPaused ? 1 : 0;
                dbparams.Add("active", activeFlag, DbType.Int32);

                // Always update these fields regardless of application type
                dbparams.Add("url", model.url, DbType.String);
                dbparams.Add("appexepath", model.appexepath, DbType.String);
                dbparams.Add("apptitle", model.apptitle, DbType.String);

                // Use a single update SQL query for all app types
                string updateSql = @"UPDATE tbl_activeapplicactions 
            SET end_time = @end_time, 
                totaltime_secounds = @totaltime_secounds, 
                idletime_secounds = @idletime_secounds, 
                active = @active,
                url = @url,
                appexepath = @appexepath,
                apptitle = @apptitle
            WHERE id = @id";

                dapper.Insert<int>(updateSql, dbparams, CommandType.Text);

                // Debug information
                Debug.WriteLine($"Updated existing record ID {data.id} for {model.processname} - DB Start: {dbStartTime}, End: {currentTime}, Total: {totalTimeFromDb:F2}s, Idle: {finalIdleTime:F2}s, Title: {model.apptitle}");
            }
            else
            {
                // No existing record found, insert new record
                InsertNewRecord(model);
            }
        }

        /// <summary>
        /// Determines if the process is a known browser
        /// </summary>
        //private bool IsBrowserProcess(string processName)
        //{
        //    if (string.IsNullOrEmpty(processName))
        //        return false;

        //    // Convert to lowercase for case-insensitive comparison
        //    string lowerProcessName = processName.ToLower();

        //    // List of known browser process names
        //    return lowerProcessName == "chrome" ||
        //           lowerProcessName == "msedge" ||
        //           lowerProcessName == "firefox" ||
        //           lowerProcessName == "opera" ||
        //           lowerProcessName == "brave" ||
        //           lowerProcessName == "iexplore" ||
        //           lowerProcessName == "safari" ||
        //           lowerProcessName.Contains("browser");
        //}

        /// <summary>
        /// Helper method to insert a new record
        /// </summary>
        private void InsertNewRecord(ActiveAppProcessPara model)
        {
            // For new records, always start with current time as start_time
            DateTime startTime = DateTime.Now;
            DateTime endTime = model.endtime;

            // For new records, total time should be minimal (just the current timer interval)
            // Since this is a new record, we'll start with near-zero time
            double totalTimeSeconds = (endTime - startTime).TotalSeconds;

            // Ensure we have at least some minimal time (timer interval)
            if (totalTimeSeconds <= 0)
            {
                totalTimeSeconds = 0; 
            }

            // For new records, idle time should always be 0
            double idleTimeSeconds = 0;

            var dbparams = new DynamicParameters();
            dbparams.Add("processid", model.processid, DbType.Int32);
            dbparams.Add("processname", model.processname, DbType.String);
            dbparams.Add("processusername", model.processusername, DbType.String);
            dbparams.Add("appdescription", model.appdescription, DbType.String);
            dbparams.Add("apptitle", model.apptitle, DbType.String);
            dbparams.Add("appexepath", model.appexepath, DbType.String);
            dbparams.Add("url", model.url, DbType.String);
            dbparams.Add("start_time", startTime, DbType.DateTime);
            dbparams.Add("end_time", endTime, DbType.DateTime);

            // Use calculated values - NOT the model values
            dbparams.Add("totaltime_secounds", totalTimeSeconds, DbType.Double);
            dbparams.Add("idletime_secounds", idleTimeSeconds, DbType.Double);
            dbparams.Add("active", 0, DbType.Int32);

            string insertSql = @"INSERT INTO tbl_activeapplicactions(
                        processid, processname, processusername, appdescription, apptitle, 
                        appexepath, url, start_time, end_time, totaltime_secounds, 
                        idletime_secounds, active) 
                        VALUES(
                        @processid, @processname, @processusername, @appdescription, @apptitle, 
                        @appexepath, @url, @start_time, @end_time, @totaltime_secounds, 
                        @idletime_secounds, @active)";

            dapper.Insert<int>(insertSql, dbparams, CommandType.Text);

            Debug.WriteLine($"Inserted new record for {model.processname} - Start: {startTime:HH:mm:ss}, End: {endTime:HH:mm:ss}, Total: {totalTimeSeconds:F2}s, Idle: {idleTimeSeconds:F2}s");
        }

        /// <summary>
        /// Alternative method to handle new app initialization better
        /// </summary>
        private void InitializeNewApp(string appKey, Process process, string appTitle, string programPath, string url, dynamic extraProcessInfo, DateTime currentTime)
        {
            // When a new app becomes active, initialize it with the current time as start time
            appTimeData[appKey] = new AppTimeData
            {
                ProcessId = process.Id,
                ProcessName = process.ProcessName,
                ProcessUsername = extraProcessInfo.Username,
                AppDescription = extraProcessInfo.Description,
                AppTitle = appTitle,
                AppExePath = programPath,
                Url = url,
                StartTime = currentTime, // Use current time as start time
                EndTime = currentTime,   // Initially same as start time
                TotalTimeSeconds = 0,    // Start with 0 seconds
                IdleTimeSeconds = 0      // Start with 0 idle time
            };

            // Initialize accumulated idle time for the new app
            accumulatedIdleTime[appKey] = 0;

            Debug.WriteLine($"Initialized new app: {appKey} at {currentTime:HH:mm:ss}");
        }

        /// <summary>
        /// Updated method to create new database record with proper time calculation
        /// </summary>
        private void CreateInitialDatabaseRecord(string appKey, DateTime currentTime)
        {
            if (!appTimeData.ContainsKey(appKey))
                return;

            var appData = appTimeData[appKey];

            // For the initial database record, use minimal time
            // The time will be properly accumulated as the app continues to be used
            var activeAppParams = new ActiveAppProcessPara
            {
                processid = appData.ProcessId,
                processname = appData.ProcessName,
                processusername = appData.ProcessUsername,
                appdescription = appData.AppDescription,
                apptitle = appData.AppTitle,
                appexepath = appData.AppExePath,
                url = appData.Url,
                endtime = currentTime,
                totaltime_secounds = 0,  // Start with 0 for new records
                idletime_secounds = 0    // Start with 0 idle time for new records
            };

            // Insert the initial record
            InsertNewRecord(activeAppParams);
        }
        /// <summary>
        /// Loads active applications data from database
        /// </summary>
        private void LoadActiveApps()
        {
            try
            {
                string datepara = monthCalendar1.SelectionRange.Start.ToString("yyyy-MM-dd");

                // Update the SQL query to properly format time values and fix summarization
                string appSummarySql = @"SELECT 
                                processname, 
                                strftime('%H:%M:%S', SUM(totaltime_secounds), 'unixepoch') totaltimesecounds, 
                                strftime('%H:%M:%S', SUM(idletime_secounds), 'unixepoch') idletimesecounds, 
                                strftime('%H:%M:%S', (SUM(totaltime_secounds) - SUM(idletime_secounds)), 'unixepoch') actualsecounds 
                                FROM (
                                    SELECT 
                                        CASE 
                                            WHEN processname = 'chrome' OR processname LIKE '%chrome%' THEN 'Google Chrome'
                                            WHEN processname LIKE '%visual studio%' THEN 'Microsoft Visual Studio 2022'
                                            WHEN appdescription <> '' THEN appdescription
                                            ELSE processname
                                        END as processname,
                                        totaltime_secounds,
                                        idletime_secounds
                                    FROM tbl_activeapplicactions
                                    WHERE totaltime_secounds > 0 
                                    AND date(start_time) = @datepara
                                ) 
                                GROUP BY processname
                                ORDER BY SUM(totaltime_secounds) DESC";

                var parameters = new DynamicParameters();
                parameters.Add("@datepara", datepara);

                List<ActiveTrackerAppModel> applicationLogs = dapper.GetAll<ActiveTrackerAppModel>(appSummarySql, parameters, CommandType.Text);

                if (applicationLogs != null && applicationLogs.Count > 0)
                {
                    dataGridView1.DataSource = applicationLogs;

                    // Calculate and display total time for the day
                    DisplayDaySummary(applicationLogs, datepara);
                }
                else
                {
                    dataGridView1.DataSource = null;
                    // Show message if no data found
                    DisplayNoDataMessage(datepara);
                }

                // Update the SQL query for detailed application data as well
                string appDetailSql = @"SELECT 
                               processid, 
                               CASE 
                                  WHEN apptitle LIKE '%.exe%' THEN processname
                                  ELSE apptitle 
                               END processname, 
                               strftime('%H:%M:%S', totaltime_secounds, 'unixepoch') totaltimesecounds, 
                               strftime('%H:%M:%S', idletime_secounds, 'unixepoch') idletimesecounds, 
                               strftime('%H:%M:%S', (totaltime_secounds - idletime_secounds), 'unixepoch') actualsecounds,
                               strftime('%H:%M', start_time) start_time,
                               strftime('%H:%M', end_time) end_time
                               FROM tbl_activeapplicactions 
                               WHERE totaltime_secounds > 0  
                               AND date(start_time) = @datepara 
                               ORDER BY start_time DESC";

                List<ActiveTrackerAppModel> activeApps2 = dapper.GetAll<ActiveTrackerAppModel>(appDetailSql, parameters, CommandType.Text);

                if (activeApps2 != null && activeApps2.Count > 0)
                {
                    dataGridView2.DataSource = activeApps2;
                }
                else
                {
                    dataGridView2.DataSource = null;
                }
            }
            catch (Exception ex)
            {
               MessageBox.Show($"Error loading application data: {ex.Message}",
                               "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Debug.WriteLine($"Error in LoadActiveApps: {ex.Message}");
            }
        }

        /// <summary>
        /// Model class for day summary data
        /// </summary>
        public class DaySummaryModel
        {
            public double total_seconds { get; set; }
            public double idle_seconds { get; set; }
            public double active_seconds { get; set; }
        }

        /// <summary>
        /// Displays summary information for the selected day
        /// </summary>
        private void DisplayDaySummary(List<ActiveTrackerAppModel> applicationLogs, string date)
        {
            try
            {
                if (applicationLogs == null || applicationLogs.Count == 0)
                    return;

                // Calculate total time for the day
                string totalTimeSql = @"SELECT 
                       SUM(totaltime_secounds) as total_seconds,
                       SUM(idletime_secounds) as idle_seconds,
                       SUM(totaltime_secounds - idletime_secounds) as active_seconds
                       FROM tbl_activeapplicactions 
                       WHERE date(start_time) = @datepara";

                var parameters = new DynamicParameters();
                parameters.Add("@datepara", date);

                // Use the same pattern as other methods in your code
                var dayInfo = dapper.Get<DaySummaryModel>(totalTimeSql, parameters, commandType: CommandType.Text);

                if (dayInfo != null)
                {
                    double totalSeconds = dayInfo.total_seconds;
                    double idleSeconds = dayInfo.idle_seconds;
                    double activeSeconds = dayInfo.active_seconds;

                    string totalTime = cf.SecondsToTime(totalSeconds);
                    string idleTime = cf.SecondsToTime(idleSeconds);
                    string activeTime = cf.SecondsToTime(activeSeconds);

                    // Update the form title with summary
                    DateTime selectedDate = DateTime.Parse(date);
                    string dateDisplay = selectedDate.Date == DateTime.Now.Date ? "Today" : selectedDate.ToString("MMM dd");
                    this.Text = $"HOTimeTracker - {dateDisplay} (Total: {totalTime}, Active: {activeTime})";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error calculating day summary: {ex.Message}");
            }
        }

        /// <summary>
        /// Displays a message when no data is found for the selected date
        /// </summary>
        private void DisplayNoDataMessage(string date)
        {
            DateTime selectedDate = DateTime.Parse(date);

            if (selectedDate.Date == DateTime.Now.Date)
            {
                this.Text = "HOTimeTracker - Today (No activity recorded yet)";
            }
            else if (selectedDate.Date > DateTime.Now.Date)
            {
                this.Text = $"HOTimeTracker - {selectedDate.ToString("MMM dd, yyyy")} (Future Date)";
            }
            else
            {
                this.Text = $"HOTimeTracker - {selectedDate.ToString("MMM dd, yyyy")} (No activity recorded)";
            }
        }

        // Update the existing HOTimeTracker_Activated event
        private void HOTimeTracker_Activated(object sender, EventArgs e)
        {
            // Don't automatically change the date when form is activated
            // Allow user to continue viewing the selected date
            isPaused = true;
            timer1.Enabled = false;

            // Only load data if needed
            if (dataGridView1.DataSource == null)
            {
                LoadActiveApps();
            }
        }

        // Add a method to reset to today's view
        public void ResetToToday()
        {
            dateParam = DateTime.Now.ToString("yyyy-MM-dd");
            monthCalendar1.SetDate(DateTime.Now);
            LoadActiveApps();
            isPaused = false;
            timer1.Enabled = true;
        }

        //private void HOTimeTracker_Activated(object sender, EventArgs e)
        //{
        //    dateParam = monthCalendar1.TodayDate.ToString("yyyy-MM-dd");
        //    monthCalendar1.SetDate(Convert.ToDateTime(dateParam));
        //    isPaused = true;
        //    timer1.Enabled = false;
        //}

        private void HOTimeTracker_Deactivate(object sender, EventArgs e)
        {
            isPaused = false;
            timer1.Enabled = true;
        }

        //private void notifyIcon1_MouseClick(object sender, MouseEventArgs e)
        //{
        //    if (e.Button == System.Windows.Forms.MouseButtons.Left)
        //    {
        //        allowShowDisplay = true;
        //        this.Visible = !this.Visible;
        //    }
        //}

        //private void HOTimeTracker_FormClosing(object sender, FormClosingEventArgs e)
        //{
        //    isPaused = false;
        //    allowShowDisplay = false;
        //    this.Visible = !this.Visible;
        //    e.Cancel = true;
        //}

        /// <summary>
        /// Sets up the context menu for the system tray icon
        /// </summary>
        private void SetupSystemTrayMenu()
        {
            // Create context menu for the notify icon
            ContextMenuStrip contextMenu = new ContextMenuStrip();

            // Add "Show/Hide" menu item
            ToolStripMenuItem showHideItem = new ToolStripMenuItem();
            showHideItem.Text = "Show/Hide Window";
            showHideItem.Click += ShowHideItem_Click;
            contextMenu.Items.Add(showHideItem);

            // Add separator
            contextMenu.Items.Add(new ToolStripSeparator());

            // Add "Pause/Resume Tracking" menu item
            ToolStripMenuItem pauseResumeItem = new ToolStripMenuItem();
            pauseResumeItem.Text = isPaused ? "Resume Tracking" : "Pause Tracking";
            pauseResumeItem.Click += PauseResumeItem_Click;
            contextMenu.Items.Add(pauseResumeItem);

            // Add separator
            contextMenu.Items.Add(new ToolStripSeparator());

            // Add "Close Application" menu item
            ToolStripMenuItem closeItem = new ToolStripMenuItem();
            closeItem.Text = "Close Application";
            closeItem.Click += CloseItem_Click;
            contextMenu.Items.Add(closeItem);

            // Assign context menu to notify icon
            notifyIcon1.ContextMenuStrip = contextMenu;

            // Set tooltip text
            notifyIcon1.Text = "HOTimeTracker - Time Tracking Application";
        }

        /// <summary>
        /// Handles the Show/Hide menu item click
        /// </summary>
        private void ShowHideItem_Click(object sender, EventArgs e)
        {
            allowShowDisplay = true;
            this.Visible = !this.Visible;
            if (this.Visible)
            {
                this.WindowState = FormWindowState.Normal;
                this.BringToFront();
            }
        }

        /// <summary>
        /// Handles the Pause/Resume menu item click
        /// </summary>
        private void PauseResumeItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem menuItem = sender as ToolStripMenuItem;

            isPaused = !isPaused;
            timer1.Enabled = !isPaused;

            // Update menu text
            menuItem.Text = isPaused ? "Resume Tracking" : "Pause Tracking";

            // Update notify icon tooltip
            notifyIcon1.Text = isPaused ?
                "HOTimeTracker - Tracking Paused" :
                "HOTimeTracker - Tracking Active";

            // Show balloon tip notification
            notifyIcon1.BalloonTipTitle = "HOTimeTracker";
            notifyIcon1.BalloonTipText = isPaused ?
                "Time tracking has been paused" :
                "Time tracking has been resumed";
            notifyIcon1.BalloonTipIcon = ToolTipIcon.Info;
            notifyIcon1.ShowBalloonTip(2000);
        }

        /// <summary>
        /// Handles the Close Application menu item click
        /// </summary>
        private void CloseItem_Click(object sender, EventArgs e)
        {
            // Show confirmation dialog
            DialogResult result = MessageBox.Show(
                "Are you sure you want to close the application?\n\nThis will stop time tracking.",
                "Confirm Close",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);

            if (result == DialogResult.Yes)
            {
                // Perform cleanup before closing
                CleanupBeforeClose();

                // Actually close the application
                notifyIcon1.Visible = false;
                Application.Exit();
            }
        }

        /// <summary>
        /// Performs cleanup operations before closing the application
        /// </summary>
        private void CleanupBeforeClose()
        {
            try
            {
                // Stop the timer
                timer1.Enabled = false;

                // Save any pending data for the current active app
                if (!string.IsNullOrEmpty(activeAppKey) && appTimeData.ContainsKey(activeAppKey))
                {
                    DateTime currentTime = DateTime.Now;
                    TimeSpan elapsedTime = currentTime - lastTrackingTime;
                    UpdateAppTimeAndPersist(activeAppKey, elapsedTime, currentTime);
                }

                // Hide the notify icon
                notifyIcon1.Visible = false;

                // You can add any other cleanup operations here
                // such as saving configuration, clearing temporary files, etc.
            }
            catch (Exception ex)
            {
                // Log any cleanup errors
                Debug.WriteLine($"Error during cleanup: {ex.Message}");
            }
        }

        // Update the existing FormClosing event to prevent accidental closure
        private void HOTimeTracker_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Only prevent closure if it's not a programmatic close (from our Close menu)
            if (e.CloseReason == CloseReason.UserClosing)
            {
                isPaused = false;
                allowShowDisplay = false;
                this.Visible = false;
                e.Cancel = true; // Prevent actual closure, just hide the window

                // Show balloon tip to inform user about system tray
                notifyIcon1.BalloonTipTitle = "HOTimeTracker";
                notifyIcon1.BalloonTipText = "Application minimized to system tray. Right-click the tray icon to access options.";
                notifyIcon1.BalloonTipIcon = ToolTipIcon.Info;
                notifyIcon1.ShowBalloonTip(3000);
            }
        }

        // Update the existing mouse click event to be more specific
        private void notifyIcon1_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // Double-click to show/hide window (single click is reserved for context menu)
                allowShowDisplay = true;
                this.Visible = !this.Visible;
                if (this.Visible)
                {
                    this.WindowState = FormWindowState.Normal;
                    this.BringToFront();
                }
            }
        }

        // Add a double-click event for easier access to show/hide
        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                allowShowDisplay = true;
                this.Visible = !this.Visible;
                if (this.Visible)
                {
                    this.WindowState = FormWindowState.Normal;
                    this.BringToFront();
                }
            }
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void groupBox1_Enter(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {

        }

        /// <summary>
        /// Converts seconds to formatted time string
        /// </summary>
        //public static string SecondsToTime(double secs)
        //{
        //    TimeSpan t = TimeSpan.FromSeconds(secs);
        //    return string.Format("{0:D2}:{1:D2}:{2:D2}",
        //        (int)t.TotalHours,
        //        t.Minutes,
        //        t.Seconds);
        //}
    }
}
