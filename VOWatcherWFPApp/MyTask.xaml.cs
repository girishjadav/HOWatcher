using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Windows;
using VOWatcherWFPApp.model; // adjust if needed
using Newtonsoft.Json;

namespace VOWatcherWFPApp
{
    public partial class MyTask : Window
    {
        public MyTask()
        {
            InitializeComponent();
            LoadMyTasks();
            this.WindowState = WindowState.Maximized;
        }

        private async void LoadMyTasks()
        {
            try
            {
                var tasks = await GetTodayTasks();
                taskDataGrid.ItemsSource = tasks;
            }
            catch (Exception ex)
            {
               System.Windows.MessageBox.Show($"Error fetching tasks: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task<List<GetTaskListForUser>> GetTodayTasks()
        {
            var today = DateTime.Today;
            var tasksForToday = new List<GetTaskListForUser>();

            int employeeId = LoginWindow.userDetail?.employeeid ?? 0;
            if (employeeId == 0)
                throw new Exception("User is not logged in.");

            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(" /");
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", LoginWindow.userDetail?.access_token);

                var response = await client.GetAsync($"Top10TaskList/GetTaskListForUser?EmpID={employeeId}&status=P");
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    throw new Exception($"API failed: {response.StatusCode}\n{json}");

                var allTasks = JsonConvert.DeserializeObject<List<GetTaskListForUser>>(json);

                // filter tasks for today
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

        private void taskDataGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
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
                    $"Task: {selectedTask.Projected}\n" +
                    $"Priority: {selectedTask.Priority}\n" +
                    $"Assign Date: {selectedTask.AssignDate:dd-MM-yyyy}\n" +
                    $"ETA: {selectedTask.ETA:dd-MM-yyyy HH:mm}";

               
            }
        }
    }
}
