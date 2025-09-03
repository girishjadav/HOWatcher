using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VOWatcher
{
    public class VOAppWatcherModel
    {
        public int appid { get; set; }
        public string appname { get; set; }
        public string apptitle { get; set; }
        public string apptime { get; set; }
    }
    public class VOWatcherModel
    {
        VOAppWatcherModel vOAppWatcher = new VOAppWatcherModel();

        public int AppId
        {
            get { return vOAppWatcher.appid; }
        }
        public string AppName
        {
            get { return vOAppWatcher.appname; }
        }

        public string AppTitle
        {
            get { return vOAppWatcher.apptitle; }
        }

        public string AppTime
        {
            get { return vOAppWatcher.apptime; }
        }

        public List<VOAppWatcherModel> AppWatcherList { get; set; }

        public IEnumerable GetClassInformation(List<VOAppWatcherModel> models)
        {

            ArrayList children = new ArrayList();
            foreach (VOAppWatcherModel x in models)
                children.Add(new VOWatcherModel(x));
            return children;
        }

        public VOWatcherModel(VOAppWatcherModel _vOAppWatcher)
        {
            vOAppWatcher = _vOAppWatcher;
            AppWatcherList = new List<VOAppWatcherModel>();
        }

    }


    public class HOAppTrackerModel
    {
        public int processid { get; set; }
        public string processname { get; set; }
        public string apptitle { get; set; }
        public string apptime { get; set; }
        public string appidletime { get; set; }
        public string appusedtime { get; set; }

    }

    public class HOTrackerModel
    {
        HOAppTrackerModel hotrackermodel = new HOAppTrackerModel();  

        public int ProcessId
        {
            get { return hotrackermodel.processid; }
        }
        public string AppName
        {
            get { return hotrackermodel.processname;}
        }
        public string AppTitle
        {
            get { return hotrackermodel.apptitle; }
        }
        public string AppTime
        {
            get { return hotrackermodel.apptime; }
        }
        public string AppIdleTime
        {
            get { return hotrackermodel.appidletime; }
        }
        public string AppActualTime
        {
            get { return hotrackermodel.appusedtime; }
        }
        public List<HOAppTrackerModel> AppTrackerList { get; set; }
        public IEnumerable GetClassInformation(List<HOAppTrackerModel> models)
        {

            ArrayList children = new ArrayList();
            foreach (HOAppTrackerModel x in models)
                children.Add(new HOTrackerModel(x));
            return children;
        }

        public HOTrackerModel(HOAppTrackerModel _hoAppWatcher)
        {
            hotrackermodel = _hoAppWatcher;
            AppTrackerList = new List<HOAppTrackerModel>();
        }
    }


    public class TaskInfoModel
    {
        public string apptitle { get; set; }
        public DateTime starttime { get; set; }
        public DateTime endtime { get; set; }
        public string appruntime { get; set; }
    }

    public class UtilizationTrackerModel
    {
        public int ID { get; set; }
        public int Companyid { get; set; }
        public int Branchid { get; set; }
        public int Employeeid { get; set; }
        public int ProjectId { get; set; }
        public int SubProjectId { get; set; }
        public string Activity { get; set; }
        public Nullable<System.DateTime> StartTime { get; set; }
        public Nullable<System.DateTime> EndTime { get; set; }
        public string Duration { get; set; }
        public Nullable<System.DateTime> Updateddate { get; set; }
        public int SubProjectCategoryId { get; set; }
        public Nullable<int> TaskListid { get; set; }
        public bool ActiveInvoice { get; set; }
        public bool? IsAdmin { get; set; }
        public bool? NonBillable { get; set; }
        public string WatcherAppTitle { get; set; }
        public int? UpdatedBy { get; set; }
    }


}
