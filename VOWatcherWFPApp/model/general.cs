using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VOWatcherWFPApp.model
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
        public object Status { get; set; }  // This is the correct one to keep
        public object Data { get; set; }    // This will contain "in" or "out"
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
        public DateTime StartTime { get; set; }
        public DateTime endtime { get; set; }
        public DateTime logdate { get; set; }
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
        public DateTime logdate { get; set; }

    }
    /// <summary>
    /// Model class for day summary data
    /// </summary>
    public class DaySummaryModel
    {
        public double total_seconds { get; set; }
        public double idle_seconds { get; set; }
        public double active_seconds { get; set; }
    }

    public class ClockInStatusResult
    {

        public string Status { get; set; } // e.g., "in" or "out"
        [JsonProperty("shift_start_time")]
        public string CheckInTime { get; set; }
        [JsonProperty("shift_end_time")] 
        public string CheckOutTime { get; set; }
    }

    public class TaskFormModel
    {
        public string AssignedTo { get; set; }
        public string AssignedBy { get; set; }
        public string CostCentre { get; set; }
        public string Project { get; set; }
        public string Category { get; set; }
        public string Task { get; set; }
        public string TaskDescription { get; set; }
        public DateTime AssignDate { get; set; }
        public DateTime? DueDate { get; set; }
        public string Status { get; set; }
        public string Hidden { get; set; }
    }


    public class User
    {
        public int userId { get; set; }
        public int id { get; set; }
        public string name { get; set; }
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
        public string button { get; set; }
        public int SubProjectCategoryId { get; set; }
        public Nullable<int> TaskListid { get; set; }
        public bool ActiveInvoice { get; set; }
        public bool? IsAdmin { get; set; }
        public bool? NonBillable { get; set; }
        public string WatcherAppTitle { get; set; }
        public int? UpdatedBy { get; set; }
    }

    public class projectname

    {
    public int id { get; set; }
    public object companyId { get; set; }
    public object branchId { get; set; }
    public string project { get; set; }
    public object projectDescription { get; set; }
    public object vendorCompany { get; set; }
    public object personName { get; set; }
    public object addressLine1 { get; set; }
    public object addressLine2 { get; set; }
    public object cityId { get; set; }
    public object stateId { get; set; }
    public object countryId { get; set; }
    public object zip { get; set; }
    public object active { get; set; }
}
    public partial class TaskList
    {

        [Column("ID")]
        public int Id { get; set; }
        [Column("Point_Person")]
        public int? PointPerson { get; set; }
        [Column("Second_Person")]
        public int? SecondPerson { get; set; }
        [Column("Accountable_Person")]
        public int? AccountablePerson { get; set; }
        public int? Project { get; set; }
        public int? Subproject { get; set; }
        [Column("SubProject_Category")]
        public int? SubProjectCategory { get; set; }
        [StringLength(300)]
        public string Subject { get; set; }

        [StringLength(2000)]
        public string Task { get; set; }
        [StringLength(50)]
        public string Completed { get; set; }
        [Column(TypeName = "datetime")]
        public DateTime? AssignDate { get; set; }

        [Column("ETA", TypeName = "datetime")]
        public DateTime? Eta { get; set; }

        [Column("ETA_Time")]
        public int EtaTime { get; set; }
        [Column("Actual_Time")]
        public int ActualTime { get; set; }

        [Column("Created_Time", TypeName = "datetime")]
        public DateTime? CreatedTime { get; set; }

        [Column("Updated_Time", TypeName = "datetime")]
        public DateTime? UpdatedTime { get; set; }
        public string etaHH { get; set; }
        public string etaMM { get; set; }
        public bool IsRecurrent { get; set; }


    }

    public partial class GetTaskListForUser
    {
        public int id { get; set; }
        public string PointPerson { get; set; }
        public string SecondPerson { get; set; }
        public string AccountablePerson { get; set; }
        public string Project { get; set; }
        public string SubProject { get; set; }
        public string SubProjectCategory { get; set; }
        public string Task { get; set; }
        public string Completed { get; set; }
        public string Status_Percentage { get; set; }
        public string Duration { get; set; }
        public int Projected { get; set; }
        public int? TrackerTaskId { get; set; }  // To hold GetTrackerTask.id

        public string ETAFormatted
        {
            get
            {
                int hours = Projected / 60;
                int minutes = Projected % 60;
                return $"{hours:D2}:{minutes:D2}";
            }
        }

        public string Priority { get; set; }
        public DateTime AssignDate { get; set; }
        public string ETAHH { get; set; }
        public string ETAMM { get; set; }
        public DateTime ETA { get; set; }
        public int projectid { get; set; }
        public int subprojectid { get; set; }
        public int subProject_Categoryid { get; set; }
        // For UI control
        public bool IsRunning { get; set; } 
        

        public string ButtonContent => IsRunning ? "Stop" : "Start";


    }

    public class AddEditTrackerTask
    {
        public int id { get; set; }
        public int taskListId { get; set; }
        public int projectId { get; set; }
        public int subProjectId { get; set; }
        public int subProjectCategoryId { get; set; }
        public string activity { get; set; }
        public string button { get; set; }
        public int branchid { get; set; }
        public int employeeid { get; set; }
        public int companyid { get; set; }
        public int cloneID { get; set; }
        public int isAdmin { get; set; }
    }
    public class GetTrackerTask
    {
        public int id { get; set; }
        public int companyid { get; set; }
        public int branchid { get; set; }
        public int employeeid { get; set; }
        public int projectId { get; set; }
        public int subProjectId { get; set; }
        public string activity { get; set; }
        public DateTime? startTime { get; set; }
        public DateTime? endTime { get; set; }
        public string duration { get; set; }
        public DateTime? updateddate { get; set; }
        public string button { get; set; }
        public int subProjectCategoryId { get; set; }
        public int? taskListid { get; set; }
        public bool activeInvoice { get; set; }
        public int cloneID { get; set; }
        public bool? isAdmin { get; set; }
        public bool? nonBillable { get; set; }
        public string watcherAppTitle { get; set; }
        public string updatedBy { get; set; }
        public DateTime? activityUpdateddate { get; set; }
        public string activityUpdatedby { get; set; }
    }

  

}




