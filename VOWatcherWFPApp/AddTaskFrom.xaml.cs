using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using VOAPIService;
using VOAPIService.common;
using VOWatcherWFPApp.model;



namespace VOWatcherWFPApp
{
    public partial class AddTaskFrom : Window
    {
        private readonly string baseUrl = ConfigurationManager.AppSettings["baseurl"];
     
        public AddTaskFrom()
        {
            InitializeComponent();
            if (LoginWindow.userDetail == null || LoginWindow.userDetail.userid <= 0)
            {
                System.Windows.MessageBox.Show("Please login first.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Loaded += AddTaskFrom_Loaded;
            StartDatePicker.SelectedDate = DateTime.Now;
            EndDatePicker.SelectedDate = DateTime.Now;
            

        }

        private async void AddTaskFrom_Loaded(object sender, RoutedEventArgs e)

        {
            await LoadCostCentresAsync();
            await LoadUsersAsync();
        }


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

            // AssignedTo - normal list of all users
            AssignedTo.ItemsSource = users;
            AssignedTo.DisplayMemberPath = "name";
            AssignedTo.SelectedValuePath = "id";

            // AssignedBy - only show logged-in user
            AssignedBy.ItemsSource = new List<User>
{
    new User
    {
        userId = LoginWindow.userDetail.userid,
        name = LoginWindow.userDetail.username
    }
};
            AssignedBy.DisplayMemberPath = "name";
            AssignedBy.SelectedValuePath = "userId";
            AssignedBy.SelectedValue = LoginWindow.userDetail.userid;
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



        private void Task_TextChanged(object sender, TextChangedEventArgs e) { }
        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (LoginWindow.userDetail == null || LoginWindow.userDetail.userid == 0)
            {
                System.Windows.MessageBox.Show("Please login first.");
                return;
            }

            if (!ValidateForm())
            {
                System.Windows.MessageBox.Show("Please fill in all required fields.");
                return;
            }

            // your existing code
            var taskList = new TaskList
            {

                PointPerson = AssignedTo.SelectedValue != null ? Convert.ToInt32(AssignedTo.SelectedValue) : (int?)null,
                SecondPerson = AssignedTo.SelectedValue != null ? Convert.ToInt32(AssignedTo.SelectedValue) : (int?)null,
                AccountablePerson = LoginWindow.userDetail.userid,
                Project = CostCentre.SelectedValue != null ? Convert.ToInt32(CostCentre.SelectedValue) : (int?)null,
                Subproject = Project.SelectedValue != null ? Convert.ToInt32(Project.SelectedValue) : (int?)null,
                SubProjectCategory = Category.SelectedValue != null ? Convert.ToInt32(Category.SelectedValue) : (int?)null,
                Subject = Task.Text,
                Task = TaskDescription.Text,
                Completed = "Not Started",
                AssignDate = DateTime.Now,
                Eta = DateTime.Now,
                EtaTime = ((Hours.Value ?? 0) * 60) + (Minutes.Value ?? 0),
                ActualTime = 0,
                CreatedTime = DateTime.Now,
                UpdatedTime = DateTime.Now,
                etaHH = (Hours.Value ?? 0).ToString(),
                etaMM = (Minutes.Value ?? 0).ToString(),
                IsRecurrent = false
            };

            var oAuthParams = new VOAPIOAuthParams
            {
                BaseUrl = baseUrl,
                Module = "/Top10TaskList/AddorEditTaskList"
            };

            var oClient = VOApiRestSharp.GetInstance(oAuthParams);
            string result = oClient.PostModuleResult(LoginWindow.userDetail.access_token, taskList);

            System.Windows.MessageBox.Show("Task saved successfully!");
            this.Close();
        }


        private bool ValidateForm()
        {
            bool isValid = true;

            isValid &= AssignedTo.SelectedValue != null;
            isValid &= CostCentre.SelectedValue != null;
            isValid &= Project.SelectedValue != null;
            isValid &= Category.SelectedValue != null;
            isValid &= !string.IsNullOrWhiteSpace(Task.Text);
            isValid &= StartDatePicker.SelectedDate.HasValue;

            int hours = Hours.Value ?? 0;
            int minutes = Minutes.Value ?? 0;
            isValid &= (hours > 0 || minutes > 0);

            return isValid;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void AssignedTo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }


    }
}

