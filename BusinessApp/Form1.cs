using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using BusinessData.SQLAccess;
using BusinessData.DataModel;
using System.Configuration;

using System.Collections;
using System.Management;
using System.Diagnostics;
using BusinessService.Common;
using Dapper;
using BrightIdeasSoftware;
using VOWatcher;
using System.IO;
using VOAPIService;
using VOAPIService.common;
using System.Net;
using Newtonsoft.Json;
using System.Globalization;
using VOWatcher.model;
using System.Device.Location;
using System.Timers;

namespace BusinessApp
{
    public partial class Form1 : Form
    {
        public static string appName, prevvalue, prevapp;
		public static int appIdleId;
        public static Stack applnames;
        public static Stack prodnames;
        public static Hashtable applhash;
        public static DateTime applfocustime;
        public static string appltitle;
        public static string tempstr;
		public static string datepara;

		public TimeSpan applfocusinterval;
		public IDapper dapper;
		public static int appId = 0;
		private bool allowshowdisplay = false;
		public static bool isPaused = false; //false; //true - to make click event active
		public static VOWatcherModel datalog;
		LoginForm loginForm = new LoginForm();
		TaskForm taskForm = new TaskForm();
		ContextMenuStrip menuStrip = new ContextMenuStrip();
		ContextMenuStrip menuStriptask = new ContextMenuStrip();

		string authtoken;
		System.Timers.Timer t;
		int h, m, s;
		public bool isCheckedin = false;
		//for form auto hide script
		//protected override void SetVisibleCore(bool value)
		//{
		//	base.SetVisibleCore(allowshowdisplay ? value : allowshowdisplay);
		//}
		public Form1()
        {

			applnames = new Stack();
			prodnames = new Stack();
			applhash = new Hashtable();
			dapper = new Dapperr();
			InitializeComponent();
			//for timer startstop

			t = new System.Timers.Timer();
			t.Interval = 1000;
			t.Elapsed += OnTimeEvent;

			datepara = monthCalendar1.TodayDate.ToString("yyyy-MM-dd");


			if (LoginForm.isLogin)
			{
				authtoken = appToken();
			}
            else
            {
                loginForm.ShowDialog();
            }



            //Header Background Color
            foreach (OLVColumn item in treeListView1.Columns)
			{
				var headerstyle = new HeaderFormatStyle();
				headerstyle.SetBackColor(Color.Gray);
				headerstyle.SetForeColor(Color.White);
				item.HeaderFormatStyle = headerstyle;
			}
			foreach (OLVColumn item in treeListView2.Columns)
			{
				var headerstyle = new HeaderFormatStyle();
				headerstyle.SetBackColor(Color.Gray);
				headerstyle.SetForeColor(Color.White);
				item.HeaderFormatStyle = headerstyle;
			}


			this.treeListView1.ChildrenGetter = delegate (object x)
			{
				VOWatcherModel appinfo = (VOWatcherModel)x;
				try
				{
					return appinfo.GetClassInformation(appinfo.AppWatcherList);
				}
				catch (Exception ex)
				{
					MessageBox.Show(this, ex.Message, "ObjectListViewDemo", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
					return new ArrayList();
				}

			};

			this.treeListView1.CanExpandGetter = delegate (object x) {
				if (x is VOWatcherModel)
				{
					if (((VOWatcherModel)x) != null)
						return true;
					else
						return false;
				}
				else
				{
					return false;
				}
			};

			this.treeListView2.ChildrenGetter = delegate (object x)
			{
				VOWatcherModel appinfo = (VOWatcherModel)x;
				try
				{
					return appinfo.GetClassInformation(appinfo.AppWatcherList);
				}
				catch (Exception ex)
				{
					MessageBox.Show(this, ex.Message, "ObjectListViewDemo", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
					return new ArrayList();
				}

			};

			this.treeListView2.CanExpandGetter = delegate (object x) {
				if (x is VOWatcherModel)
				{
					if (((VOWatcherModel)x) != null)
						return true;
					else
						return false;
				}
				else
				{
					return false;
				}
			};

		}

        private void OnTimeEvent(object sender, ElapsedEventArgs e)
        {
			Invoke(new Action(() =>
			{
				s += 1;
				if (s==60)
                {
					s = 0;
					m += 1;
                }
				if (m==60)
                {
					m = 0;
					h += 1;
                }
				lblcheckin.Text = string.Format("{0}:{1}:{2}", h.ToString().PadLeft(2, '0'), m.ToString().PadLeft(2, '0'), s.ToString().PadLeft(2, '0'));
			}));
        }

        private void Form1_Activated(object sender, EventArgs e)
        {
			datepara = monthCalendar1.TodayDate.ToString("yyyy-MM-dd");
			//monthCalendar1.SelectionRange.Start = Convert.ToDateTime(datepara);
			monthCalendar1.SetDate(Convert.ToDateTime(datepara));
			timer1.Enabled = false;
			LoadAactiveApplication();
			LoadTrackerApplication();
		}

        private void Form1_Deactivate(object sender, EventArgs e)
        {
            if (isPaused == false)
            {
                timer1.Enabled = true;
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
			this.checkBox2.Visible = false;
			this.checkBox1.Visible = true;
			notifyIcon1.Text = "VO Watcher is in InVisible Mode";
		}

        private void timer1_Tick(object sender, EventArgs e)
        {
 
			IntPtr hwnd = APIFuncs.getforegroundWindow();
			Int32 pid = APIFuncs.GetWindowProcessID(hwnd);
			Process p = Process.GetProcessById(pid);
			//This is used to monitor and save active application's  details in Hashtable for future saving in xml file...
			try
			{

				bool isIdle = false;

				appName = p.ProcessName;
				
				appltitle = APIFuncs.ActiveApplTitle().Trim().Replace("\0", "");

				string pp = APIFuncs.GetMainModuleFileName(p);

				ApplicationLogModel applicationLogModel = new ApplicationLogModel();

				if (APIFuncs.GetIdleTime() > 60000)
				{
					MessageBox.Show(hwnd.ToString());
					//appName = "Idle";
                    //applnames = new Stack();
                    //prodnames = new Stack();
                    //applhash = new Hashtable();
                    isIdle = true;
					appltitle = appltitle + " - Idle";
					//appIdleId = p.Id;
					ActiveAppProcess(p, isIdle);
				}

				if (isIdle == false)
				{
                    if (p.Id != 0)
                    {
                        if (prevvalue != null)
                        {
                            string pvalue = "";
                            if (prevvalue.Length > 8)

                                pvalue = prevvalue.Substring(prevvalue.Length - 4);
                            if (pvalue == "Idle")
                            {
                                applnames = new Stack();
                                prodnames = new Stack();
                                applhash = new Hashtable();
                            }
						}
                    }
                    //appIdleId = p.Id;
                    ActiveAppProcess(p, isIdle);
					
					//if (p.Id != 0)
					//{
					//	string ll = p.MainModule.FileVersionInfo.FileName;

					//	int filelenghth = ll.Length;
					//	//if (filelenghth > 100)
					//	//	filelenghth = 100;

					//	applicationLogModel.appname = p.MainModule.FileVersionInfo.ProductName.ToString();
					//	applicationLogModel.apppath = ll.Substring(0, filelenghth);

					//	var id = dapper.Get<int>($"select id from tbl_applicationlog where appname = {"'" + applicationLogModel.appname + "'" }", null, commandType: CommandType.Text);

					//	if (id == 0)
					//	{
					//		var dbparams = new DynamicParameters();
					//		dbparams.Add("appname", applicationLogModel.appname, DbType.String);
					//		dbparams.Add("apppath", applicationLogModel.apppath, DbType.String);
					//		var result = dapper.Insert<int>("insert into tbl_applicationlog(appname, apppath) values(@appname,@apppath)", dbparams, CommandType.Text);
					//	}

					//	if (id != 0)
					//	{
					//		if (!applnames.Contains(appltitle))
					//		{
					//			if (appltitle.Length > 0)
					//			{
					//				if (appltitle != null || appltitle != "" || appltitle != "unknown")
					//				{

					//					DateTime appstarttime = DateTime.Now;
					//					applnames.Push(appltitle);
					//					applhash.Add(appltitle, appstarttime.ToString());
					//					//prevapp = appName;
					//					isNewAppl = true;
					//					var dbparams = new DynamicParameters();
					//					dbparams.Add("apptitle", appltitle, DbType.String);
					//					dbparams.Add("applicationlog_id", id, DbType.Int32);
					//					dbparams.Add("start_time", appstarttime, DbType.DateTime);
					//					dbparams.Add("end_time", DateTime.Now, DbType.DateTime);
					//					dbparams.Add("totalprocesstime", Convert.ToInt32(p.TotalProcessorTime.TotalSeconds), DbType.Int32);
					//					dbparams.Add("usedprocesstime", Convert.ToInt32(p.UserProcessorTime.TotalSeconds), DbType.Int32);
					//					var result = dapper.Insert<int>("insert into tbl_activeapps(apptitle, applicationlog_id, start_time, end_time, totalprocesstime, usedprocesstime) values(@apptitle, @applicationlog_id, @start_time, @end_time, @totalprocesstime, @usedprocesstime)", dbparams, CommandType.Text);
					//				}
					//			}
					//		}
					//		if (prevvalue != appltitle)
					//		{
					//			prevvalue = appltitle;
					//		}
					//		if (prevapp != appName && isNewAppl == false)
					//		{
					//			bool isnew = true;
					//			if (appltitle == null)
					//			{
					//				isnew = false;

					//			}
					//			if (appltitle == "")
					//			{
					//				isnew = false;
					//			}
					//			if (appltitle == "unknown")
					//			{
					//				isnew = false;
					//			}
					//			if (isnew)
					//			{
					//				DateTime appstarttime = DateTime.Now;
					//				applhash.Remove(prevvalue);
					//				applhash.Add(appltitle, appstarttime.ToString());
					//				//prevapp = appName;
					//				isNewAppl = true;
					//				var dbparams = new DynamicParameters();
					//				dbparams.Add("apptitle", appltitle, DbType.String);
					//				dbparams.Add("applicationlog_id", id, DbType.Int32);
					//				dbparams.Add("start_time", appstarttime, DbType.DateTime);
					//				dbparams.Add("end_time", DateTime.Now, DbType.DateTime);
					//				dbparams.Add("totalprocesstime", Convert.ToInt32(p.TotalProcessorTime.TotalSeconds), DbType.Int32);
					//				dbparams.Add("usedprocesstime", Convert.ToInt32(p.UserProcessorTime.TotalSeconds), DbType.Int32);
					//				var result = dapper.Insert<int>("insert into tbl_activeapps(apptitle, applicationlog_id, start_time, end_time, totalprocesstime, usedprocesstime) values(@apptitle, @applicationlog_id, @start_time, @end_time, @totalprocesstime, @usedprocesstime)", dbparams, CommandType.Text);
					//			}
					//		}


					//		if (prevvalue == appltitle)
					//		{
					//			IDictionaryEnumerator en = applhash.GetEnumerator();
					//			while (en.MoveNext())
					//			{
					//				if (en.Key.ToString() == prevvalue)
					//				{
					//					if (prevvalue != null || prevvalue != "" || prevvalue != "unknown")
					//					{
					//						DateTime dt = Convert.ToDateTime(en.Value);
					//						string para = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
					//						var appid = dapper.Update<int>($"update tbl_activeapps set end_time = {"'" + para + "'"}, totalprocesstime = {"" + Convert.ToInt32(p.TotalProcessorTime.TotalSeconds).ToString() + ""}, usedprocesstime = {"" + Convert.ToInt32(p.UserProcessorTime.TotalSeconds).ToString() + ""}  where apptitle = {"'" + prevvalue + "'" } and datetime(start_time) = {"'" + dt.ToString("yyyy-MM-dd HH:mm:ss") + "'" }", null, commandType: CommandType.Text);
					//						appId = appid;
					//						prevapp = appName;
					//					}
					//					break;
					//				}

					//			}

					//		}

					//	}
					//}
					//LoadAactiveApplication();

					//					select appname, apptitle, time(datetime(sum(Cast((
					//						JulianDay(end_time) - JulianDay(start_time)
					//					) * 24 * 60 * 60 As Integer)), 'unixepoch'))  appruntime from tbl_activeapps, tbl_applicationlog where
					//tbl_applicationlog.id = tbl_activeapps.applicationlog_id and
					//apptitle <> '' and apptitle<>'unknown' and
					//date(start_time) = '2021-12-09' group by appname, apptitle

					//TimeSpan tt;
					//appltitle = APIFuncs.ActiveApplTitle().Trim().Replace("\0", "");
					//if (!applnames.Contains(appltitle + "$$$!!!" + appName + "%%%@@@" + appId))
					//{
					//	applnames.Push(appltitle + "$$$!!!" + appName + "%%%@@@" + appId);
					//	applhash.Add(appltitle + "$$$!!!" + appName + "%%%@@@" + appId, 0);
					//	isNewAppl = true;

					//}
					//if (prevvalue != (appltitle + "$$$!!!" + appName + "%%%@@@" + appId))
					//{
					//	IDictionaryEnumerator en = applhash.GetEnumerator();
					//	applfocusinterval = DateTime.Now.Subtract(applfocustime);

					//	while (en.MoveNext())
					//	{
					//		if (en.Key.ToString() == prevvalue)
					//		{
					//			double prevseconds = Convert.ToDouble(en.Value);
					//			applhash.Remove(prevvalue);
					//			applhash.Add(prevvalue, (applfocusinterval.TotalSeconds + prevseconds));
					//			break;
					//		}
					//	}
					//	prevvalue = appltitle + "$$$!!!" + appName + "%%%@@@" + appId;
					//	applfocustime = DateTime.Now;
					//}
					//if (isNewAppl)
					//	applfocustime = DateTime.Now;
				}
			}
			catch (Exception ex)
			{
				
				//IntPtr hwnd = APIFuncs.getforegroundWindow();
				//Int32 pid = APIFuncs.GetWindowProcessID(hwnd);
				//Process p = Process.GetProcessById(pid);
				//MessageBox.Show(p.ProcessName);
				//MessageBox.Show(ex.Message + ":" + ex.StackTrace);
				if(ex.Message == "Access is denied")
                {
					return;
                }



				if (p.Id>0)
				{
					return;
					string query = "SELECT ExecutablePath, ProcessID FROM Win32_Process";
					ManagementObjectSearcher searcher = new ManagementObjectSearcher(query);

					foreach (ManagementObject item in searcher.Get())
					{
						object id = item["ProcessID"];
						object path = item["ExecutablePath"];

						if (path != null && id.ToString() == p.Id.ToString())
						{
							MessageBox.Show(path.ToString());
						}
					}
				}
			}

		}

        private void monthCalendar1_DateSelected(object sender, DateRangeEventArgs e)
        {
			//MessageBox.Show(monthCalendar1.SelectionRange.Start.ToString("yyyy-MM-dd"));
			treeListView1.Roots = null;
			LoadAactiveApplication();
			LoadTrackerApplication();
		}

        private void notifyIcon1_MouseClick(object sender, MouseEventArgs e)
        {
			if (e.Button == System.Windows.Forms.MouseButtons.Left)
			{
				this.allowshowdisplay = true;
				this.Visible = !this.Visible;
			}
		}

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
			this.allowshowdisplay = false;
			this.Visible = !this.Visible;
			e.Cancel = true;
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
			if (this.checkBox1.Checked == false)
			{
				isPaused = true;
				this.timer1.Enabled = false;
				this.checkBox1.Visible = false;
				this.checkBox2.Checked = true;
				this.checkBox2.Visible = true;
			}
		}

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
			if (this.checkBox2.Checked == false)
			{
				isPaused = false;
				this.timer1.Enabled = true;
				this.checkBox2.Visible = false;
				this.checkBox1.Checked = true;
				this.checkBox1.Visible = true;
			}
		}

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            loginForm.Close();
			taskForm.Close();
            //this.Close();
            Application.Exit();
		}

        private void loginToolStripMenuItem_Click(object sender, EventArgs e)
        {
			loginForm = new LoginForm();
			isPaused = true;
			loginForm.ShowDialog();

		}

		public void LoadAactiveApplication()
        {

			//string datepara = DateTime.Now.ToString("yyyy-MM-dd");
			datepara = monthCalendar1.SelectionRange.Start.ToString("yyyy-MM-dd");

			//string ls_sql = "select appname, apptitle, time(datetime(sum(Cast((JulianDay(end_time) - JulianDay(start_time)) * 24 * 60 * 60 As Integer)),'unixepoch'))  appruntime ";
			//ls_sql += "from tbl_activeapps, tbl_applicationlog where tbl_applicationlog.id = tbl_activeapps.applicationlog_id and ";
			//ls_sql += "apptitle<>'' and apptitle<>'unknown' and date(start_time) = '"+ datepara + "' group by appname, apptitle";

			//List<ActiveLogModel> activeApps = dapper.GetAll<ActiveLogModel>(ls_sql, null, CommandType.Text);

			//         if (activeApps.Count > 0)
			//         {
			//             dgappview.DataSource = activeApps;
			//         }

			string ls_sql1 = "select applicationlog_id, appname apptitle, time(datetime(sum(totalsecounds),'unixepoch'))  appruntime from ";
			ls_sql1 += "(select applicationlog_id, id, sum(Cast((JulianDay(end_time) - JulianDay(start_time)) * 24 * 60 * 60 As Integer)) totalsecounds  from tbl_activeapps ";
			ls_sql1 += "where apptitle<>'' and apptitle<>'unknown' and votrackerid is null and date(start_time) = '" + datepara + "' group by applicationlog_id, id) tbl, tbl_applicationlog where ";
			ls_sql1 += "tbl.applicationlog_id = tbl_applicationlog.id group by applicationlog_id";

			List <ActiveAppModel> applicationLogs = dapper.GetAll<ActiveAppModel>(ls_sql1, null, CommandType.Text);

			ArrayList roots = new ArrayList();
			if (applicationLogs.Count > 0)
            {
				for (int i=0; i<applicationLogs.Count;i++)
                {
					VOAppWatcherModel m = new VOAppWatcherModel();
					m.appid = applicationLogs[i].applicationlog_id;
					m.appname = applicationLogs[i].apptitle;
					m.apptitle = applicationLogs[i].apptitle; 
					m.apptime = applicationLogs[i].appruntime;
					VOWatcherModel vo = new VOWatcherModel(m);

					string ls_sql2 = "select tbl_activeapps.id id, appname, apptitle, time(datetime(sum(Cast((JulianDay(end_time) - JulianDay(start_time)) * 24 * 60 * 60 As Integer)),'unixepoch'))  appruntime ";
					ls_sql2 += "from tbl_activeapps, tbl_applicationlog where tbl_applicationlog.id = tbl_activeapps.applicationlog_id and ";
					ls_sql2 += "apptitle<>'' and apptitle<>'unknown' and votrackerid is null and date(start_time) = '" + datepara + "' and appname = '" + applicationLogs[i].apptitle + "' group by appname, apptitle";
					List<ActiveLogModel> activeApps2 = dapper.GetAll<ActiveLogModel>(ls_sql2, null, CommandType.Text);
					foreach(ActiveLogModel item in activeApps2)
                    {
						m = new VOAppWatcherModel();
						//m.appname = item.appname;
						m.appid = item.id;
						m.appname = item.apptitle;
						//m.apptitle = item.apptitle;
						m.apptime = item.appruntime;
						vo.AppWatcherList.Add(m);
					}
					roots.Add(vo);
				}
				treeListView1.Roots = roots;
			}
			else
			{
				treeListView1.Roots = null;
			}

		}

		public void LoadTrackerApplication()
		{

			//string datepara = DateTime.Now.ToString("yyyy-MM-dd");
			string datepara = monthCalendar1.SelectionRange.Start.ToString("yyyy-MM-dd");

			//string ls_sql = "select appname, apptitle, time(datetime(sum(Cast((JulianDay(end_time) - JulianDay(start_time)) * 24 * 60 * 60 As Integer)),'unixepoch'))  appruntime ";
			//ls_sql += "from tbl_activeapps, tbl_applicationlog where tbl_applicationlog.id = tbl_activeapps.applicationlog_id and ";
			//ls_sql += "apptitle<>'' and apptitle<>'unknown' and date(start_time) = '"+ datepara + "' group by appname, apptitle";

			//List<ActiveLogModel> activeApps = dapper.GetAll<ActiveLogModel>(ls_sql, null, CommandType.Text);

			//         if (activeApps.Count > 0)
			//         {
			//             dgappview.DataSource = activeApps;
			//         }

			string ls_sql1 = "select applicationlog_id, appname apptitle, time(datetime(sum(totalsecounds),'unixepoch'))  appruntime from ";
			ls_sql1 += "(select applicationlog_id, id, sum(Cast((JulianDay(end_time) - JulianDay(start_time)) * 24 * 60 * 60 As Integer)) totalsecounds  from tbl_activeapps ";
			ls_sql1 += "where apptitle<>'' and apptitle<>'unknown' and votrackerid is not null and date(start_time) = '" + datepara + "' group by applicationlog_id, id) tbl, tbl_applicationlog where ";
			ls_sql1 += "tbl.applicationlog_id = tbl_applicationlog.id group by applicationlog_id";

			List<ActiveAppModel> applicationLogs = dapper.GetAll<ActiveAppModel>(ls_sql1, null, CommandType.Text);

			ArrayList roots = new ArrayList();
			if (applicationLogs.Count > 0)
			{
				for (int i = 0; i < applicationLogs.Count; i++)
				{
					VOAppWatcherModel m = new VOAppWatcherModel();
					m.appid = applicationLogs[i].applicationlog_id;
					m.appname = applicationLogs[i].apptitle;
					m.apptitle = applicationLogs[i].apptitle;
					m.apptime = applicationLogs[i].appruntime;
					VOWatcherModel vo = new VOWatcherModel(m);

					string ls_sql2 = "select tbl_activeapps.id id, appname, apptitle, time(datetime(sum(Cast((JulianDay(end_time) - JulianDay(start_time)) * 24 * 60 * 60 As Integer)),'unixepoch'))  appruntime ";
					ls_sql2 += "from tbl_activeapps, tbl_applicationlog where tbl_applicationlog.id = tbl_activeapps.applicationlog_id and ";
					ls_sql2 += "apptitle<>'' and apptitle<>'unknown' and votrackerid is not null and date(start_time) = '" + datepara + "' and appname = '" + applicationLogs[i].apptitle + "' group by appname, apptitle";
					List<ActiveLogModel> activeApps2 = dapper.GetAll<ActiveLogModel>(ls_sql2, null, CommandType.Text);
					foreach (ActiveLogModel item in activeApps2)
					{
						m = new VOAppWatcherModel();
						//m.appname = item.appname;
						m.appid = item.id;
						m.appname = item.apptitle;
						//m.apptitle = item.apptitle;
						m.apptime = item.appruntime;
						vo.AppWatcherList.Add(m);
					}
					roots.Add(vo);
				}
				treeListView2.Roots = roots;
			}
			else
            {
				treeListView2.Roots = null;
            }


		}

		private void treeListView1_CellRightClick(object sender, CellRightClickEventArgs e)
        {

			if (e.RowIndex != -1)
            {
				var obj = (TreeListView)sender;
				if(LoginForm.isLogin)
				{
					var data = obj.SelectedObject;
					datalog = (VOWatcherModel)data;
					this.menuStrip = PopupMenu;
					this.menuStrip.Show(Cursor.Position);
				}
			}



		}

        private void logTaskToolStripMenuItem_Click(object sender, EventArgs e)
        {
			taskForm = new TaskForm();
			isPaused = true;
			taskForm.ShowDialog();

		}

        private void refreshToolStripMenuItem_Click(object sender, EventArgs e)
        {
			timer1.Enabled = true;
			timer1.Enabled = false;
        }

        private void treeListView2_CellRightClick(object sender, CellRightClickEventArgs e)
        {
			if (e.RowIndex != -1)
			{
				var obj = (TreeListView)sender;
				if (LoginForm.isLogin)
				{
					var data = obj.SelectedObject;
					datalog = (VOWatcherModel)data;
					this.menuStriptask = PopupMenu2;
					this.menuStriptask.Show(Cursor.Position);
				}
			}
		}

        private void deleteTaskToolStripMenuItem_Click(object sender, EventArgs e)
        {
			//

			VOWatcherModel data = Form1.datalog;
			string result = "";

			if (data.AppWatcherList.Count > 0)
            {
				for ( int i=0;i<data.AppWatcherList.Count; i++)
                {
					var id = dapper.Get<int>($"select votrackerid from tbl_activeapps where id = {"" + data.AppWatcherList[i].appid + "" }", null, commandType: CommandType.Text);
					result = DeleteTask(id);
					var appid = dapper.Update<int>($"update tbl_activeapps set votrackerid = null, projectid = null, subprojectid = null, subprojectbranchid = null where votrackerid = {"" + id + "" }", null, commandType: CommandType.Text);

				}
			}
			else
            {
				var id = dapper.Get<int>($"select votrackerid from tbl_activeapps where id = {"" + data.AppId + "" }", null, commandType: CommandType.Text);
				result  = DeleteTask(id);
				var appid = dapper.Update<int>($"update tbl_activeapps set votrackerid = null, projectid = null, subprojectid = null, subprojectbranchid = null where votrackerid = {"" + id + "" }", null, commandType: CommandType.Text);

			}
			if (result == "")
			{ 
				LoadTrackerApplication();
				MessageBox.Show("Data deleted successfully !!!");
			}else
            {
				MessageBox.Show("Error in Data delete!!!");

			}
		}

        private void ActiveAppProcess(Process p, bool isIdle)
        {
			bool isNewAppl = false;

			if (p.Id != 0)
			{
				if(appIdleId == p.Id)
                {
					isNewAppl = true;
                }else
                {
					isNewAppl = false;
                }

				if (isIdle)
				{
					appIdleId = p.Id;
				}else
                {
					appIdleId = 0;
                }

				string ll = p.MainModule.FileVersionInfo.FileName;
				ApplicationLogModel applicationLogModel = new ApplicationLogModel();

				int filelenghth = ll.Length;
				//if (filelenghth > 100)
				//	filelenghth = 100;
				if (p.MainModule.FileVersionInfo.ProductName!=null)
				{ 
					applicationLogModel.appname = p.MainModule.FileVersionInfo.ProductName.ToString();
				}else
                {
					applicationLogModel.appname = p.ProcessName;

				}
				applicationLogModel.apppath = ll.Substring(0, filelenghth);

				var id = dapper.Get<int>($"select id from tbl_applicationlog where appname = {"'" + applicationLogModel.appname + "'" }", null, commandType: CommandType.Text);

				if (id == 0)
				{
					var dbparams = new DynamicParameters();
					dbparams.Add("appname", applicationLogModel.appname, DbType.String);
					dbparams.Add("apppath", applicationLogModel.apppath, DbType.String);
					var result = dapper.Insert<int>("insert into tbl_applicationlog(appname, apppath) values(@appname,@apppath)", dbparams, CommandType.Text);
				}

				if (id != 0)
				{
					if (!applnames.Contains(appltitle))
					{
						if (appltitle.Length > 0)
						{
							if (appltitle != null || appltitle != "" || appltitle != "unknown")
							{

								DateTime appstarttime = DateTime.Now;
								applnames.Push(appltitle);
								applhash.Add(appltitle, appstarttime.ToString());
								//prevapp = appName;
								isNewAppl = true;
								var dbparams = new DynamicParameters();
								dbparams.Add("apptitle", appltitle, DbType.String);
								dbparams.Add("applicationlog_id", id, DbType.Int32);
								dbparams.Add("start_time", appstarttime, DbType.DateTime);
								dbparams.Add("end_time", DateTime.Now, DbType.DateTime);
								dbparams.Add("totalprocesstime", Convert.ToInt32(p.TotalProcessorTime.TotalSeconds), DbType.Int32);
								dbparams.Add("usedprocesstime", Convert.ToInt32(p.UserProcessorTime.TotalSeconds), DbType.Int32);
								var result = dapper.Insert<int>("insert into tbl_activeapps(apptitle, applicationlog_id, start_time, end_time, totalprocesstime, usedprocesstime) values(@apptitle, @applicationlog_id, @start_time, @end_time, @totalprocesstime, @usedprocesstime)", dbparams, CommandType.Text);
							}
						}
					}
					if (prevvalue != appltitle)
					{
						prevvalue = appltitle;
					}


					//differt appname
					if (prevapp != appName && isNewAppl == false)
					{
						bool isnew = true;
						if (appltitle == null)
						{
							isnew = false;

						}
						if (appltitle == "")
						{
							isnew = false;
						}
						if (appltitle == "unknown")
						{
							isnew = false;
						}
						if (isnew)
						{
							DateTime appstarttime = DateTime.Now;
							applhash.Remove(prevvalue);
							applhash.Add(appltitle, appstarttime.ToString());
							//prevapp = appName;
							isNewAppl = true;
							var dbparams = new DynamicParameters();
							dbparams.Add("apptitle", appltitle, DbType.String);
							dbparams.Add("applicationlog_id", id, DbType.Int32);
							dbparams.Add("start_time", appstarttime, DbType.DateTime);
							dbparams.Add("end_time", DateTime.Now, DbType.DateTime);
							dbparams.Add("totalprocesstime", Convert.ToInt32(p.TotalProcessorTime.TotalSeconds), DbType.Int32);
							dbparams.Add("usedprocesstime", Convert.ToInt32(p.UserProcessorTime.TotalSeconds), DbType.Int32);
							var result = dapper.Insert<int>("insert into tbl_activeapps(apptitle, applicationlog_id, start_time, end_time, totalprocesstime, usedprocesstime) values(@apptitle, @applicationlog_id, @start_time, @end_time, @totalprocesstime, @usedprocesstime)", dbparams, CommandType.Text);
						}
					}


					if (prevvalue == appltitle)
					{
						IDictionaryEnumerator en = applhash.GetEnumerator();
						while (en.MoveNext())
						{
							if (en.Key.ToString() == prevvalue)
							{
								if (prevvalue != null || prevvalue != "" || prevvalue != "unknown")
								{
									DateTime dt = Convert.ToDateTime(en.Value);
									string para = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fffffff");
									var appid = dapper.Update<int>($"update tbl_activeapps set end_time = {"'" + para + "'"}, totalprocesstime = {"" + Convert.ToInt32(p.TotalProcessorTime.TotalSeconds).ToString() + ""}, usedprocesstime = {"" + Convert.ToInt32(p.UserProcessorTime.TotalSeconds).ToString() + ""}  where apptitle = {"'" + prevvalue + "'" } and datetime(start_time) = {"'" + dt.ToString("yyyy-MM-dd HH:mm:ss") + "'" }", null, commandType: CommandType.Text);
									appId = appid;
									prevapp = appName;
								}
								break;
							}

						}

					}

				}
				LoadAactiveApplication();
				LoadTrackerApplication();
			}
		}


        private void checkBox2_MouseHover(object sender, EventArgs e)
        {
			Control senderObject = sender as Control;
			string hoveredControl = senderObject.Tag.ToString();

			// only instantiate a tooltip if the control's tag contains data
			if (hoveredControl != "")
			{
				ToolTip info = new ToolTip
				{
					AutomaticDelay = 500
				};

				string tooltipMessage = string.Empty;

				// add all conditionals here to modify message based on the tag 
				// of the hovered control
				if (hoveredControl == "Start Recording")
				{
					tooltipMessage = "Start Recording";
				}

				info.SetToolTip(senderObject, tooltipMessage);
			}
		}

        private void checkBox1_MouseHover(object sender, EventArgs e)
        {
			Control senderObject = sender as Control;
			string hoveredControl = senderObject.Tag.ToString();

			// only instantiate a tooltip if the control's tag contains data
			if (hoveredControl != "")
			{
				ToolTip info = new ToolTip
				{
					AutomaticDelay = 500
				};

				string tooltipMessage = string.Empty;

				// add all conditionals here to modify message based on the tag 
				// of the hovered control
				if (hoveredControl == "Stop Recording")
				{
					tooltipMessage = "Stop Recording";
				}

				info.SetToolTip(senderObject, tooltipMessage);
			}
		}

        private void timer2_Tick(object sender, EventArgs e)
        {
			var timeUtc = DateTime.UtcNow;
			var easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
			var today = TimeZoneInfo.ConvertTimeFromUtc(timeUtc, easternZone);
			if (!isCheckedin)
			{ 
				button1.Text = "Check-in"+ "\n"+ today.ToString("HH:mm:ss");
			}else
            {
				button1.Text = "Check-out" + "\n" + today.ToString("HH:mm:ss");
			}
		}

        private void button1_Click(object sender, EventArgs e)
        {
			if (LoginForm.isLogin)
			{
//				authtoken = appToken();
				UserClockInModel userClockIn = new UserClockInModel();
				userClockIn.DeviceName = GetSystemName();
				userClockIn.EmployeeId = LoginForm.userDetail.employeeid;
				userClockIn.UserId = LoginForm.userDetail.userid;
				userClockIn.IpAddress = GetIPAddress();
				userClockIn.UserName = LoginForm.userDetail.username;
				userClockIn.Pin = "0";
				VOAPIOAuthParams oAuthParams = new VOAPIOAuthParams();
				oAuthParams.BaseUrl = Convert.ToString(ConfigurationManager.AppSettings["baseurl"].ToString());
				oAuthParams.Module = "/punchdetail/clockinout";
				VOApiRestSharp oClient = VOApiRestSharp.GetInstance(oAuthParams);
				string result = oClient.PostModuleResult(LoginForm.userDetail.access_token, userClockIn);

				var jsonresult = JsonConvert.DeserializeObject<APIResult>(result);

				switch (jsonresult.Message)
				{
					case "Clocked-In successfully.":
						h = 0;
						m = 0;
						s = 0;
						t.Start();
						isCheckedin = true;
						label1.Visible = false;
						MessageBox.Show(jsonresult.Message);
						break;
					case "Clocked-Out successfully.":
						t.Stop();
						isCheckedin = false;
						label1.Visible = true;
						//lblcheckin.Text = "00:00:00";
						break;
					default:
						t.Stop();
						isCheckedin = false;
						label1.Visible = true;
						MessageBox.Show("Error in connection!!!");
						lblcheckin.Text = "00:00:00";
						break;
				}

				
			}
			else
			{
				t.Stop();
				isCheckedin = false;
				label1.Visible = true;
				loginForm.ShowDialog();
				lblcheckin.Text = "00:00:00";
			}

			if(!isCheckedin)
			{
				t.Stop();
				isCheckedin = false;
				label1.Visible = true;
			}
		}

        private void treeListView1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void groupBox1_Enter(object sender, EventArgs e)
        {

        }
        private void comboBox3_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void textBox3_TextChanged(object sender, EventArgs e)
        {

        }


        static string appToken()
        {
            VOAPIOAuthParams oAuthParams = new VOAPIOAuthParams();
            oAuthParams.ApiKey = Convert.ToString(ConfigurationManager.AppSettings["apikey"].ToString());
            oAuthParams.AuthUrl = Convert.ToString(ConfigurationManager.AppSettings["authurl"].ToString());
            VOApiRestSharp oClient = VOApiRestSharp.GetInstance(oAuthParams);
            VOAPIAuthtoken tokens = oClient.GenerateAccessToken();
            string token = tokens.AccessToken;
            return token;
        }

		static string DeleteTask(int id)
        {
			VOAPIOAuthParams oAuthParams = new VOAPIOAuthParams();
			oAuthParams.BaseUrl = Convert.ToString(ConfigurationManager.AppSettings["baseurl"].ToString());
			oAuthParams.Module = "/UtilizationTracker/DeActivateTrackertask/"+id;
			VOApiRestSharp oClient = VOApiRestSharp.GetInstance(oAuthParams);
			string result = oClient.DeleteModuleResult(LoginForm.userDetail.access_token, "");
			return result;
		}

		static string GetIPAddress()
		{
			String address = "";
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

		static string GetSystemName()
        {
			string pcname = System.Environment.MachineName;
			return pcname;

		}
	}
}
