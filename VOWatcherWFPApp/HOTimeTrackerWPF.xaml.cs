using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Dynamic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using BusinessData.DataModel;
using BusinessService.Common;
using Dapper;
using System.ComponentModel;
using System.Windows.Forms; // For NotifyIcon which is still used from System.Windows.Forms
using System.Linq;
using System.Collections.ObjectModel;
using VOWatcherWFPApp.model;
using VOWatcherWFPApp.Services;
using System.Management;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Data;
using System.Runtime.CompilerServices;

namespace VOWatcherWFPApp
{
    /// <summary>
    /// Interaction logic for HOTimeTrackerWPF.xaml
    /// </summary>
    public partial class HOTimeTrackerWPF : Window, INotifyPropertyChanged
    {
        #region Variables Declaration
        private static string appName;
        private static string prevAppKey;
        private static Dictionary<string, AppTimeData> appTimeData = new Dictionary<string, AppTimeData>();
        private DateTime lastActivityTime;
        private DateTime lastTrackingTime;
        private const int IDLE_THRESHOLD_SECONDS = 60;
        private IDapper dapper;
        //private bool allowShowDisplay = false;
        public static bool isPaused = false;
        private string dateParam;
        private string activeAppKey = string.Empty;
        private bool wasIdleLastTick = false;
        private DateTime idleStartTime;
        private Dictionary<string, double> accumulatedIdleTime = new Dictionary<string, double>();
        private DateTime lastIdleCheck;
        private CommonFunction cf = new CommonFunction();
        private NotifyIcon notifyIcon;
        private DispatcherTimer timer;
        private DispatcherTimer timer1;

        

        // Create a flag to track if closing was initiated by the menu item
        private bool isExitMenuItem = false;
        // Observable collections for the DataGrids
        private ObservableCollection<ActiveTrackerAppModel> applicationLogs = new ObservableCollection<ActiveTrackerAppModel>();
        private ObservableCollection<ActiveTrackerAppModel> detailedAppLogs = new ObservableCollection<ActiveTrackerAppModel>();

        public ObservableCollection<ActiveTrackerAppModel> ApplicationLogs
        {
            get { return applicationLogs; }
            set
            {
                applicationLogs = value;
                OnPropertyChanged(nameof(ApplicationLogs));
            }
        }

        public ObservableCollection<ActiveTrackerAppModel> DetailedAppLogs
        {
            get { return detailedAppLogs; }
            set
            {
                detailedAppLogs = value;
                OnPropertyChanged(nameof(DetailedAppLogs));
            }
        }

        #endregion

        #region INotifyPropertyChanged Implementation
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Modify your WindowTitle property to use proper binding
        private string _windowTitle = "Health Office Time Tracker";
        public string WindowTitle
        {
            get { return _windowTitle; }
            set
            {
                if (_windowTitle != value)
                {
                    _windowTitle = value;
                    OnPropertyChanged(nameof(WindowTitle));
                }
            }
        }

        #endregion

        #region Disable Windows form button
        //Windows button
        private const int GWL_STYLE = -16;
        private const int WS_MAXIMIZEBOX = 0x10000;
        private const int WS_SYSMENU = 0x80000;
        // Methods to disable the Close and Maximize buttons
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);
       
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

        #endregion
        public HOTimeTrackerWPF()
        {
            InitializeComponent();

            DataContext = this;
            // Set initial window title
            WindowTitle = "Health Office Time Tracker";

            dapper = new Dapperr();
            dateParam = DateTime.Now.ToString("yyyy-MM-dd");

            lastActivityTime = DateTime.Now;
            lastTrackingTime = DateTime.Now;
            idleStartTime = DateTime.Now;
            lastIdleCheck = DateTime.Now;

            // Set initial calendar selection to today
            monthCalendar1.DisplayDate = DateTime.Now;
            monthCalendar1.SelectedDate = DateTime.Now;

            // Register the date selection event
            monthCalendar1.SelectedDatesChanged += monthCalendar1_SelectedDatesChanged;

            // Handle window closed event
            //Closing += HOTimeTrackerWPF_Deactivated;
            SourceInitialized += HOTimeTrackerWPF_SourceInitialized;
            // remove window close button
            //Loaded += HOTimeTrackerWPF_Loaded;
            // Activate/Deactivate events
            Activated += HOTimeTrackerWPF_Activated;
            Deactivated += HOTimeTrackerWPF_Deactivated;

            //this.Hide();

            SetupSystemTrayIcon();
            SetupTimer();
            // Initial data load
            LoadActiveApps();
            // Configure the DataGrid grouping
            ConfigureDataGridGrouping();

            dataGridView1.ItemsSource = ApplicationLogs;
            dataGridView2.ItemsSource = DetailedAppLogs;
        }

        private void HOTimeTrackerWPF_SourceInitialized(object sender, EventArgs e)
        {
            // Get the window handle
            IntPtr hwnd = new WindowInteropHelper(this).Handle;

            // Change the window style
            int currentStyle = GetWindowLong(hwnd, GWL_STYLE);

            // To disable the maximize button:
            SetWindowLong(hwnd, GWL_STYLE, currentStyle & ~WS_MAXIMIZEBOX);

            // To disable both maximize and close buttons:
            // SetWindowLong(hwnd, GWL_STYLE, currentStyle & ~(WS_MAXIMIZEBOX | WS_SYSMENU));

        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Disable maximize button only
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int currentStyle = GetWindowLong(hwnd, GWL_STYLE);
            SetWindowLong(hwnd, GWL_STYLE, currentStyle & ~WS_MAXIMIZEBOX);

            // Handle the closing event to prevent the window from closing
            this.Closing += (s, args) =>
            {
                this.Hide();
                args.Cancel = isExitMenuItem ? false : true; // This prevents the window from closing
            };
        }
        private void HOTimeTrackerWPF_Loaded(object sender, RoutedEventArgs e)
        {
            this.Hide(); // completely hides window
        }

        private void monthCalendar1_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (monthCalendar1.SelectedDate.HasValue)
                {
                    DateTime selectedDate = monthCalendar1.SelectedDate.Value;
                    dateParam = selectedDate.ToString("yyyy-MM-dd");
                    UpdateDateDisplay(selectedDate);
                    LoadActiveApps();

                    DateTime today = DateTime.Now.Date;
                    if (selectedDate < today)
                    {
                        WindowTitle = $"Health Office Time Tracker - Viewing {selectedDate.ToString("MMMM dd, yyyy")}";
                    }
                    else if (selectedDate == today)
                    {
                        WindowTitle = "Health Office Time Tracker - Live Tracking";
                    }
                    else
                    {
                        WindowTitle = $"Health Office Time Tracker - Future Date Selected";
                        ApplicationLogs.Clear();
                        DetailedAppLogs.Clear();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading data for selected date: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateDateDisplay(DateTime selectedDate)
        {
            DateTime today = DateTime.Now.Date;
            if (selectedDate.Date == today)
            {
                WindowTitle = "Health Office Time Tracker - Live Tracking (Today)";
            }
            else if (selectedDate.Date < today)
            {
                TimeSpan difference = today - selectedDate.Date;
                if (difference.Days == 1)
                {
                    WindowTitle = "Health Office Time Tracker - Yesterday";
                }
                else
                {
                    WindowTitle = $"Health Office Time Tracker - {selectedDate.ToString("MMM dd, yyyy")} ({difference.Days} days ago)";
                }
            }
            else
            {
                WindowTitle = $"Health Office Time Tracker - {selectedDate.ToString("MMM dd, yyyy")} (Future Date)";
            }
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                this.Hide();
                if (notifyIcon != null)
                {
                    notifyIcon.BalloonTipTitle = "HOTimeTrackerWPF";
                    notifyIcon.BalloonTipText = "Application minimized to system tray. Double-click to restore.";
                    notifyIcon.ShowBalloonTip(3000);
                }
            }

            base.OnStateChanged(e);
        }
        private void HOTimeTrackerWPF_Deactivated(object sender, EventArgs e)
        {
            isPaused = false;
            timer.Start();
        }

        private void HOTimeTrackerWPF_Activated(object sender, EventArgs e)
        {
            isPaused = true;
            timer.Stop();
            if (ApplicationLogs.Count == 0)
            {
                LoadActiveApps();
            }
        }


        private void Timer_Tick(object sender, EventArgs e)
        {
            TrackRunningAppTime();
            LoadActiveApps();
        }
        private void Timer_Tick1(object sender, EventArgs e)
        {
            // Update UI on each tick
            label3.Content = DateTime.Now.ToString("HH:mm:ss");
        }

        private void ConfigureDataGridGrouping()
        {
            // Create a collection view for the DetailedAppLogs collection
            var view = CollectionViewSource.GetDefaultView(DetailedAppLogs);

            // Set the grouping property to AppDescription
            var groupDescription = new PropertyGroupDescription("appdescription");
            view.GroupDescriptions.Add(groupDescription);

            // Apply the grouped view to the DataGrid
            dataGridView2.ItemsSource = view;

            // Optional: Configure group style to customize appearance
            GroupStyle groupStyle = new GroupStyle();

            // Create a header template for the group
            FrameworkElementFactory headerPanel = new FrameworkElementFactory(typeof(StackPanel));
            headerPanel.SetValue(StackPanel.OrientationProperty, System.Windows.Controls.Orientation.Horizontal);

            FrameworkElementFactory textBlock = new FrameworkElementFactory(typeof(TextBlock));
            textBlock.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Name"));
            textBlock.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            textBlock.SetValue(TextBlock.MarginProperty, new Thickness(5, 0, 0, 0));

            headerPanel.AppendChild(textBlock);

            DataTemplate headerTemplate = new DataTemplate();
            headerTemplate.VisualTree = headerPanel;

            groupStyle.HeaderTemplate = headerTemplate;

            // Add the GroupStyle to the DataGrid
            dataGridView2.GroupStyle.Add(groupStyle);
        }
        private void SetupSystemTrayIcon()
        {
            // Create context menu for system tray icon
            ContextMenuStrip contextMenu = new ContextMenuStrip();

            // Show/Hide menu item
            ToolStripMenuItem showHideItem = new ToolStripMenuItem();
            showHideItem.Text = "Show/Hide Window";
            showHideItem.Click += (s, e) =>
            {
                if (Visibility == Visibility.Visible)
                {
                    Hide();
                }
                else
                {
                    Show();
                    WindowState = WindowState.Normal;
                    Activate();
                }
            };
            contextMenu.Items.Add(showHideItem);
            contextMenu.Items.Add(new ToolStripSeparator());

            // Pause/Resume menu item
            ToolStripMenuItem pauseResumeItem = new ToolStripMenuItem();
            pauseResumeItem.Text = isPaused ? "Resume Tracking" : "Pause Tracking";
            pauseResumeItem.Click += (s, e) =>
            {
                isPaused = !isPaused;
                timer.IsEnabled = !isPaused;
                pauseResumeItem.Text = isPaused ? "Resume Tracking" : "Pause Tracking";

                // Update notify icon text and show balloon tip
                notifyIcon.Text = isPaused ? "Health Office Time Tracker - Tracking Paused" : "Health Office Time Tracker - Tracking Active";
                notifyIcon.BalloonTipTitle = "HOTimeTracker";
                notifyIcon.BalloonTipText = isPaused ? "Time tracking has been paused" : "Time tracking has been resumed";
                notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
                notifyIcon.ShowBalloonTip(2000);
            };
            contextMenu.Items.Add(pauseResumeItem);
            contextMenu.Items.Add(new ToolStripSeparator());

            // Exit application menu item
            ToolStripMenuItem exitItem = new ToolStripMenuItem();
            exitItem.Text = "Close Application";
            exitItem.Click += new EventHandler(HOTimeTrackerWPF_Closing);
            contextMenu.Items.Add(exitItem);

            // Create notify icon
            notifyIcon = new NotifyIcon
            {
                //Icon = System.Drawing.SystemIcons.Application,
                // Use the same icon as the window for consistency
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location),
                // Or use the specific icon file: new Icon(AppDomain.CurrentDomain.BaseDirectory + "timetracker.ico"),
                Text = "Health Office Time Tracker - Time Tracking Application",
                Visible = true,
                ContextMenuStrip = contextMenu
            };

            // Register mouse click events
            notifyIcon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    if (Visibility != Visibility.Visible)
                    {
                        Show();
                        WindowState = WindowState.Normal;
                        Activate();
                        LoadActiveApps();
                    }
                    else
                    {
                        if (WindowState == WindowState.Minimized)
                        {
                            WindowState = WindowState.Normal;
                            Activate();
                        }
                        else
                        {
                            Focus();
                        }
                    }
                }
            };

            // Double-click to show app
            notifyIcon.MouseDoubleClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    Show();
                    WindowState = WindowState.Normal;
                    Activate();
                    LoadActiveApps();
                }
            };
        }
        private void HOTimeTrackerWPF_Closing(object sender, EventArgs e)
        {
            // Ask user for confirmation
            MessageBoxResult result = System.Windows.MessageBox.Show(
                "Are you sure you want to close the application?\n\nThis will stop time tracking.",
                "Confirm Close",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                isExitMenuItem = true;
                // Clean up resources
                CleanupBeforeClose();

                // Dispose of the notify icon to remove it from the system tray
                if (notifyIcon != null)
                {
                    notifyIcon.Dispose();
                    this.Close();
                }
            }
            else
            {
                // Cancel the closing event
                //e.Cancel = true;

                // Minimize to system tray instead
                this.WindowState = WindowState.Minimized;
                this.Hide();
            }
        }

        private void SetupTimer()
        {
            // Create and configure the timer for tracking app usage
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(5); // Update every 5 seconds
            timer.Tick += Timer_Tick;
            timer.Start();

            timer1 = new DispatcherTimer();
            timer1.Interval = TimeSpan.FromSeconds(1);
            timer1.Tick += Timer_Tick1;
            timer1.Start();
            // Initial tracking to get the first app
            TrackRunningAppTime();
        }
        private void CleanupBeforeClose()
        {
            // Stop the timer to prevent further tracking
            if (timer != null && timer.IsEnabled)
            {
                timer.Stop();
            }

            // Save any final app time data
            if (!string.IsNullOrEmpty(activeAppKey) && appTimeData.ContainsKey(activeAppKey))
            {
                var appData = appTimeData[activeAppKey];
                var activeAppParams = new ActiveAppProcessPara
                {
                    processid = appData.ProcessId,
                    processname = appData.ProcessName,
                    processusername = appData.ProcessUsername,
                    appdescription = appData.AppDescription,
                    apptitle = appData.AppTitle,
                    appexepath = appData.AppExePath,
                    url = appData.Url,
                    endtime = DateTime.Now,
                    totaltime_secounds = appData.TotalTimeSeconds,
                    idletime_secounds = appData.IdleTimeSeconds
                };

                // Update the database with the latest app usage
                LogActiveApps(activeAppParams);
            }
        }
        private void LoadActiveApps()
        {
            try
            {
                string datepara = monthCalendar1.SelectedDate.HasValue
                    ? monthCalendar1.SelectedDate.Value.ToString("yyyy-MM-dd")
                    : DateTime.Now.ToString("yyyy-MM-dd");

                string appSummarySql = @"SELECT processname, 
                                strftime('%H:%M:%S', SUM(totaltime_secounds), 'unixepoch') totaltimesecounds, 
                                strftime('%H:%M:%S', SUM(idletime_secounds), 'unixepoch') idletimesecounds, 
                                strftime('%H:%M:%S', (SUM(totaltime_secounds) - SUM(idletime_secounds)), 'unixepoch') actualsecounds 
                                FROM (SELECT CASE 
                                            WHEN processname = 'chrome' OR processname LIKE '%chrome%' THEN 'Google Chrome'
                                            WHEN processname LIKE '%visual studio%' THEN 'Microsoft Visual Studio 2022'
                                            WHEN appdescription <> '' THEN appdescription
                                            ELSE processname
                                        END as processname, totaltime_secounds, idletime_secounds
                                    FROM tbl_activeapplicactions WHERE totaltime_secounds > 0 AND date(start_time) = @datepara) 
                                GROUP BY processname ORDER BY SUM(totaltime_secounds) DESC";

                var parameters = new DynamicParameters();
                parameters.Add("@datepara", datepara);
                List<ActiveTrackerAppModel> applicationSummary = dapper.GetAll<ActiveTrackerAppModel>(appSummarySql, parameters, CommandType.Text);

                // Update UI on the UI thread
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    ApplicationLogs.Clear();
                    if (applicationSummary != null && applicationSummary.Count > 0)
                    {
                        foreach (var item in applicationSummary)
                        {
                            ApplicationLogs.Add(item);
                        }
                        DisplayDaySummary(applicationSummary, datepara);
                    }
                    else
                    {
                        DisplayNoDataMessage(datepara);
                    }
                });

                string appDetailSql = @"SELECT processid, case WHEN appdescription <> '' THEN appdescription else processname end appdescription, CASE WHEN apptitle LIKE '%.exe%' THEN processname ELSE apptitle END processname, 
                               strftime('%H:%M:%S', totaltime_secounds, 'unixepoch') totaltimesecounds, 
                               strftime('%H:%M:%S', idletime_secounds, 'unixepoch') idletimesecounds, 
                               strftime('%H:%M:%S', (totaltime_secounds - idletime_secounds), 'unixepoch') actualsecounds,
                               strftime('%m/%d/%Y %H:%M:%S', start_time) starttime, strftime('%m/%d/%Y %H:%M:%S', end_time) endtime
                               FROM tbl_activeapplicactions WHERE totaltime_secounds > 0  AND date(start_time) = @datepara ORDER BY appdescription, start_time DESC";

                List<ActiveTrackerAppModel> activeAppsDetail = dapper.GetAll<ActiveTrackerAppModel>(appDetailSql, parameters, CommandType.Text);

                // Update UI on the UI thread
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    DetailedAppLogs.Clear();
                    if (activeAppsDetail != null && activeAppsDetail.Count > 0)
                    {
                        foreach (var item in activeAppsDetail)
                        {
                            DetailedAppLogs.Add(item);
                        }
                    }
                });
                // Refresh the CollectionView to update grouping
                CollectionViewSource.GetDefaultView(DetailedAppLogs).Refresh();
            }
            catch (Exception ex)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    System.Windows.MessageBox.Show($"Error loading application data: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        private void DisplayDaySummary(List<ActiveTrackerAppModel> applicationLogs, string date)
        {
            try
            {
                if (applicationLogs == null || applicationLogs.Count == 0)
                    return;

                string totalTimeSql = @"SELECT SUM(totaltime_secounds) as total_seconds, SUM(idletime_secounds) as idle_seconds,
                       SUM(totaltime_secounds - idletime_secounds) as active_seconds FROM tbl_activeapplicactions WHERE date(start_time) = @datepara";

                var parameters = new DynamicParameters();
                parameters.Add("@datepara", date);
                var dayInfo = dapper.Get<DaySummaryModel>(totalTimeSql, parameters, commandType: CommandType.Text);
                if (dayInfo != null)
                {
                    double totalSeconds = dayInfo.total_seconds;
                    double idleSeconds = dayInfo.idle_seconds;
                    double activeSeconds = dayInfo.active_seconds;
                    string totalTime = cf.SecondsToTime(totalSeconds);
                    string idleTime = cf.SecondsToTime(idleSeconds);
                    string activeTime = cf.SecondsToTime(activeSeconds);
                    DateTime selectedDate = DateTime.Parse(date);
                    string dateDisplay = selectedDate.Date == DateTime.Now.Date ? "Today" : selectedDate.ToString("MMM dd");

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        WindowTitle = $"Health Office Time Tracker - {dateDisplay} (Total: {totalTime}, Active: {activeTime})";
                    });
                }
            }
            catch (Exception ex)
            {
                // Log error but continue
            }
        }

        private void DisplayNoDataMessage(string date)
        {
            DateTime selectedDate = DateTime.Parse(date);

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (selectedDate.Date == DateTime.Now.Date)
                {
                    WindowTitle = "Health Office Time Tracker - Today (No activity recorded yet)";
                }
                else if (selectedDate.Date > DateTime.Now.Date)
                {
                    WindowTitle = $"Health Office Time Tracker - {selectedDate.ToString("MMM dd, yyyy")} (Future Date)";
                }
                else
                {
                    WindowTitle = $"Health Office Time Tracker - {selectedDate.ToString("MMM dd, yyyy")} (No activity recorded)";
                }
            });
        }

        public ExpandoObject GetProcessExtraInformation(int processId)
        {
            string query = "Select * From Win32_Process Where ProcessID = " + processId;
            ManagementObjectSearcher searcher = new ManagementObjectSearcher(query);
            ManagementObjectCollection processList = searcher.Get();

            dynamic response = new ExpandoObject();
            response.Description = "";
            response.Username = "Unknown";

            foreach (ManagementObject obj in processList)
            {
                string[] argList = new string[] { string.Empty, string.Empty };
                int returnVal = Convert.ToInt32(obj.InvokeMethod("GetOwner", argList));
                if (returnVal == 0)
                {
                    response.Username = argList[0];
                }
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

        public void TrackRunningAppTime()
        {
            try
            {
                IntPtr hwnd = APIFuncs.getforegroundWindow();
                Int32 pid = APIFuncs.GetWindowProcessID(hwnd);
                Process process = Process.GetProcessById(pid);
                appName = process.ProcessName;
                string appTitle = APIFuncs.ActiveApplTitle().Trim().Replace("\0", "");
                string appId = process.Id.ToString();

                if (appTitle == "System tray overflow window." || string.IsNullOrEmpty(appTitle))
                {
                    return;
                }
                dynamic extraProcessInfo = GetProcessExtraInformation(process.Id);
                string programPath = APIFuncs.GetMainModuleFileName(process);
                string url = cf.GetBrowserUrl(process);
                string currentAppKey = cf.CreateAppKey(appId, appName, appTitle);
                TimeSpan idleTime = APIFuncs.RetrieveIdleTime();
                bool isIdle = idleTime.TotalSeconds > IDLE_THRESHOLD_SECONDS;
                DateTime currentTime = DateTime.Now;
                double timeSinceLastCheck = (currentTime - lastIdleCheck).TotalSeconds;
                lastIdleCheck = currentTime;
                if (isIdle && !wasIdleLastTick)
                {
                    idleStartTime = currentTime;
                    wasIdleLastTick = true;
                }
                else if (!isIdle && wasIdleLastTick)
                {
                    TimeSpan idleDuration = currentTime - idleStartTime;

                    double idleSeconds = Math.Min(idleDuration.TotalSeconds, timeSinceLastCheck);

                    if (!string.IsNullOrEmpty(activeAppKey))
                    {
                        if (!accumulatedIdleTime.ContainsKey(activeAppKey))
                        {
                            accumulatedIdleTime[activeAppKey] = 0;
                        }
                        accumulatedIdleTime[activeAppKey] += idleSeconds;
                    }
                    wasIdleLastTick = false;
                }
                else if (isIdle && wasIdleLastTick)
                {
                    if (!string.IsNullOrEmpty(activeAppKey))
                    {
                        if (!accumulatedIdleTime.ContainsKey(activeAppKey))
                        {
                            accumulatedIdleTime[activeAppKey] = 0;
                        }

                        accumulatedIdleTime[activeAppKey] += timeSinceLastCheck;
                    }
                }
                if (!isIdle)
                {
                    lastActivityTime = currentTime;
                }
                TimeSpan elapsedTime = currentTime - lastTrackingTime;
                if (prevAppKey != currentAppKey)
                {
                    if (!string.IsNullOrEmpty(prevAppKey) && appTimeData.ContainsKey(prevAppKey))
                    {
                        UpdateAppTimeAndPersist(prevAppKey, elapsedTime, currentTime);
                    }
                    if (!appTimeData.ContainsKey(currentAppKey))
                    {
                        InitializeNewApp(currentAppKey, process, appTitle, programPath, url, extraProcessInfo, currentTime);

                        CreateInitialDatabaseRecord(currentAppKey, currentTime);
                    }
                    activeAppKey = currentAppKey;
                    prevAppKey = currentAppKey;
                }
                else
                {
                    UpdateAppTimeAndPersist(currentAppKey, elapsedTime, currentTime);
                }
                lastTrackingTime = currentTime;
            }
            catch (Exception ex) { }
        }

        private void UpdateAppTimeAndPersist(string appKey, TimeSpan elapsedTime, DateTime currentTime)
        {
            if (!appTimeData.ContainsKey(appKey))
                return;

            var appData = appTimeData[appKey];
            double idleTimeIncrement = 0;
            if (accumulatedIdleTime.ContainsKey(appKey) && accumulatedIdleTime[appKey] > 0)
            {
                idleTimeIncrement = accumulatedIdleTime[appKey];
                accumulatedIdleTime[appKey] = 0;
            }

            appData.TotalTimeSeconds += elapsedTime.TotalSeconds;
            appData.IdleTimeSeconds += idleTimeIncrement;
            appData.IdleTimeSeconds = Math.Min(appData.IdleTimeSeconds, appData.TotalTimeSeconds);
            appData.EndTime = currentTime;

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
                totaltime_secounds = appData.TotalTimeSeconds,
                idletime_secounds = appData.IdleTimeSeconds
            };
            LogActiveApps(activeAppParams);
        }

        private void LogActiveApps(ActiveAppProcessPara model)
        {
            if (string.IsNullOrEmpty(model.apptitle) || model.apptitle.ToLower() == "unknown-apptitle")
                return;

            bool isBrowser = cf.IsBrowserProcess(model.processname);
            string sql;
            var parameters = new DynamicParameters();

            if (isBrowser)
            {
                sql = @"SELECT id, processid, processname, processusername, appdescription, apptitle, appexepath, url, start_time, end_time, totaltime_secounds, idletime_secounds,             
            votrackerid, projectid, subprojectid, subprojectbranchid FROM tbl_activeapplicactions WHERE processid = @processid AND processname = @processname 
            AND apptitle = @apptitle AND date(start_time) = @dateParam ORDER BY start_time DESC LIMIT 1";
                parameters.Add("@processid", model.processid);
                parameters.Add("@processname", model.processname);
                parameters.Add("@apptitle", model.apptitle);
                parameters.Add("@dateParam", dateParam);
            }
            else
            {
                sql = @"SELECT id, processid, processname, processusername, appdescription, apptitle, appexepath, url, start_time, end_time, totaltime_secounds, idletime_secounds, 
            votrackerid, projectid, subprojectid, subprojectbranchid FROM tbl_activeapplicactions WHERE processid = @processid AND processname = @processname 
            AND date(start_time) = @dateParam ORDER BY start_time DESC LIMIT 1";
                parameters.Add("@processid", model.processid);
                parameters.Add("@processname", model.processname);
                parameters.Add("@dateParam", dateParam);
            }
            var data = dapper.Get<ActiveAppProcessModel>(sql, parameters, commandType: CommandType.Text);
            if (data != null)
            {
                DateTime lastEndTime = Convert.ToDateTime(data.end_time);
                DateTime currentTime = model.endtime;
                TimeSpan timeSinceLastUpdate = currentTime - lastEndTime;
                if (timeSinceLastUpdate.TotalMinutes > 2)
                {
                    InsertNewRecord(model);
                    return;
                }

                DateTime dbStartTime = Convert.ToDateTime(data.start_time);
                double totalTimeFromDb = (currentTime - dbStartTime).TotalSeconds;
                double idleTimeFromModel = model.idletime_secounds;
                double finalIdleTime = Math.Min(idleTimeFromModel, totalTimeFromDb);
                var dbparams = new DynamicParameters();
                int activeFlag = isPaused ? 1 : 0;
                dbparams.Add("end_time", currentTime, DbType.DateTime);
                dbparams.Add("totaltime_secounds", totalTimeFromDb, DbType.Double);
                dbparams.Add("idletime_secounds", finalIdleTime, DbType.Double);
                dbparams.Add("id", data.id, DbType.Int32);
                dbparams.Add("active", activeFlag, DbType.Int32);
                dbparams.Add("url", model.url, DbType.String);
                dbparams.Add("appexepath", model.appexepath, DbType.String);
                dbparams.Add("apptitle", model.apptitle, DbType.String);
                string updateSql = @"UPDATE tbl_activeapplicactions SET end_time = @end_time, totaltime_secounds = @totaltime_secounds, 
                idletime_secounds = @idletime_secounds, active = @active, url = @url, appexepath = @appexepath, apptitle = @apptitle WHERE id = @id";
                dapper.Insert<int>(updateSql, dbparams, CommandType.Text);
            }
            else
            {
                InsertNewRecord(model);
            }
        }

        private void InsertNewRecord(ActiveAppProcessPara model)
        {
            DateTime startTime = DateTime.Now;
            DateTime endTime = model.endtime;
            double totalTimeSeconds = (endTime - startTime).TotalSeconds;
            if (totalTimeSeconds <= 0)
            {
                totalTimeSeconds = 0;
            }
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
            dbparams.Add("totaltime_secounds", totalTimeSeconds, DbType.Double);
            dbparams.Add("idletime_secounds", idleTimeSeconds, DbType.Double);
            dbparams.Add("active", 0, DbType.Int32);

            string insertSql = @"INSERT INTO tbl_activeapplicactions(processid, processname, processusername, appdescription, apptitle, 
                        appexepath, url, start_time, end_time, totaltime_secounds, idletime_secounds, active) 
                        VALUES(@processid, @processname, @processusername, @appdescription, @apptitle, @appexepath, @url, @start_time, @end_time, @totaltime_secounds, 
                        @idletime_secounds, @active)";
            dapper.Insert<int>(insertSql, dbparams, CommandType.Text);
        }

        private void InitializeNewApp(string appKey, Process process, string appTitle, string programPath, string url, dynamic extraProcessInfo, DateTime currentTime)
        {
            appTimeData[appKey] = new AppTimeData
            {
                ProcessId = process.Id,
                ProcessName = process.ProcessName,
                ProcessUsername = extraProcessInfo.Username,
                AppDescription = extraProcessInfo.Description,
                AppTitle = appTitle,
                AppExePath = programPath,
                Url = url,
                StartTime = currentTime,
                EndTime = currentTime,
                TotalTimeSeconds = 0,
                IdleTimeSeconds = 0
            };
            accumulatedIdleTime[appKey] = 0;
        }

        private void CreateInitialDatabaseRecord(string appKey, DateTime currentTime)
        {
            if (!appTimeData.ContainsKey(appKey))
                return;

            var appData = appTimeData[appKey];
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
                totaltime_secounds = 0,
                idletime_secounds = 0
            };
            InsertNewRecord(activeAppParams);
        }
    }
}