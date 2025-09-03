using BusinessApp;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using VOAPIService;
using VOAPIService.common;
using VOWatcher.model;
using Dapper;
using BusinessService.Common;
using BusinessData.DataModel;

namespace VOWatcher
{
    public partial class TaskForm : Form
    {
        private IDapper dapper;
        public List<TaskInfoModel> applicationLogs = new List<TaskInfoModel>();
        public bool isGroup = false;
        public TaskForm()
        {
            InitializeComponent();
            dapper = new Dapperr();
            if (LoginForm.isLogin)
            {
                VOAPIOAuthParams oAuthParams = new VOAPIOAuthParams();
                oAuthParams.BaseUrl = Convert.ToString(ConfigurationManager.AppSettings["baseurl"].ToString());
                oAuthParams.Module = "/UtilizationTracker/GetTrackerProject?companyid=1&branchid=1";
                VOApiRestSharp oClient = VOApiRestSharp.GetInstance(oAuthParams);
                string result = oClient.GetModuleResult(LoginForm.userDetail.access_token);
                List<TrackerProject> trackerProjects = JsonConvert.DeserializeObject<List<TrackerProject>>(result);
                if (trackerProjects.Count > 0)
                {
                    comboBox1.ValueMember = "id";
                    comboBox1.DisplayMember = "project";
                    comboBox1.DataSource = trackerProjects;
                }
            }


        }

        private void TaskForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            Form1.isPaused = false;
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            VOAPIOAuthParams oAuthParams = new VOAPIOAuthParams();
            oAuthParams.BaseUrl = Convert.ToString(ConfigurationManager.AppSettings["baseurl"].ToString());
            oAuthParams.Module = "/UtilizationTracker/GetTrackerSubProject/" + comboBox1.SelectedValue;
            VOApiRestSharp oClient = VOApiRestSharp.GetInstance(oAuthParams);
            string result = oClient.GetModuleResult(LoginForm.userDetail.access_token);
            List<TrackerSubProject> subProjects = JsonConvert.DeserializeObject<List<TrackerSubProject>>(result);
            if (subProjects.Count > 0)
            {
                comboBox2.ValueMember = "id";
                comboBox2.DisplayMember = "subcategory";
                comboBox2.DataSource = subProjects;
            }
        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            VOAPIOAuthParams oAuthParams = new VOAPIOAuthParams();
            oAuthParams.BaseUrl = Convert.ToString(ConfigurationManager.AppSettings["baseurl"].ToString());
            oAuthParams.Module = "/UtilizationTracker/GetTrackerSubProjectCategory?projectid=" + comboBox1.SelectedValue + "&subprojectcategoryid=" + comboBox2.SelectedValue;
            VOApiRestSharp oClient = VOApiRestSharp.GetInstance(oAuthParams);
            string result = oClient.GetModuleResult(LoginForm.userDetail.access_token);
            List<TrackerSubProject> subProjects = JsonConvert.DeserializeObject<List<TrackerSubProject>>(result);
            if (subProjects.Count > 0)
            {
                comboBox3.ValueMember = "id";
                comboBox3.DisplayMember = "subcategory";
                comboBox3.DataSource = subProjects;
            }

        }

        private void TaskForm_Load(object sender, EventArgs e)
        {
            VOWatcherModel data = (VOWatcherModel)Form1.datalog;
            string ls_sql = "";
            if (data.AppName == data.AppTitle)
            {
                textBox2.Text = data.AppWatcherList[0].appname;

                ls_sql = "select datetime(max(end_time), '-' || Cast(sum(Cast((JulianDay(end_time) - JulianDay(start_time)) * 24 * 60 * 60 As Integer)) as VARCHAR(255))||' seconds') starttime, ";
                ls_sql += "max(datetime(end_time)) endtime, time(datetime(sum(Cast((JulianDay(end_time) - JulianDay(start_time)) * 24 * 60 * 60 As Integer)) ,'unixepoch'))  appruntime ";
                ls_sql += "from tbl_activeapps where applicationlog_id = (";
                ls_sql += "select applicationlog_id from tbl_activeapps where id = " + data.AppWatcherList[0].appid.ToString() + ") and ";
                ls_sql += "start_time >= '" + Form1.datepara + "' and ";
                ls_sql += "apptitle<>'' and apptitle<>'unknown' and instr(apptitle,'Idle') = 0 and votrackerid is null group by applicationlog_id";
                applicationLogs = dapper.GetAll<TaskInfoModel>(ls_sql, null, CommandType.Text);
                if (applicationLogs.Count > 0)
                {
                    dateTimePicker1.Value = applicationLogs[0].starttime;
                    dateTimePicker2.Value = applicationLogs[0].endtime;
                    textBox3.Text = applicationLogs[0].appruntime;
                    isGroup = true;
                }
            }
            else
            {
                textBox2.Text = data.AppName;
                ls_sql = "select apptitle,datetime(max(end_time), '-' || Cast(sum(Cast((JulianDay(end_time) - JulianDay(start_time)) * 24 * 60 * 60 As Integer)) as VARCHAR(255))||' seconds') starttime, ";
                ls_sql += "max(datetime(end_time)) endtime, time(datetime(sum(Cast((JulianDay(end_time) - JulianDay(start_time)) * 24 * 60 * 60 As Integer)) ,'unixepoch'))  appruntime ";
                ls_sql += "from tbl_activeapps where apptitle = (select apptitle from tbl_activeapps where id = " + data.AppId.ToString() + ") and ";
                ls_sql += "start_time >= (select start_time from tbl_activeapps where id = " + data.AppId.ToString() + ") and ";
                ls_sql += "apptitle<>'' and apptitle<>'unknown' and instr(apptitle,'Idle') = 0 group by apptitle, applicationlog_id";
                applicationLogs = dapper.GetAll<TaskInfoModel>(ls_sql, null, CommandType.Text);

                if (applicationLogs.Count > 0)
                {
                    int cnt = applicationLogs.Count - 1;
                    DateTime dt = applicationLogs[cnt].endtime;
                    dateTimePicker1.Value = dt.AddSeconds(-(TimeSpan.Parse(data.AppTime).TotalSeconds));
                    dateTimePicker2.Value = applicationLogs[cnt].endtime;
                    textBox3.Text = data.AppTime;
                    isGroup = false;
                }


            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            VOWatcherModel data = (VOWatcherModel)Form1.datalog;
            UtilizationTrackerModel trackerdata = new UtilizationTrackerModel();

            trackerdata.ID = Convert.ToInt32(textBox4.Text);
            trackerdata.Companyid = LoginForm.userDetail.companyId;
            trackerdata.Branchid = LoginForm.userDetail.branchId;
            trackerdata.Employeeid = LoginForm.userDetail.employeeid;
            trackerdata.ProjectId = Convert.ToInt32(comboBox1.SelectedValue);
            trackerdata.SubProjectId = Convert.ToInt32(comboBox2.SelectedValue);
            trackerdata.Activity = textBox1.Text;
            trackerdata.StartTime = dateTimePicker1.Value;
            trackerdata.EndTime = dateTimePicker2.Value;
            trackerdata.Duration = textBox3.Text;
            trackerdata.Updateddate = DateTime.Now;
            trackerdata.SubProjectCategoryId = Convert.ToInt32(comboBox3.SelectedValue);
            trackerdata.TaskListid = data.AppId;
            trackerdata.ActiveInvoice = checkBox1.Checked;
            trackerdata.IsAdmin = true;
            trackerdata.NonBillable = null;
            if (isGroup)
            {
                trackerdata.WatcherAppTitle = data.AppWatcherList[0].appname;
            }
            else
            {
                trackerdata.WatcherAppTitle = data.AppName;

            }
            trackerdata.UpdatedBy = LoginForm.userDetail.userid;

            VOAPIOAuthParams oAuthParams = new VOAPIOAuthParams();
            oAuthParams.BaseUrl = Convert.ToString(ConfigurationManager.AppSettings["baseurl"].ToString());
            oAuthParams.Module = "/UtilizationTracker/addwatchertrackertask";
            VOApiRestSharp oClient = VOApiRestSharp.GetInstance(oAuthParams);
            string result = oClient.PostModuleResult(LoginForm.userDetail.access_token, trackerdata);

            if (isGroup)
            {
                for (int i = 0; i < data.AppWatcherList.Count; i++)
                {
                    var appid = dapper.Update<int>($"update tbl_activeapps set votrackerid = {"" + result + ""}, projectid = {"" + comboBox1.SelectedValue + ""}, subprojectid = {"" + comboBox2.SelectedValue + ""}, subprojectbranchid = {"" + comboBox3.SelectedValue + ""} where id = {"" + data.AppWatcherList[i].appid + ""}", null, commandType: CommandType.Text);
                }

            }
            else
            {
                int cnt = applicationLogs.Count - 1;

                string ls_sql = $"update tbl_activeapps set votrackerid = {"" + result + ""}, projectid = {"" + comboBox1.SelectedValue + ""}, subprojectid = {"" + comboBox2.SelectedValue + ""}, subprojectbranchid = {"" + comboBox3.SelectedValue + ""} where apptitle = {"'" + data.AppName + "'"} and start_time >= (select start_time from tbl_activeapps where id = {"" + data.AppId + ""} ) and  applicationlog_id = (select applicationlog_id from tbl_activeapps where id =  {"" + data.AppId + ""} ) and end_time <= {"datetime('" + applicationLogs[cnt].endtime.ToString("yyyy-MM-dd HH:mm:ss") + "', '+1 minutes')"} and votrackerid is null";

                var appid = dapper.Update<int>(ls_sql, null, commandType: CommandType.Text);
            }

             MessageBox.Show("Data saved successfully !!!");

            this.Close();

        }
    }
}
