using BusinessData.DataModel;
using BusinessService.Common;
using Dapper;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms; // For NotifyIcon which is still used from System.Windows.Forms
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Tracing;
using VOAPIService;

using VOAPIService.common;
using VOWatcherWFPApp.model;
using VOWatcherWFPApp.Services;
using VOWatcherWPFApp;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TreeView;


namespace VOWatcherWFPApp

{
    /// <summary>
    /// Interaction logic for Tracker.xaml
    /// </summary>
    public partial class Tracker : Window, INotifyPropertyChanged
    {
        public static bool isLogin = false;
        public static User userDetail = null;

        // 🔔 Global logout event for all other windows
        public static event Action OnLogout;

        public static T FindChild<T>(DependencyObject parent, string childName) where T : DependencyObject
        {
            if (parent == null) return null;

            T foundChild = null;
            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                T childType = child as T;

                if (childType == null)
                {
                    foundChild = FindChild<T>(child, childName);
                    if (foundChild != null) break;
                }
                else if (!string.IsNullOrEmpty(childName))
                {
                    var frameworkElement = child as FrameworkElement;
                    if (frameworkElement != null && frameworkElement.Name == childName)
                    {
                        foundChild = (T)child;
                        break;
                    }
                }
                else
                {
                    foundChild = (T)child;
                    break;
                }
            }
            return foundChild;
        }
        private DateTime? _currentStartTime;
        private DispatcherTimer _elapsedTimer;
        private DispatcherTimer _pollingTimer;
        private HttpClient _httpClient;
        private GetTrackerTask _currentTrackerTask;
        #region Variables Declaration
        private static string appName;
        private static string prevAppKey;
        private static Dictionary<string, AppTimeData> appTimeData = new Dictionary<string, AppTimeData>();
        private DateTime lastActivityTime;
        private DateTime lastTrackingTime;
        private const int IDLE_THRESHOLD_SECONDS = 60;
        private IDapper dapper;
        private bool allowShowDisplay = false;
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
        private bool isCheckedIn = false;
        private int h = 0, m = 0, s = 0;
        private DispatcherTimer stopwatchTimer;
        private DispatcherTimer currentTimeTimer;
        private ClockInStatusResult currentStatus;  // stores check-in status (incl. from GoVirtual)
        private int totalSeconds = 0;
        private DispatcherTimer _timer;
        private DateTime? _startDate;
        private DispatcherTimer _timers;
        private DateTime _startTime;
        private bool _isProcessingClick = false;




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
        private bool isCheckedin;


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
        public Tracker()
        {
            InitializeComponent();
            LoginWindow.OnLogout += HandleLogout;


            this.Loaded += Tracker_Loaded;
            this.Activated += Tracker_Activated;
            this.StateChanged += Tracker_StateChanged;
            btnLogin.Content = LoginWindow.isLogin ? "Logout" : "Login";

            DataContext = this;
            // Set initial window title
            WindowTitle = "Health Office Time Tracker";

            dapper = new Dapperr();
            dateParam = DateTime.Now.ToString("yyyy-MM-dd");

            lastActivityTime = DateTime.Now;
            lastTrackingTime = DateTime.Now;
            idleStartTime = DateTime.Now;
            lastIdleCheck = DateTime.Now;

            //// Set initial calendar selection to today
            monthCalendar1.DisplayDate = DateTime.Now;
            monthCalendar1.SelectedDate = DateTime.Now;

            //// Register the date selection event
            monthCalendar1.SelectedDatesChanged += monthCalendar1_SelectedDatesChanged;

            // Handle window closed event
            //Closing += Tracker_Deactivated;
            SourceInitialized += Tracker_SourceInitialized;
            // remove window close button
            Loaded += Tracker_Loaded;
            // Activate/Deactivate events
            Activated += Tracker_Activated;
            Deactivated += Tracker_Deactivated;
            StateChanged += Tracker_StateChanged;
            //this.Hide();

            SetupSystemTrayIcon();
            SetupTimer();
            // Initial data load
            LoadActiveApps();
            // Configure the DataGrid grouping
            ConfigureDataGridGrouping();
            _httpClient = new HttpClient();
            // Don't set authorization header here - it will be set after login





            dataGridView1.ItemsSource = ApplicationLogs;
            dataGridView2.ItemsSource = DetailedAppLogs;

        }


        //private async void Window_Loaded(object sender, RoutedEventArgs e)
        //{
        //    int employeeid = LoginWindow.userDetail.employeeid;
        //    await LoadTrackerTimerAsync(employeeid);
        //}
        private void HandleLogout()
        {
            Dispatcher.Invoke(() =>
            {
                // Stop polling timer
                if (_pollingTimer != null)
                {
                    _pollingTimer.Stop();
                    _pollingTimer = null;
                }
                
                // Dispose HttpClient
                _httpClient?.Dispose();
                _httpClient = null;
                
                // Reset UI elements
                btnLogin.Content = "Login";
                txtStartTime.Text = "--:--:--";
                txtTaskName.Text = null;
                txtHours.Text = txtMinutes.Text = txtSeconds.Text = "00";
                imgStartStop.Source = new BitmapImage(new Uri("start.png", UriKind.Relative));
                StopElapsedTimer();
                
                System.Windows.MessageBox.Show("You have been logged out. This window will now close.", "Session Expired",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                this.Close(); // or DisableUI() if you want to keep it open but locked
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // ✅ Unsubscribe to avoid memory leaks
            LoginWindow.OnLogout -= HandleLogout;
        }


        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {

            // ✅ 1. Start live current time
            currentTimeTimer = new DispatcherTimer();
            currentTimeTimer.Interval = TimeSpan.FromSeconds(1);
            currentTimeTimer.Tick += CurrentTimeTimer_Tick;
            currentTimeTimer.Start();

            // ✅ 2. Only load tracker data if user is logged in
            if (LoginWindow.isLogin && LoginWindow.userDetail != null)
            {
                InitializeHttpClient();
                // ✅ 3. Check GoVirtual check-in status on load
                CheckCurrentClockInStatus();
                // ✅ 4. Load tracker timer from API using employee ID
                LoadTrackerData();
            }

        }



        private void Tracker_Loaded(object sender, RoutedEventArgs e)
        {

            Login();
        }



        private void Login()
        {
            if (LoginWindow.isLogin)
            {
                btnLogin.Content = "Logout";
                InitializeHttpClient();
                return;
            }

            LoginWindow loginWindow = new LoginWindow();
            loginWindow.Owner = this;
            bool? result = loginWindow.ShowDialog();

            if (result == true)
            {
                btnLogin.Content = "Logout";
                InitializeHttpClient();
                // Load tracker data after successful login
                LoadTrackerData();
            }
            else
            {
                System.Windows.MessageBox.Show("You must be logged in to continue.", "Login Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                this.Close();
            }
        }

        public void InitializeHttpClient()
        {
            if (LoginWindow.isLogin && LoginWindow.userDetail != null && !string.IsNullOrEmpty(LoginWindow.userDetail.access_token))
            {
                _httpClient?.Dispose();
                _httpClient = new HttpClient();
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", LoginWindow.userDetail.access_token);
            }
        }



        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            if (LoginWindow.isLogin)
            {
                // Already logged in, so perform logout
                var result = System.Windows.MessageBox.Show("Do you want to logout?", "Confirm Logout",
                                             MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    LoginWindow.isLogin = false;

                    // keep schema file intact
                    btnLogin.Content = "Login";



                    System.Windows.MessageBox.Show("Logged out successfully.", "Logout", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                // Not logged in, open login window
                LoginWindow loginWindow = new LoginWindow();
                loginWindow.Owner = this;
                bool? result = loginWindow.ShowDialog();

                if (result == true)
                {
                    // Login successful
                    btnLogin.Content = "Logout";
                    InitializeHttpClient();
                    // Load tracker data after successful login
                    LoadTrackerData();
                }
                else
                {
                    System.Windows.MessageBox.Show("You must be logged in to continue.", "Login Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    // you can decide to Close() the app if critical
                }
            }
        }




        public async Task LoadTrackerData()
        {
            try
            {
                Debug.WriteLine("Loading tracker data...");
                
                // Check if user is logged in
                if (!LoginWindow.isLogin || LoginWindow.userDetail == null)
                {
                    Debug.WriteLine("Cannot load tracker data - user not logged in");
                    return;
                }

                await FetchAndDisplayTrackerTask();
                await FetchTrackerTask(); // optional, depending on your use case
                StartPollingForUpdates();
                
                Debug.WriteLine("Tracker data loaded successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading tracker data: {ex.Message}");
                System.Windows.MessageBox.Show($"Error loading tracker data: {ex.Message}", "Exception", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private async Task FetchAndDisplayTrackerTask()
        {
            try
            {
                // Check if user is logged in and has valid credentials
                if (!LoginWindow.isLogin || LoginWindow.userDetail == null || string.IsNullOrEmpty(LoginWindow.userDetail.access_token))
                {
                    Debug.WriteLine("User not logged in or missing access token");
                    return;
                }

                var oAuthParams = new VOAPIOAuthParams
                {
                    BaseUrl = ConfigurationManager.AppSettings["baseurl"].ToString(), // <-- using your existing variable
                    Module = $"/UtilizationTracker/GetTrackerTask/{LoginWindow.userDetail.employeeid}"
                };

                var oClient = VOApiRestSharp.GetInstance(oAuthParams);
                string result = oClient.GetModuleResult(LoginWindow.userDetail.access_token);

                if (!string.IsNullOrWhiteSpace(result))
                {
                    Debug.WriteLine($"API response: {result}");

                    var tracker = JsonConvert.DeserializeObject<GetTrackerTask>(result);

                    if (tracker != null && tracker.startTime.HasValue)
                    {
                        _currentStartTime = tracker.startTime;
                        _currentTrackerTask = tracker;

                        txtStartTime.Text = tracker.startTime.Value.ToString("HH:mm:ss");
                        txtTaskName.Text = tracker.activity ?? "--";
                        imgStartStop.Source = new BitmapImage(new Uri("stop.png", UriKind.Relative));
                        StartElapsedTimer(tracker.startTime.Value);
                    }
                    else
                    {
                        _currentStartTime = null;
                        _currentTrackerTask = null;

                        txtStartTime.Text = "--:--:--";
                        txtTaskName.Text = null;
                        txtHours.Text = txtMinutes.Text = txtSeconds.Text = "00";
                        imgStartStop.Source = new BitmapImage(new Uri("start.png", UriKind.Relative));
                        StopElapsedTimer();
                    }
                }
                else
                {
                    Debug.WriteLine("Empty response from API");
                    // Don't show error message for empty response as it might be normal
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in FetchAndDisplayTrackerTask: {ex.Message}");
                // Only show error message for critical errors, not for normal API failures
                if (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
                {
                    System.Windows.MessageBox.Show("Session expired. Please login again.", "Authentication Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    LoginWindow.PerformLogout();
                }
            }
        }




        private void StartElapsedTimer(DateTime startTime)
        {
            StopElapsedTimer(); // always stop old one

            _elapsedTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };

            _elapsedTimer.Tick += (s, e) =>
            {
                var elapsed = DateTime.Now - startTime;

                txtHours.Text = elapsed.Hours.ToString("D2");
                txtMinutes.Text = elapsed.Minutes.ToString("D2");
                txtSeconds.Text = elapsed.Seconds.ToString("D2");
            };

            _elapsedTimer.Start();
        }

        private void StopElapsedTimer()
        {
            if (_elapsedTimer != null)
            {
                _elapsedTimer.Stop();
                _elapsedTimer = null;
            }
        }

        private void StartPollingForUpdates()
        {
            if (_pollingTimer != null)
                return; // Already running

            // Only start polling if user is logged in
            if (!LoginWindow.isLogin || LoginWindow.userDetail == null)
            {
                Debug.WriteLine("Cannot start polling - user not logged in");
                return;
            }

            _pollingTimer = new DispatcherTimer();
            _pollingTimer.Interval = TimeSpan.FromSeconds(3); // You can reduce to 3 if needed
            _pollingTimer.Tick += async (s, e) =>
            {
                // Check if still logged in before making API call
                if (LoginWindow.isLogin && LoginWindow.userDetail != null)
                {
                    await FetchAndDisplayTrackerTask();
                }
                else
                {
                    // Stop polling if user is no longer logged in
                    _pollingTimer?.Stop();
                    _pollingTimer = null;
                }
            };
            _pollingTimer.Start();
            Debug.WriteLine("Started polling for tracker updates");
        }


        private async void btnStartStop_Click(object sender, RoutedEventArgs e)
        {
            string token = LoginWindow.userDetail.access_token;

            if (_currentStartTime != null && _currentTrackerTask != null)
            {
                // Create STOP payload
                var data = new AddEditTrackerTask
                {
                    id = _currentTrackerTask.id,
                    employeeid = _currentTrackerTask.employeeid,
                    branchid = _currentTrackerTask.branchid,
                    companyid = _currentTrackerTask.companyid,
                    taskListId = _currentTrackerTask.taskListid ?? 0,
                    projectId = _currentTrackerTask.projectId,
                    subProjectId = _currentTrackerTask.subProjectId,
                    subProjectCategoryId = _currentTrackerTask.subProjectCategoryId,
                    activity = _currentTrackerTask.activity,
                    button = "stop",
                    cloneID = 0,
                    isAdmin = 1
                };

                try
                {
                    string baseUrl = ConfigurationManager.AppSettings["baseurl"]?.ToString()?.TrimEnd('/');
                    if (string.IsNullOrWhiteSpace(baseUrl))
                    {
                        System.Windows.MessageBox.Show("Base URL missing in configuration.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    using (var http = new HttpClient())
                    {
                        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                        var json = JsonConvert.SerializeObject(data);
                        var content = new StringContent(json, Encoding.UTF8, "application/json");
                        var resp = await http.PostAsync($"{baseUrl}/UtilizationTracker/AddEditTrackerTask", content);
                        if (!resp.IsSuccessStatusCode)
                        {
                            var body = await resp.Content.ReadAsStringAsync();
                            System.Windows.MessageBox.Show($"Failed to stop task. Status: {resp.StatusCode}\n{body}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }

                    Debug.WriteLine("Stop task payload: " + JsonConvert.SerializeObject(data, Formatting.Indented));

                    // Reset UI
                    _currentStartTime = null;
                    _currentTrackerTask = null;
                    txtStartTime.Text = "--:--:--";
                    txtTaskName.Text = null;
                    txtHours.Text = txtMinutes.Text = txtSeconds.Text = "00";
                    imgStartStop.Source = new BitmapImage(new Uri("start.png", UriKind.Relative));
                    StopElapsedTimer();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Error stopping task:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }



            // Removed redirect to portal
        }







        private void StartTimer()
        {
            StopTimer(); // clear any previous timer

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += (s, e) =>
            {
                var elapsed = DateTime.Now - _startTime;

                txtHours.Text = elapsed.Hours.ToString("D2");
                txtMinutes.Text = elapsed.Minutes.ToString("D2");
                txtSeconds.Text = elapsed.Seconds.ToString("D2");
            };
            _timer.Start();
        }

        private void StopTimer()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer = null;

                txtHours.Text = "00";
                txtMinutes.Text = "00";
                txtSeconds.Text = "00";
            }
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
                System.Windows.MessageBox.Show($"Error loading data for selected date: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
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
                    notifyIcon.BalloonTipTitle = "Tracker";
                    notifyIcon.BalloonTipText = "Application minimized to system tray. Double-click to restore.";
                    notifyIcon.ShowBalloonTip(3000);
                }
            }

            base.OnStateChanged(e);
        }
        private void Tracker_Deactivated(object sender, EventArgs e)
        {
            isPaused = false;
            timer.Start();
        }

        private void Tracker_Activated(object sender, EventArgs e)
        {
            isPaused = true;
            timer.Stop();
            if (ApplicationLogs.Count == 0)
            {
                LoadActiveApps();
            }
            CheckCurrentClockInStatus();
        }
        private void Tracker_StateChanged(object sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Normal || this.WindowState == WindowState.Maximized)
            {
                CheckCurrentClockInStatus();
            }
        }

        private void Tracker_SourceInitialized(object sender, EventArgs e)
        {
            // Get the window handle
            IntPtr hwnd = new WindowInteropHelper(this).Handle;

            // Change the window style
            int currentStyle = GetWindowLong(hwnd, GWL_STYLE);

            // To disable the maximize button:
            SetWindowLong(hwnd, GWL_STYLE, currentStyle & ~WS_MAXIMIZEBOX);

            // To disable both maximize and close buttons:
            // SetWindowLong(hwnd, GWL_STYLE, currentStyle & ~(WS_MAXIMIZEBOX | WS_SYSMENU));
            // Ensure window doesn't overlap taskbar
            this.MaxHeight = SystemParameters.MaximizedPrimaryScreenHeight;
            this.MaxWidth = SystemParameters.MaximizedPrimaryScreenWidth;
        }


        private void Timer_Tick(object sender, EventArgs e)
        {
            TrackRunningAppTime();
            LoadActiveApps();
        }
        private void Timer_Tick1(object sender, EventArgs e)
        {
            // Update UI on each tick
            label3.Content = "Current Time: " + DateTime.Now.ToString("HH:mm:ss");
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
                    WindowState = WindowState.Maximized;
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
            exitItem.Click += new EventHandler(Tracker_Closing);
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
                        WindowState = WindowState.Maximized;
                        Activate();
                        LoadActiveApps();
                    }
                    else
                    {
                        if (WindowState == WindowState.Minimized)
                        {
                            WindowState = WindowState.Maximized;
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
                    WindowState = WindowState.Maximized;
                    Activate();
                    LoadActiveApps();
                }
            };
        }
        private void Tracker_Closing(object sender, EventArgs e)
        {
            // Ask user for confirmation
            System.Windows.MessageBoxResult result = System.Windows.MessageBox.Show(
                 "Are you sure you want to close the application?\n\nThis will stop time tracking.",
                 "Confirm Close",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
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

                string appDetailSql = @"SELECT processid, 
                               CASE WHEN appdescription <> '' THEN appdescription ELSE processname END appdescription, 
                               CASE WHEN apptitle LIKE '%.exe%' THEN processname ELSE apptitle END processname, 
                               strftime('%H:%M:%S', totaltime_secounds, 'unixepoch') totaltimesecounds, 
                               strftime('%H:%M:%S', idletime_secounds, 'unixepoch') idletimesecounds, 
                               strftime('%H:%M:%S', (totaltime_secounds - idletime_secounds), 'unixepoch') actualsecounds,
                               strftime('%m/%d/%Y %H:%M:%S', start_time) starttime, 
                               strftime('%m/%d/%Y %H:%M:%S', end_time) endtime
                               FROM tbl_activeapplicactions 
                               WHERE totaltime_secounds > 0 AND date(start_time) = @datepara 
                               ORDER BY apptitle, start_time DESC";

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
                    System.Windows.MessageBox.Show($"Error loading application data: {ex.Message}", "Database Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
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
            catch (Exception)
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

        private static string RemoveSuffixIgnoreCase(string input, string suffix)
        {
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(suffix)) return input;
            if (input.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return input.Substring(0, input.Length - suffix.Length);
            }
            return input;
        }

        private static string GetRegistrableDomain(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) return null;
            host = host.Trim().ToLowerInvariant();
            if (host.StartsWith("www.")) host = host.Substring(4);

            var parts = host.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return host;

            string last = parts[parts.Length - 1];
            string second = parts[parts.Length - 2];

            // Simple heuristic to handle multi-part TLDs like co.uk, com.au, co.in
            string[] secondLevelTlds = { "co", "com", "net", "org", "gov", "edu" };
            if (last.Length == 2 && secondLevelTlds.Contains(second, StringComparer.OrdinalIgnoreCase) && parts.Length >= 3)
            {
                return parts[parts.Length - 3] + "." + second + "." + last;
            }

            return second + "." + last;
        }

        private string NormalizeAppTitle(string processName, string originalTitle, string url)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(originalTitle)) return originalTitle;

                string lowerProcess = (processName ?? string.Empty).ToLowerInvariant();
                bool isEditor = lowerProcess.Contains("cursor") || lowerProcess.Contains("code") || lowerProcess.Contains("devenv")
                                || originalTitle.IndexOf("Cursor", StringComparison.OrdinalIgnoreCase) >= 0
                                || originalTitle.IndexOf("Visual Studio", StringComparison.OrdinalIgnoreCase) >= 0
                                || originalTitle.IndexOf("Visual Studio Code", StringComparison.OrdinalIgnoreCase) >= 0;

                if (isEditor)
                {
                    // Typical pattern: "FileName.ext - WorkspaceName - Cursor"
                    var parts = originalTitle.Split(new[] { " - " }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        // Take the second last part as the workspace/project name
                        return parts[parts.Length - 2].Trim();
                    }
                }

                // Browser normalization - Keep individual tab titles for separate tracking
                bool isBrowser = cf.IsBrowserProcess(processName);
                if (isBrowser)
                {
                    // Strip trailing browser name suffix from window title but keep the page title
                    string stripped = originalTitle;
                    stripped = RemoveSuffixIgnoreCase(stripped, " - Google Chrome");
                    stripped = RemoveSuffixIgnoreCase(stripped, " - Microsoft Edge");
                    stripped = RemoveSuffixIgnoreCase(stripped, " - Mozilla Firefox");
                    stripped = stripped.Trim();

                    // For individual tab tracking, use the full page title instead of just domain
                    if (!string.IsNullOrWhiteSpace(stripped))
                    {
                        // If we have a URL, append it to the title for better identification
                        if (!string.IsNullOrWhiteSpace(url) && Uri.TryCreate(url, UriKind.Absolute, out var uri))
                        {
                            System.Diagnostics.Debug.WriteLine($"URL captured: {url}, Title: {stripped}");
                            // Return title with URL for unique identification
                            return $"{stripped} ({uri.Host})";
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"No URL captured for {processName}, originalTitle: {originalTitle}");
                            return stripped;
                        }
                    }

                    // Fallbacks when no title available
                    if (stripped.Equals("New tab", StringComparison.OrdinalIgnoreCase) || stripped.Equals("New Tab", StringComparison.OrdinalIgnoreCase))
                    {
                        return "New Tab";
                    }

                    // Map process to friendly browser label as last resort
                    if (lowerProcess.Contains("chrome")) return "Google Chrome";
                    if (lowerProcess.Contains("msedge")) return "Microsoft Edge";
                    if (lowerProcess.Contains("firefox")) return "Mozilla Firefox";
                    if (lowerProcess.Contains("iexplore")) return "Internet Explorer";

                    return processName;
                }

                // Non-browser: collapse to product family when typical suffix/prefix exists
                string t = originalTitle.Trim();
                // Visual Studio solution titles often like: "SolutionName - Microsoft Visual Studio"
                if (t.IndexOf("Microsoft Visual Studio", StringComparison.OrdinalIgnoreCase) > 0)
                {
                    var parts = t.Split(new[] { " - " }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        return parts[0].Trim() + " - Visual Studio";
                    }
                    return "Visual Studio";
                }
                // Office apps: normalize
                if (t.EndsWith(" - Word", StringComparison.OrdinalIgnoreCase)) return "Microsoft Word";
                if (t.EndsWith(" - Excel", StringComparison.OrdinalIgnoreCase)) return "Microsoft Excel";
                if (t.EndsWith(" - PowerPoint", StringComparison.OrdinalIgnoreCase)) return "Microsoft PowerPoint";
                if (t.EndsWith(" - OneNote", StringComparison.OrdinalIgnoreCase)) return "Microsoft OneNote";

                return t;
            }
            catch
            {
                return originalTitle;
            }
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
                appTitle = NormalizeAppTitle(appName, appTitle, url);
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
            catch (Exception) { }
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
            // Persist only the increment for this tick so DB totals always represent the sum of active sessions during the day
            LogActiveApps(activeAppParams, elapsedTime.TotalSeconds, idleTimeIncrement);
        }

        private void LogActiveApps(ActiveAppProcessPara model, double activeIncrementSeconds = 0, double idleIncrementSeconds = 0)
        {
            if (string.IsNullOrEmpty(model.apptitle) || model.apptitle.ToLower() == "unknown-apptitle")
                return;

            // For browsers, don't consolidate - keep individual tabs separate
            // For other apps, consolidate by apptitle per day
            string sql;
            if (cf.IsBrowserProcess(model.processname))
            {
                // For browsers, check by apptitle AND processid to keep tabs separate
                sql = @"SELECT id, start_time, end_time, totaltime_secounds, idletime_secounds 
                        FROM tbl_activeapplicactions 
                        WHERE apptitle = @apptitle AND processid = @processid AND date(start_time) = @dateParam 
                        ORDER BY start_time ASC LIMIT 1";
            }
            else
            {
                // For non-browsers, consolidate by apptitle per day
                sql = @"SELECT id, start_time, end_time, totaltime_secounds, idletime_secounds 
                        FROM tbl_activeapplicactions 
                        WHERE apptitle = @apptitle AND date(start_time) = @dateParam 
                        ORDER BY start_time ASC LIMIT 1";
            }

            var parameters = new DynamicParameters();
            parameters.Add("@apptitle", model.apptitle);
            parameters.Add("@dateParam", dateParam);
            if (cf.IsBrowserProcess(model.processname))
            {
                parameters.Add("@processid", model.processid);
            }

            var data = dapper.Get<ActiveAppProcessModel>(sql, parameters, commandType: CommandType.Text);
            if (data != null)
            {
                // Accumulate active and idle seconds
                double existingTotal = 0;
                double existingIdle = 0;
                try { existingTotal = Convert.ToDouble(data.totaltime_secounds); } catch { existingTotal = 0; }
                try { existingIdle = Convert.ToDouble(data.idletime_secounds); } catch { existingIdle = 0; }

                double newTotal = existingTotal + Math.Max(0, activeIncrementSeconds);
                double newIdle = existingIdle + Math.Max(0, idleIncrementSeconds);
                newIdle = Math.Min(newIdle, newTotal);

                var dbparams = new DynamicParameters();
                int activeFlag = isPaused ? 1 : 0;
                dbparams.Add("end_time", model.endtime, DbType.DateTime);
                dbparams.Add("totaltime_secounds", newTotal, DbType.Double);
                dbparams.Add("idletime_secounds", newIdle, DbType.Double);
                dbparams.Add("id", data.id, DbType.Int32);
                dbparams.Add("active", activeFlag, DbType.Int32);
                dbparams.Add("url", model.url, DbType.String);
                dbparams.Add("appexepath", model.appexepath, DbType.String);
                dbparams.Add("apptitle", model.apptitle, DbType.String);
                dbparams.Add("processname", model.processname, DbType.String);
                dbparams.Add("processusername", model.processusername, DbType.String);
                dbparams.Add("appdescription", model.appdescription, DbType.String);

                string updateSql = @"UPDATE tbl_activeapplicactions 
                                     SET end_time = @end_time, 
                                         totaltime_secounds = @totaltime_secounds, 
                                         idletime_secounds = @idletime_secounds, 
                                         active = @active, 
                                         url = @url, 
                                         appexepath = @appexepath, 
                                         apptitle = @apptitle, 
                                         processname = @processname, 
                                         processusername = @processusername, 
                                         appdescription = @appdescription 
                                     WHERE id = @id";
                dapper.Insert<int>(updateSql, dbparams, CommandType.Text);
            }
            else
            {
                // First time this apptitle is seen today – create a single consolidated row
                InsertNewRecord(model, activeIncrementSeconds, Math.Max(0, idleIncrementSeconds));
            }
        }


        private static string GetAppToken()
        {
            try
            {
                var oAuthParams = new VOAPIOAuthParams
                {
                    ApiKey = ConfigurationManager.AppSettings["apikey"],
                    AuthUrl = ConfigurationManager.AppSettings["authurl"]
                };

                VOApiRestSharp oClient = VOApiRestSharp.GetInstance(oAuthParams);
                VOAPIAuthtoken tokens = oClient.GenerateAccessToken();

                return tokens?.AccessToken ?? string.Empty;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Failed to generate app token:\n" + ex.Message);
                return string.Empty;
            }
        }

        private void InsertNewRecord(ActiveAppProcessPara model, double initialActiveSeconds = 0, double initialIdleSeconds = 0)
        {
            // Initialize a new record for this apptitle/day (or apptitle/processid for browsers)
            DateTime startTime = model.endtime; // first seen time for today
            DateTime endTime = model.endtime;
            double totalTimeSeconds = Math.Max(0, initialActiveSeconds);
            double idleTimeSeconds = Math.Min(Math.Max(0, initialIdleSeconds), totalTimeSeconds);
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
                IdleTimeSeconds = 0,
                logdate = currentTime
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
                idletime_secounds = 0,
                logdate = appData.logdate
            };
            InsertNewRecord(activeAppParams, 0, 0);
        }


        private void StartTimers()
        {
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += (sender, e) =>
            {
                s++;
                if (s == 60) { s = 0; m++; }
                if (m == 60) { m = 0; h++; }

                lblcheckin.Text = $"{h:D2}:{m:D2}:{s:D2}";
            };
            timer.Start();
        }

        //checkinout buttton
        private void CheckInOutButton_Click(object sender, RoutedEventArgs e)
        {
            if (!LoginWindow.isLogin)
            {
                System.Windows.MessageBox.Show("Please login first.");

                return;
            }

            // Check if already clocked in (from GoVirtual or this app)
            bool alreadyCheckedIn = currentStatus?.Status?.ToLower() == "in";

            UserClockInModel userClockIn = new UserClockInModel
            {
                DeviceName = GetSystemName(),
                EmployeeId = LoginWindow.userDetail.employeeid,
                UserId = LoginWindow.userDetail.userid,
                IpAddress = GetIPAddress(),
                UserName = LoginWindow.userDetail.username,
                Pin = "0"
            };

            VOAPIOAuthParams oAuthParams = new VOAPIOAuthParams
            {
                BaseUrl = ConfigurationManager.AppSettings["baseurl"].ToString(),
                Module = "/punchdetail/clockinout"
            };

            VOApiRestSharp oClient = VOApiRestSharp.GetInstance(oAuthParams);
            string result = oClient.PostModuleResult(LoginWindow.userDetail.access_token, userClockIn);

            if (string.IsNullOrWhiteSpace(result))
            {
                System.Windows.MessageBox.Show("No response from server. Please try again.");
                return;
            }

            APIResult jsonresult;

            try
            {
                jsonresult = JsonConvert.DeserializeObject<APIResult>(result);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Failed to parse server response: " + ex.Message);
                return;
            }

            switch (jsonresult?.Message)
            {
                case "Clocked-In successfully.":
                    h = m = s = 0;
                    isCheckedin = true;
                    StartStopwatch();
                    CheckInOutButton.Content = "Check-out";
                    lblcheckinT.Text = "You checked in at " + DateTime.Now.ToString("HH:mm:ss tt");
                    lblcheckinT.Foreground = Brushes.Green;

                    currentStatus = new ClockInStatusResult();
                    currentStatus.Status = "in";
                    currentStatus.CheckInTime = DateTime.Now.ToString("HH:mm:ss");

                    break;

                case "Clocked-Out successfully.":
                    isCheckedin = false;
                    StopStopwatch();
                    CheckInOutButton.Content = "Check-in";
                    lblcheckin.Text = "00:00:00";
                    lblcheckinT.Text = "You checked out at " + DateTime.Now.ToString("hh:mm:ss tt");
                    lblcheckinT.Foreground = Brushes.Red;

                    currentStatus = new ClockInStatusResult
                    {
                        Status = "out",
                        CheckInTime = null
                    };
                    break;

                default:
                    isCheckedin = false;
                    StopStopwatch();
                    System.Windows.MessageBox.Show("Unexpected response: " + jsonresult?.Message);
                    lblcheckin.Text = "00:00:00";
                    txtCheckinTime.Text = DateTime.Now.ToString("hh:mm:ss tt");
                    CheckInOutButton.Content = "Check-in";
                    break;
            }
        }

        private void CheckCurrentClockInStatus()
        {
            if (!LoginWindow.isLogin) return;

            var statusParams = new VOAPIOAuthParams
            {
                BaseUrl = ConfigurationManager.AppSettings["baseurl"].ToString(),
                Module = "/PunchDetail/GetUserInOutvalue/" + LoginWindow.userDetail.userid
            };

            VOApiRestSharp client = VOApiRestSharp.GetInstance(statusParams);
            var result = client.GetModuleResult(LoginWindow.userDetail.access_token);

            if (result == null)
                return;

            try
            {
                // Parse directly into ClockInStatusResult if API returns raw status
                currentStatus = JsonConvert.DeserializeObject<ClockInStatusResult>(result);

                // ✅ Debug output
                System.Diagnostics.Debug.WriteLine($"Status from API: {currentStatus?.Status}");

                if (currentStatus?.CheckInTime != null && currentStatus?.CheckOutTime == null)
                {
                    // ✅ User already checked in — show "Check-out"
                    isCheckedin = true;
                    CheckInOutButton.Content = "Check-out";
                    lblcheckinT.Text = "You checked in at " + Convert.ToDateTime(currentStatus.CheckInTime).ToString("HH:mm:ss");
                    lblcheckinT.Foreground = Brushes.Green;
                }
                else
                {
                    // ❌ User not checked in — show "Check-in"
                    isCheckedin = false;
                    CheckInOutButton.Content = "Check-in";
                    lblcheckinT.Text = "You are not checked in.";
                    lblcheckinT.Foreground = Brushes.Red;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Failed to check check-in status: " + ex.Message);
            }
        }

        private void StartStopwatch()
        {
            if (stopwatchTimer == null)
            {
                stopwatchTimer = new DispatcherTimer();
                stopwatchTimer.Interval = TimeSpan.FromSeconds(1);
                stopwatchTimer.Tick += (s, e) =>
                {
                    this.s++;
                    if (this.s == 60) { this.s = 0; m++; }
                    if (m == 60) { m = 0; h++; }
                    lblcheckin.Text = $"{h:D2}:{m:D2}:{this.s:D2}";
                };
            }
            stopwatchTimer.Start();
        }

        private void StopStopwatch()
        {
            stopwatchTimer?.Stop();
            h = m = s = 0;
            lblcheckin.Text = "00:00:00";
        }





        private void CurrentTimeTimer_Tick(object sender, EventArgs e)
        {
            if (txtCheckinTime != null)
            {
                txtCheckinTime.Text = DateTime.Now.ToString("hh:mm:ss tt");
            }
        }
        private void StartCurrentTimeTimer()
        {
            currentTimeTimer = new DispatcherTimer();
            currentTimeTimer.Interval = TimeSpan.FromSeconds(1);
            currentTimeTimer.Tick += (s, ev) =>
            {
                txtCheckinTime.Text = DateTime.Now.ToString("hh:mm:ss tt");
            };
            currentTimeTimer.Start();
        }




        private static string GetSystemName()
        {
            string pcname = Environment.MachineName;
            return pcname;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (LoginWindow.userDetail == null || LoginWindow.userDetail.userid <= 0)
            {
                System.Windows.MessageBox.Show("Please login first.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);

                return;
            }

            var addTaskWindow = new AddTaskFrom(); // class name of your XAML window
            addTaskWindow.Show(); // opens as normal window (non-modal)
        }


        private static string GetIPAddress()
        {
            string address = "";
            WebRequest request = WebRequest.Create("http://checkip.dyndns.org/");
            using (WebResponse response = request.GetResponse())
            using (StreamReader stream = new StreamReader(response.GetResponseStream()))
            {
                address = stream.ReadToEnd();
            }

            int first = address.IndexOf("Address: ") + 9;
            int last = address.LastIndexOf("</body>");
            address = address.Substring(first, last - first);

            return address;
        }
        private void dataGridView2_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (dataGridView2.SelectedItem is ActiveTrackerAppModel selectedApp)
            {
                var contextMenu = new System.Windows.Controls.ContextMenu();

                // Add Task menu item
                var addTaskItem = new System.Windows.Controls.MenuItem { Header = "Add Task" };
                addTaskItem.Click += (s, args) =>
                {
                    var model = new TaskFormModel
                    {
                        TaskDescription = $"Task for {selectedApp.processname}",

                    };

                    if (DateTime.TryParse(selectedApp.starttime, out DateTime fromTime))
                        model.AssignDate = fromTime;

                    if (DateTime.TryParse(selectedApp.endtime, out DateTime toTime))
                        model.DueDate = toTime;


                    var TaskWindow = new TaskWindow();
                    TaskWindow.LoadTask(model);
                    TaskWindow.LoadTasks();
                    TaskWindow.ShowDialog();

                    var TaskForm = new TaskForm(model);
                    TaskForm.LoadTasks(model);

                };



                // Refresh Data menu item
                var refreshItem = new System.Windows.Controls.MenuItem { Header = "Refresh Data" };
                refreshItem.Click += (s, args) =>
                {
                    timer1.IsEnabled = false;
                    timer1.IsEnabled = true;
                };

                // Add items to context menu
                contextMenu.Items.Add(addTaskItem);
                contextMenu.Items.Add(refreshItem);

                // Show the context menu at mouse location
                contextMenu.PlacementTarget = dataGridView2;
                contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
                contextMenu.IsOpen = true;
            }
        }
        private async void LoadMyTasks()
        {
            try
            {
                var tasks = await GetTodayTasks();
                foreach (var task in tasks)
                {
                    int hours = task.Projected / 60;
                    int minutes = task.Projected % 60;
                    task.ETAHH = hours.ToString("D2");
                    task.ETAMM = minutes.ToString("D2");
                }

                taskDataGrid.ItemsSource = tasks;

                await FetchTrackerTask(); // Ensure we sync running task after binding
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error fetching tasks: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private Task<GetTrackerTask> GetRunningTrackerTask()
        {
            return Task.Run(() =>
            {
                int employeeId = LoginWindow.userDetail.employeeid;

                var oAuthParams = new VOAPIOAuthParams
                {
                    BaseUrl = ConfigurationManager.AppSettings["baseurl"].ToString(),
                    Module = $"/UtilizationTracker/GetTrackerTask/{employeeId}"
                };

                var oClient = VOApiRestSharp.GetInstance(oAuthParams);
                string result = oClient.GetModuleResult(LoginWindow.userDetail.access_token);

                if (string.IsNullOrWhiteSpace(result))
                    return null;

                try
                {
                    return JsonConvert.DeserializeObject<GetTrackerTask>(result);
                }
                catch
                {
                    return null;
                }
            });
        }
        private async Task FetchTrackerTask()
        {
            var runningTask = await GetRunningTrackerTask();

            foreach (var item in taskDataGrid.Items)
            {
                if (item is GetTaskListForUser taskItem)
                {
                    if (runningTask != null && runningTask.taskListid == taskItem.id)
                    {
                        taskItem.IsRunning = runningTask.startTime.HasValue;
                        taskItem.TrackerTaskId = runningTask.id;  // 🔥 Assign GetTrackerTask.id here
                    }
                    else
                    {
                        taskItem.IsRunning = false;
                        taskItem.TrackerTaskId = null;
                    }
                }
            }

            taskDataGrid.Items.Refresh();

            // Update button content per task row
            foreach (var row in taskDataGrid.Items)
            {
                var dgRow = taskDataGrid.ItemContainerGenerator.ContainerFromItem(row) as DataGridRow;
                if (dgRow != null)
                {
                    var button = FindChild<System.Windows.Controls.Button>(dgRow, "btnStartStop");
                    if (button != null && row is GetTaskListForUser taskItem)
                    {
                        button.Content = taskItem.IsRunning ? "Stop" : "Start";
                    }
                }
            }

            // Start or Stop Timer based on running task
            if (runningTask != null && runningTask.startTime.HasValue)
            {
                StartTimer(runningTask.startTime.Value);
            }
            else
            {
                StopTimers();
            }
        }

        private void StartTimer(DateTime startTime)
        {
            StopTimers(); // clear previous

            _elapsedTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };

            _elapsedTimer.Tick += (s, e) =>
            {
                var elapsed = DateTime.Now - startTime;

                txtHours.Text = elapsed.Hours.ToString("D2");
                txtMinutes.Text = elapsed.Minutes.ToString("D2");
                txtSeconds.Text = elapsed.Seconds.ToString("D2");
            };

            _elapsedTimer.Start();
        }

        private void StopTimers()
        {
            if (_elapsedTimer != null)
            {
                _elapsedTimer.Stop();
                _elapsedTimer = null;
            }
        }

        private void StartUpdates()
        {
            if (_pollingTimer == null)
            {
                _pollingTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(3)
                };
                _pollingTimer.Tick += async (s, e) => await FetchTrackerTask();
                _pollingTimer.Start();
            }
        }

        private async void btnStartStops_Click(object sender, RoutedEventArgs e)
        {
            if (_isProcessingClick)
                return; // Prevent multiple clicks

            _isProcessingClick = true;

            try
            {
                if (sender is System.Windows.Controls.Button button && button.DataContext is GetTaskListForUser task)
                {
                    int employeeId = LoginWindow.userDetail.employeeid;
                    int companyId = LoginWindow.userDetail.companyId;
                    int branchId = LoginWindow.userDetail.branchId;
                    string token = LoginWindow.userDetail.access_token;
                    string baseUrl = ConfigurationManager.AppSettings["baseurl"]?.ToString();

                    var runningTask = await GetRunningTrackerTask();
                    bool isRunning = task.IsRunning;

                    using (var client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                        AddEditTrackerTask trackerTask;

                        if (isRunning)
                        {
                            // STOP payload
                            trackerTask = new AddEditTrackerTask
                            {
                                id = runningTask?.id ?? 0,
                                employeeid = employeeId,
                                companyid = companyId,
                                branchid = branchId,
                                taskListId = task.id,
                                projectId = task.projectid,
                                subProjectId = task.subprojectid,
                                subProjectCategoryId = task.subProject_Categoryid,
                                activity = task.Priority,
                                button = "stop",
                                cloneID = 0,
                                isAdmin = 1
                            };
                        }
                        else
                        {
                            // START payload
                            trackerTask = new AddEditTrackerTask
                            {
                                id = 0,
                                employeeid = employeeId,
                                companyid = companyId,
                                branchid = branchId,
                                taskListId = task.id,
                                projectId = task.projectid,
                                subProjectId = task.subprojectid,
                                subProjectCategoryId = task.subProject_Categoryid,
                                activity = task.Priority,
                                button = "start",
                                cloneID = 0,
                                isAdmin = 1
                            };
                        }

                        string json = JsonConvert.SerializeObject(trackerTask);
                        var content = new StringContent(json, Encoding.UTF8, "application/json");

                        var response = await client.PostAsync($"{baseUrl}/UtilizationTracker/AddEditTrackerTask", content);
                        if (response.IsSuccessStatusCode)
                        {
                            // Update UI
                            foreach (var item in taskDataGrid.Items)
                            {
                                if (item is GetTaskListForUser t)
                                    t.IsRunning = !isRunning && t == task;
                            }

                            if (isRunning)
                                StopTimers();

                            taskDataGrid.Items.Refresh();
                            await FetchTrackerTask();
                            await FetchAndDisplayTrackerTask();

                            // Removed redirect to portal per user request
                        }
                        else
                        {
                            System.Windows.MessageBox.Show("Failed to update tracker.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }

                    button.IsEnabled = true;
                }
            }
            finally
            {
                _isProcessingClick = false; // Always reset flag
            }
        }

        private async Task<List<GetTaskListForUser>> GetTodayTasks()
        {
            // Prepare today's date
            var today = DateTime.Today;

            // Final list for today's tasks
            var tasksForToday = new List<GetTaskListForUser>();

            // Get Base URL from config
            string baseUrl = ConfigurationManager.AppSettings["baseurl"]?.ToString()?.TrimEnd('/');
            if (string.IsNullOrEmpty(baseUrl))
                throw new Exception("Base URL is missing in configuration.");

            // Ensure user is logged in
            int employeeId = LoginWindow.userDetail?.employeeid ?? 0;
            if (employeeId == 0 || string.IsNullOrEmpty(LoginWindow.userDetail?.access_token))
                throw new Exception("User is not logged in or access token is missing.");

            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(baseUrl + "/");
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", LoginWindow.userDetail.access_token);

                // API call to get all pending tasks for the logged-in user
                string requestUrl = $"Top10TaskList/GetTaskListForUser?EmpID={employeeId}&status=P";
                var response = await client.GetAsync(requestUrl);
                var json = await response.Content.ReadAsStringAsync();

                // Check for API failure
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"API request failed: {response.StatusCode}\nResponse: {json}");
                }

                // Deserialize tasks from JSON
                var allTasks = JsonConvert.DeserializeObject<List<GetTaskListForUser>>(json)
                               ?? new List<GetTaskListForUser>();

                // Filter tasks assigned today
                foreach (var task in allTasks)
                {
                    if (task.AssignDate.Date == today)
                    {
                        tasksForToday.Add(task);
                    }
                }
            }

            return tasksForToday;
        }


        private void taskDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (taskDataGrid.SelectedItem is GetTaskListForUser selectedTask)
            {
                string details =
                    $"ID: {selectedTask.id}\n" +
                    $"Point Person: {selectedTask.PointPerson}\n" +
                    $"Second Person: {selectedTask.SecondPerson}\n" +
                    $"Accountable Person: {selectedTask.AccountablePerson}\n" +
                    $"Project: {selectedTask.Project}\n" +
                    $"SubProject: {selectedTask.SubProject}\n" +
                    $"SubProject Category: {selectedTask.SubProjectCategory}\n" +
                    $"TaskDescription: {selectedTask.Task}\n" +
                    $"Completed: {selectedTask.Completed}\n" +
                    $"Status %: {selectedTask.Status_Percentage}\n" +
                    $"Duration: {selectedTask.Duration}\n" +
                    $"Estimated Time: {selectedTask.ETAHH}:{selectedTask.ETAMM}\n" +
                    $"Priority: {selectedTask.Priority}\n" +
                    $"Assign Date: {selectedTask.AssignDate:dd-MM-yyyy}\n" +
                    $"ETA: {selectedTask.ETA:dd-MM-yyyy HH:mm}";
            }
        }

        private void MyTaskButton_Click(object sender, RoutedEventArgs e)
        {
            if (LoginWindow.userDetail == null || LoginWindow.userDetail.userid == 0)
            {
                System.Windows.MessageBox.Show("Please login first.");
                return;
            }
            TrackerPanel.Visibility = Visibility.Collapsed;
            MyTaskPanel.Visibility = Visibility.Visible;

            LoadMyTasks();
            FetchTrackerTask();
            StartUpdates();
        }

        private void dataGridView2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void Trackerpanel(object sender, RoutedEventArgs e)
        {
            MyTaskPanel.Visibility = Visibility.Collapsed;
            TrackerPanel.Visibility = Visibility.Visible;
        }
        private void CloseMyTaskPanel_Click(object sender, RoutedEventArgs e)
        {
            MyTaskPanel.Visibility = Visibility.Collapsed;
            TrackerPanel.Visibility = Visibility.Visible;
        }

    }
}
