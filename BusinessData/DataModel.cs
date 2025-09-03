using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessData.DataModel
{
    public class ApplicationLogModel
    {
        public int id { get; set; }
        public string appname { get; set; }
        public string apppath { get; set; }
    }

    public class ActiveAppsModel
    {
        public int id { get; set; }
        public string applicationtitle { get; set; }
        public int applicationlogid { get; set; }
        public DateTime start_time { get; set; }
        public DateTime end_time { get; set; }
    }

    public class ActiveLogModel
    {
        public int id { get; set; }
        public string appname { get; set; }
        public string apptitle { get; set; }
        public string appruntime { get; set; }

    }

    public class ActiveAppModel
    {
        public int applicationlog_id { get; set; }
        public string apptitle { get; set; }
        public string appruntime { get; set; }

    }
    public class ActiveAppProcessModel
    {
        public int? id { get; set; }
        public int? processid { get; set; }
        public string processname { get; set; }
        public string processusername { get; set; }
        public string appdescription { get; set; }
        public string apptitle { get; set; }
        public string appexepath { get; set; }
        public string url { get; set; }
        public DateTime? start_time { get; set; }
        public DateTime? end_time { get; set; }
        public double? totaltime_secounds { get; set; }
        public double? idletime_secounds { get; set; }
        public int? votrackerid { get; set; }
        public int? projectid { get; set; }
        public int? subprojectid { get; set; }
        public int? subprojectbranchid { get; set; }
    }
    public class ActiveTrackerAppModel
    {
        public int? processid { get; set; }
        public string processname { get; set; }
        public string appdescription { get; set; } // Property for the app description
        public string apptitle { get; set; }
        public string totaltimesecounds { get; set; }
        public string idletimesecounds { get; set; }
        public string actualsecounds { get; set; }
        public string starttime { get; set; }
        public string endtime { get; set; }

    }
}
