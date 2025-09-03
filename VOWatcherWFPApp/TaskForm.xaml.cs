using BusinessData.DataModel;
using BusinessService.Common;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using VOAPIService;
using VOAPIService.common;
using VOWatcherWFPApp;
using VOWatcherWFPApp.model;

namespace VOWatcherWPFApp
{
    public partial class TaskForm : Window
    {
        private IDapper dapper;
        public List<ActiveTrackerAppModel> applicationLogs;
        public bool isGroup = false;
        public static User user = new User();



// Instead of hardcoding:
private readonly string baseUrl = ConfigurationManager.AppSettings["baseurl"];

    public TaskForm()
        {
            InitializeComponent();
           
            if (!LoginWindow.isLogin)
            {
                LoginWindow loginWindow = new LoginWindow();
                loginWindow.Owner = this;
                bool? result = loginWindow.ShowDialog();

                if (result != true) // User cancelled or failed login
                {
                    MessageBox.Show("You must be logged in to use this form.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                    this.Close(); // Close TaskForm
                    return;
                }
            }

            // Continue loading if login is done
            Loaded += TaskForm_Loaded;
            dapper = new Dapperr();
          
        }

        #region Window Loaded

        public TaskForm(TaskFormModel model) : this()
        {
            LoadTasks(model);
        }

   
        private async void TaskForm_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadCostCentresAsync();
            await LoadUsersAsync();
        }

        #endregion

        #region API Fetch Helpers

        private async Task<List<T>> FetchFromApiAsync<T>(string url)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", LoginWindow.userDetail.access_token);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                   System.Windows.MessageBox.Show("API call failed: " + url);
                    return new List<T>();
                }

                var json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<List<T>>(json);
            }
        }

        private async Task LoadUsersAsync()
        {
            var users = await FetchFromApiAsync<User>($"{baseUrl}/Top10TaskList/GetEmployerName");

            AssignedTo.ItemsSource = users;
            AssignedTo.DisplayMemberPath = "name";
            AssignedTo.SelectedValuePath = "id";

            AssignedBy.ItemsSource = users;
            AssignedBy.DisplayMemberPath = "name";
            AssignedBy.SelectedValuePath = "id";

         
        }

        private async Task LoadCostCentresAsync()
        {
            var costCentres = await FetchFromApiAsync<projectname>(
                $"{baseUrl}/UtilizationTracker/GetTrackerProject?companyid=1&branchid=1");

            if (costCentres.Any())
            {
                CostCentre.ItemsSource = costCentres;
                CostCentre.DisplayMemberPath = "project";   // shows "project" property text
                CostCentre.SelectedValuePath = "id";        // internal value = id
            }
            else
            {
               System.Windows.MessageBox.Show("No cost centres returned from API.");
            }
        }
        #endregion
        #region ComboBox Selection Events

        private async void CostCentre_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CostCentre.SelectedValue is int costCentreId)
            {
                var projects = await FetchFromApiAsync<TrackerSubProject>($"{baseUrl}/UtilizationTracker/GetTrackerSubProject/{costCentreId}");
                Project.ItemsSource = projects;
                Project.DisplayMemberPath = "subcategory";
                Project.SelectedValuePath = "id";
            }
        }

        private async void Project_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CostCentre.SelectedValue is int costCentreId && Project.SelectedValue is int projectId)
            {
                var categories = await FetchFromApiAsync<TrackerSubProjectBranch>($"{baseUrl}/UtilizationTracker/GetTrackerSubProjectCategory?projectid={costCentreId}&subprojectcategoryid={projectId}");
                Category.ItemsSource = categories;
                Category.DisplayMemberPath = "subcategory";
                Category.SelectedValuePath = "id";
            }
        }
        #endregion

        private string CalculateDuration()
        {
            if (StartDatePicker.SelectedDate.HasValue && StartTimePicker.Value.HasValue)
            {
                var start = StartDatePicker.SelectedDate.Value.Date + StartTimePicker.Value.Value.TimeOfDay;

                // use end if user selected, otherwise use now
                DateTime end;
                if (EndDatePicker.SelectedDate.HasValue && EndTimePicker.Value.HasValue)
                {
                    end = EndDatePicker.SelectedDate.Value.Date + EndTimePicker.Value.Value.TimeOfDay;
                }
                else
                {
                    end = DateTime.Now;
                }

                if (end > start)
                {
                    TimeSpan duration = end - start;
                    return duration.ToString(@"hh\:mm\:ss"); // ✅ like "01:23:45"
                }
                else
                {
                    return "00:00:00";
                }
            }
            else
            {
                return "00:00:00";
            }
        }


        #region Button Click Events

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        private  void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!LoginWindow.isLogin)
            {
                System.Windows.MessageBox.Show("Please login first.");

                return;
            }
            if (!ValidateForm())
            {
               System.Windows.MessageBox.Show("Please fill in all required fields.", "Validation Error",System.Windows.MessageBoxButton.OK,System.Windows.MessageBoxImage.Warning);
                return;
            }

            string baseUrl = ConfigurationManager.AppSettings["baseurl"].ToString();

            var trackerdata = new UtilizationTrackerModel
            {
                ID = 0,
                Companyid = LoginWindow.userDetail?.companyId ?? 0,
                Branchid = LoginWindow.userDetail?.branchId ?? 0,
                Employeeid = LoginWindow.userDetail?.employeeid ?? 0,
                ProjectId = Convert.ToInt32(CostCentre.SelectedValue),
                SubProjectId = Convert.ToInt32(Project.SelectedValue),
                Activity = Task.Text,
                StartTime = GetStartDateTime(),
                EndTime = GetEndDateTime(),
                Duration = CalculateDuration(),
                Updateddate = DateTime.Now,
                SubProjectCategoryId = Convert.ToInt32(Category.SelectedValue),
                TaskListid = 0,
                ActiveInvoice = true,
                IsAdmin = true,
                NonBillable = null,
                WatcherAppTitle = null,
                UpdatedBy = null
            };

            var oAuthParams = new VOAPIOAuthParams
            {
                BaseUrl = baseUrl,
                Module = "/UtilizationTracker/addwatchertrackertask"
            };

            var oClient = VOApiRestSharp.GetInstance(oAuthParams);
            string result = oClient.PostModuleResult(LoginWindow.userDetail.access_token, trackerdata);
            string debugData = JsonConvert.SerializeObject(trackerdata, Formatting.Indented);
            MessageBox.Show(debugData); // temporary for inspection

            System.Windows.MessageBox.Show("Data saved successfully!");
            this.Close();
        }

        private DateTime GetStartDateTime()
        {
            return StartDatePicker.SelectedDate.Value.Date + StartTimePicker.Value.Value.TimeOfDay;
        }

        private DateTime? GetEndDateTime()
        {
            if (EndDatePicker.SelectedDate.HasValue && EndTimePicker.Value.HasValue)
                return EndDatePicker.SelectedDate.Value.Date + EndTimePicker.Value.Value.TimeOfDay;
            return null;
        }

       

        private bool ValidateForm()
        {
            return CostCentre.SelectedValue is int ccId && ccId > 0 &&
                   Project.SelectedValue is int projId && projId > 0 &&
                   Category.SelectedValue is int catId && catId > 0 &&
                   !string.IsNullOrWhiteSpace(Task.Text) &&
                   StartDatePicker.SelectedDate.HasValue &&
                   StartTimePicker.Value.HasValue;
        }

        public void LoadTasks(TaskFormModel model)
        {
            CostCentre.Text = model.CostCentre;
            Project.Text = model.Project;
            Category.Text = model.Category;
            Task.Text = model.Task;
            TaskDescription.Text = model.TaskDescription;
            Status.Text = model.Status;
            Hidden.Text = model.Hidden;

            if (!string.IsNullOrWhiteSpace(model.AssignedTo))
                AssignedTo.Text = model.AssignedTo;
            if (!string.IsNullOrWhiteSpace(model.AssignedBy))
                AssignedBy.Text = model.AssignedBy;

            // Set DatePicker and TimePicker
            StartDatePicker.SelectedDate = model.AssignDate;
            StartTimePicker.Value = model.AssignDate;

            if (model.DueDate.HasValue)
            {
                EndDatePicker.SelectedDate = model.DueDate.Value;
                EndTimePicker.Value = model.DueDate.Value;
            }
            else
            {
                EndDatePicker.SelectedDate = null;
                EndTimePicker.Value = null;
            }
        }
        #endregion
        private void AssignTimeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void AssignedTo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void Task_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
    }
}
