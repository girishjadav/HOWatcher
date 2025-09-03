using BusinessData.DataModel;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using BusinessService.Common;
using Dapper;
using VOAPIService;
using VOAPIService.common;
using VOWatcherWFPApp.model;
using VOWatcherWPFApp;

namespace VOWatcherWFPApp
{
    public partial class TaskWindow : Window
    {
        private List<GetTaskListForUser> _tasks = new List<GetTaskListForUser>();
        private TaskFormModel _taskFormModel;
        private IDapper _dapper = new Dapperr();

        public TaskWindow()
        {
            InitializeComponent();
            LoadTasks();
          
        }

        public TaskWindow(TaskFormModel model) : this()
        {
            LoadTask(model);
        }

        public async void LoadTasks()
        {
            try
            {
                int employeeId = LoginWindow.userDetail?.employeeid ?? 0;

                using (var client = new HttpClient())
                {
                    client.BaseAddress = new Uri("https://api.govirtualnow.in/api/");
                    client.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", LoginWindow.userDetail?.access_token);

                    var response = await client.GetAsync($"Top10TaskList/GetTaskListForUser?EmpID={employeeId}&status=P");
                    var json = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        MessageBox.Show($"API failed: {response.StatusCode}\n{json}");
                        return;
                    }

                    _tasks = JsonConvert.DeserializeObject<List<GetTaskListForUser>>(json);
                }

                MyTaskComboBox.ItemsSource = _tasks;
                MyTaskComboBox.DisplayMemberPath = "Priority";
                MyTaskComboBox.SelectedValuePath = "TaskListid";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading tasks: {ex.Message}");
            }
        }



        private void AddTaskButton_Click(object sender, RoutedEventArgs e)
        {
            if (_taskFormModel == null)
            {
                _taskFormModel = new TaskFormModel();  // fallback if null
            }

            var taskForm = new TaskForm(_taskFormModel);
            taskForm.ShowDialog();
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

        private string CalculateDuration()
        {
            if (StartDatePicker.SelectedDate.HasValue && StartTimePicker.Value.HasValue)
            {
                var start = StartDatePicker.SelectedDate.Value.Date + StartTimePicker.Value.Value.TimeOfDay;
                DateTime end = EndDatePicker.SelectedDate.HasValue && EndTimePicker.Value.HasValue
                    ? EndDatePicker.SelectedDate.Value.Date + EndTimePicker.Value.Value.TimeOfDay
                    : DateTime.Now;

                if (end > start)
                {
                    TimeSpan duration = end - start;
                    return duration.ToString(@"hh\:mm\:ss");
                }
            }
            return "00:00:00";
        }

        private string CalculateActualDurationFromTracker()
        {
            if (!StartDatePicker.SelectedDate.HasValue || !StartTimePicker.Value.HasValue)
                return "00:00:00";

            DateTime start = StartDatePicker.SelectedDate.Value.Date + StartTimePicker.Value.Value.TimeOfDay;
            DateTime end = (EndDatePicker.SelectedDate.HasValue && EndTimePicker.Value.HasValue)
                ? EndDatePicker.SelectedDate.Value.Date + EndTimePicker.Value.Value.TimeOfDay
                : DateTime.Now;

            if (end <= start) return "00:00:00";

            // Pull all tracker rows that overlap the interval
            string sql = @"SELECT start_time, end_time, totaltime_secounds, idletime_secounds
                           FROM tbl_activeapplicactions
                           WHERE end_time >= @start AND start_time <= @end";

            var p = new DynamicParameters();
            p.Add("@start", start, DbType.DateTime);
            p.Add("@end", end, DbType.DateTime);

            var rows = _dapper.GetAll<ActiveAppProcessModel>(sql, p, CommandType.Text) ?? new List<ActiveAppProcessModel>();

            double totalActualSeconds = 0;
            foreach (var row in rows)
            {
                if (!row.start_time.HasValue || !row.end_time.HasValue) continue;
                DateTime rs = row.start_time.Value;
                DateTime re = row.end_time.Value;
                if (re <= rs) continue;

                // Overlap with desired interval
                DateTime os = rs > start ? rs : start;
                DateTime oe = re < end ? re : end;
                if (oe <= os) continue;

                double rowTotal = Math.Max(0, row.totaltime_secounds ?? 0);
                double rowIdle = Math.Max(0, row.idletime_secounds ?? 0);
                double rowActual = Math.Max(0, rowTotal - rowIdle);
                double rowSpan = (re - rs).TotalSeconds;
                if (rowSpan <= 0) continue;

                double overlap = (oe - os).TotalSeconds;
                // Pro-rate actual time by overlap portion
                double contribution = rowActual * (overlap / rowSpan);
                if (!double.IsNaN(contribution) && !double.IsInfinity(contribution) && contribution > 0)
                {
                    totalActualSeconds += contribution;
                }
            }

            if (totalActualSeconds <= 0) return "00:00:00";

            TimeSpan ts = TimeSpan.FromSeconds(totalActualSeconds);
            string formatted = string.Format("{0:D2}:{1:D2}:{2:D2}", (int)ts.TotalHours, ts.Minutes, ts.Seconds);
            return formatted;
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {

            try
            {
                if (!LoginWindow.isLogin)
                {
                    System.Windows.MessageBox.Show("Please login first.");

                    return;
                }
                if (!(MyTaskComboBox.SelectedItem is GetTaskListForUser selectedTask))
                {
                    MessageBox.Show("Please select a task from the dropdown.");
                    return;
                }


                // Calculate actual (active) time from tracker data for the selected interval
                string actualDuration = CalculateActualDurationFromTracker();

                var trackerdata = new UtilizationTrackerModel
                {
                    ID = 0,
                    Companyid = LoginWindow.userDetail?.companyId ?? 0,
                    Branchid = LoginWindow.userDetail?.branchId ?? 0,
                    Employeeid = LoginWindow.userDetail?.employeeid ?? 0,
                    ProjectId = selectedTask.projectid,
                    SubProjectId = selectedTask.subprojectid,
                    SubProjectCategoryId = selectedTask.subProject_Categoryid,
                    Activity = selectedTask.Priority,
                    StartTime = GetStartDateTime(),
                    EndTime = GetEndDateTime(),
                    Duration = actualDuration,
                    Updateddate = DateTime.Now,
                    TaskListid = selectedTask.id,
                    ActiveInvoice = true,
                    IsAdmin = true,
                    NonBillable = null,
                    WatcherAppTitle = null,
                    UpdatedBy = null
                };

                var oAuthParams = new VOAPIOAuthParams
                {
                    BaseUrl = ConfigurationManager.AppSettings["baseurl"]?.ToString(),
                    Module = "/UtilizationTracker/addwatchertrackertask"
                };

                var oClient = VOApiRestSharp.GetInstance(oAuthParams);
                string result = oClient.PostModuleResult(LoginWindow.userDetail.access_token, trackerdata);
                string debugData = JsonConvert.SerializeObject(trackerdata, Formatting.Indented);
               
                MessageBox.Show("Data saved successfully!");
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void MyTaskComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MyTaskComboBox.SelectedItem is GetTaskListForUser selectedTask)
            {
                Task.Text = selectedTask.Priority;
                AssignedTo.Text = selectedTask.PointPerson;
                CostCentre.Text = selectedTask.Project;
                Project.Text = selectedTask.SubProject;
                Category.Text = selectedTask.SubProjectCategory;
                Status.Text = selectedTask.Completed;

                Hours.Value = int.TryParse(selectedTask.ETAHH, out var h) ? h : 0;
                Minutes.Value = int.TryParse(selectedTask.ETAMM, out var m) ? m : 0;
            }
        }

        public void LoadTask(TaskFormModel model)
        {
            CostCentre.Text = model.CostCentre;
            Project.Text = model.Project;
            Category.Text = model.Category;
            Task.Text = model.Task;
            TaskDescription.Text = model.TaskDescription;
            Status.Text = model.Status;
            Hidden.Text = model.Hidden;

            AssignedTo.Text = model.AssignedTo;

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

        private void Status_SelectionChanged(object sender, SelectionChangedEventArgs e) { }
        private void Project_TextChanged(object sender, TextChangedEventArgs e) { }
    }
}
