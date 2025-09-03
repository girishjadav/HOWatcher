using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

// Required namespaces
using System.Diagnostics;
using System.Management;
using System.Dynamic;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using BusinessApp;
using VOWatcher.model;
using BusinessData.DataModel;
using BusinessService.Common;
using Dapper;


namespace VOWatcher
{
    public partial class TaskManager : Form
    {
        System.Timers.Timer t;
        public IDapper dapper;
        public TaskManager()
        {
            dapper = new Dapperr();
            InitializeComponent();
        }

        private void TaskManager_Load(object sender, EventArgs e)
        {
            // Once the form loads, render the items on the list
            //renderProcessesOnListView();
        }

        /// <summary>
        /// This method renders all the processes of Windows on a ListView with some values and icons.
        /// </summary>
        public void renderProcessesOnListView()
        {
            // Create an array to store the processes
            Process[] processList = Process.GetProcesses();

            // Create an Imagelist that will store the icons of every process
            ImageList Imagelist = new ImageList();

            // Loop through the array of processes to show information of every process in your console
            foreach (Process process in processList)
            {
                // Define the status from a boolean to a simple string
                string status = (process.Responding == true ? "Responding" : "Not responding");

                // Retrieve the object of extra information of the process (to retrieve Username and Description)i
                string appName = process.ProcessName;
                if (appName == "chrome")
                {
                    dynamic extraProcessInfo = GetProcessExtraInformation(process.Id);

                    var appltitle = APIFuncs.ActiveApplTitle().Trim().Replace("\0", "");

                    string pp = APIFuncs.GetMainModuleFileName(process);

                    uint i = APIFuncs.GetIdleTime();

                    // Create an array of string that will store the information to display in our 
                    string[] row = {
                    // 1 Process name
                    process.ProcessName,
                    // 2 Process ID
                    process.Id.ToString(),
                    // 3 Process status
                    status,
                    // 4 Username that started the process
                    extraProcessInfo.Username,
                    // 5 Memory usage
                    BytesToReadableValue(process.PrivateMemorySize64),
                    // 6 Description of the process
                    extraProcessInfo.Description
                };

                    //
                    // As not every process has an icon then, prevent the app from crash
                    try
                    {
                        Imagelist.Images.Add(
                            // Add an unique Key as identifier for the icon (same as the ID of the process)
                            process.Id.ToString(),
                            // Add Icon to the List 
                            Icon.ExtractAssociatedIcon(process.MainModule.FileName).ToBitmap()
                        );
                    }
                    catch { }

                    // Create a new Item to add into the list view that expects the row of information as first argument
                    ListViewItem item = new ListViewItem(row)
                    {
                        // Set the ImageIndex of the item as the same defined in the previous try-catch
                        ImageIndex = Imagelist.Images.IndexOfKey(process.Id.ToString())
                    };

                    // Add the Item
                    listView1.Items.Add(item);
                }
            }

            // Set the imagelist of your list view the previous created list :)
            listView1.LargeImageList = Imagelist;
            listView1.SmallImageList = Imagelist;
        }


        /// <summary>
        /// Method that converts bytes to its human readable value
        /// </summary>
        /// <param name="number"></param>
        /// <returns></returns>
        public string BytesToReadableValue(long number)
        {
            List<string> suffixes = new List<string> { " B", " KB", " MB", " GB", " TB", " PB" };

            for (int i = 0; i < suffixes.Count; i++)
            {
                long temp = number / (int)Math.Pow(1024, i + 1);

                if (temp == 0)
                {
                    return (number / (int)Math.Pow(1024, i)) + suffixes[i];
                }
            }

            return number.ToString();
        }

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

                    // You can return the domain too like (PCDesktop-123123\Username using instead
                    //response.Username = argList[1] + "\\" + argList[0];
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
        public void getrunningAppTime()
        {
            IntPtr hwnd = APIFuncs.getforegroundWindow();
            Int32 pid = APIFuncs.GetWindowProcessID(hwnd);
            Process process = Process.GetProcessById(pid);
            // Define the status from a boolean to a simple string
            string status = (process.Responding == true ? "Responding" : "Not responding");

            // Retrieve the object of extra information of the process (to retrieve Username and Description)i
            string appName = process.ProcessName;
            try
            {

                dynamic extraProcessInfo = GetProcessExtraInformation(process.Id);

                var appltitle = APIFuncs.ActiveApplTitle().Trim().Replace("\0", "");

                string pp = APIFuncs.GetMainModuleFileName(process);
                ActiveAppProcessPara activeAppProcessPara = new ActiveAppProcessPara();
                long c = APIFuncs.GetLastInputTime();
                long tt = APIFuncs.GetTickCount();
                uint i = APIFuncs.GetIdleTime();
                string getUrl = APIFuncs.GetChromeUrl(process);
                var ico = Icon.ExtractAssociatedIcon(process.MainModule.FileName).ToBitmap();
                int idletimesecound = 0;
                if (i > 60000)//above 1 mins
                {                     
                    idletimesecound = Convert.ToInt32(i);                    
                }

                // Create an array of string that will store the information to display in our 

                string[] row = {
                    // 1 Process name
                    process.ProcessName,
                    // 2 Process ID
                    process.Id.ToString(),
                    // 3 Process status
                    status,
                    // 4 Username that started the process
                    extraProcessInfo.Username,
                    // 5 Memory usage
                    BytesToReadableValue(process.PrivateMemorySize64),
                    // 6 Description of the process
                    extraProcessInfo.Description,
                    appltitle,
                    pp,
                    getUrl,
                };



                activeAppProcessPara.processid = process.Id;
                activeAppProcessPara.processname = process.ProcessName;
                activeAppProcessPara.processusername = extraProcessInfo.Username;
                activeAppProcessPara.appdescription = extraProcessInfo.Description;
                activeAppProcessPara.apptitle = appltitle;
                activeAppProcessPara.appexepath = pp;
                activeAppProcessPara.url = getUrl;
                activeAppProcessPara.idletime_secounds = idletimesecound;
                activeAppProcessPara.endtime = DateTime.Now;

                if (process.Id > 0)
                {
                    LogActiveApps(activeAppProcessPara);
                }

            }
            catch
            {
                //any error no entry
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            getrunningAppTime();

        }
        private void LogActiveApps(ActiveAppProcessPara model)
        {
            string datepara = DateTime.Now.ToString("yyyy-MM-dd");
            string ls_sql = "select id, processid, processname, processusername, appdescription, apptitle, appexepath, url, start_time, ";
            ls_sql += "end_time, idletime_secounds, votrackerid, projectid, subprojectid, subprojectbranchid from tbl_activeapplicactions ";
            ls_sql += "where apptitle = '" + model.apptitle + "'  and date(start_time) = '" + datepara + "' "; //and processid = " + model.processid + "
            var data = dapper.Get<ActiveAppProcessModel>(ls_sql, null, commandType: CommandType.Text);

            if (data != null)
            {
                int dd = 0;
                if (model.idletime_secounds > 0)
                {
                    double t = model.idletime_secounds - 60000;
                    double d = 10;
                    if (data.idletime_secounds == 0 || t < 1000)
                    {
                        d = 10 + 60; //EVERY 60 SECOUND TRIGGER + 10 SECOND TIMER TIME
                    }
                     
                    dd = Convert.ToInt32(Math.Round(d, 0));
                }
                var dbparams = new DynamicParameters();
                
                dbparams.Add("end_time", model.endtime, DbType.DateTime);
                dbparams.Add("idletime_secounds", data.idletime_secounds == 0 ? dd : dd + data.idletime_secounds, DbType.Int32);
                
                var result = dapper.Insert<int>($"update tbl_activeapplicactions set end_time = @end_time, idletime_secounds = @idletime_secounds where id = {"" + data.id + ""} ", dbparams, CommandType.Text);
            }
            else
            {
                if (model.apptitle != "unknown-apptitle")
                {
                    var dbparams = new DynamicParameters();
                    dbparams.Add("processid", model.processid, DbType.Int32);
                    dbparams.Add("processname", model.processname, DbType.String);
                    dbparams.Add("processusername", model.processusername, DbType.String);
                    dbparams.Add("appdescription", model.appdescription, DbType.String);
                    dbparams.Add("apptitle", model.apptitle, DbType.String);
                    dbparams.Add("appexepath", model.appexepath, DbType.String);
                    dbparams.Add("url", model.url, DbType.String);
                    dbparams.Add("start_time", DateTime.Now, DbType.DateTime);
                    dbparams.Add("end_time", DateTime.Now, DbType.DateTime);
                    dbparams.Add("idletime_secounds", 0, DbType.Int32);
                    var result = dapper.Insert<int>("insert into tbl_activeapplicactions(processid, processname, processusername, appdescription, apptitle, appexepath, url, start_time, end_time, idletime_secounds) values(@processid, @processname, @processusername, @appdescription, @apptitle, @appexepath, @url, @start_time, @end_time,@idletime_secounds)", dbparams, CommandType.Text);
                }
            }
        }

    }
}