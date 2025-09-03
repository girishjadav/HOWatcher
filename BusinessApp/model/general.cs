using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VOWatcher.model
{
    public class general
    {
    }
    public class LoginModel
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class UserDetail
    {
        public string access_token { get; set; }
        public string username { get; set; }
        public int userid { get; set; }
        public int utype { get; set; }
        public int entityId { get; set; }
        public bool isNewUser { get; set; }
        public string userrole { get; set; }
        public string timezone { get; set; }
        public int employeeid { get; set; }
        public string profileimage { get; set; }
        public int branchId { get; set; }
        public int companyId { get; set; }
        public string offset { get; set; }
        public string isTjobstarthr { get; set; }
        public string jobstarthr { get; set; }
    }

    public class TrackerProject
    {
        public int id { get; set; }
        public string project { get; set; }
    }

    public class TrackerSubProject
    {
        public int id { get; set; }
        public string subcategory { get; set; }
    }

    public class TrackerSubProjectBranch
    {
        public int id { get; set; }
        public string subcategory { get; set; }
    }

    public class IpInfo
    {
        [JsonProperty("ip")]
        public string Ip { get; set; }

        [JsonProperty("hostname")]
        public string Hostname { get; set; }

        [JsonProperty("city")]
        public string City { get; set; }

        [JsonProperty("region")]
        public string Region { get; set; }

        [JsonProperty("country")]
        public string Country { get; set; }

        [JsonProperty("loc")]
        public string Loc { get; set; }

        [JsonProperty("org")]
        public string Org { get; set; }

        [JsonProperty("postal")]
        public string Postal { get; set; }
    }

    public class UserClockInModel
    {
        public int UserId { get; set; }
        public int EmployeeId { get; set; }
        public string UserName { get; set; }
        public string Pin { get; set; }
        public string IpAddress { get; set; }
        public string DeviceName { get; set; }
    }
    public class APIResult
    {
        public string Message { get; set; }
    }
    public class ActiveAppProcessPara
    {
        public int processid { get; set; }
        public string processname { get; set; }
        public string processusername { get; set; }
        public string appdescription { get; set; }
        public string apptitle { get; set; }
        public string appexepath { get; set; }
        public string url { get; set; }
        public double totaltime_secounds { get; set; }
        public double idletime_secounds { get; set; }
        public DateTime endtime { get; set; }

    }
    public class AppTimeData
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; }
        public string ProcessUsername { get; set; }
        public string AppDescription { get; set; }
        public string AppTitle { get; set; }
        public string AppExePath { get; set; }
        public string Url { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public double TotalTimeSeconds { get; set; }
        public double IdleTimeSeconds { get; set; }
    }
}
