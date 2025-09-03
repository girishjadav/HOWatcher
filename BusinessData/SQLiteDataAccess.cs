using BusinessData.DataModel;
using Dapper;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessData.SQLAccess
{
    public class SQLiteDataAccess
    {

        //using (IDbConnection con = new SQLiteConnection(LoadConnectionString()))
        //{

        //}
        private static string LoadConnectionString(string id="BusinessDBEntity")
        {
            return ConfigurationManager.ConnectionStrings[id].ConnectionString;
        }
        public static void SaveApplicationLog(ApplicationLogModel applicationLogModel)
        {
            using (IDbConnection con = new SQLiteConnection(LoadConnectionString()))
            {
                con.Execute("insert into tbl_applicationlog(appname, apppath) values(@appname,@apppath", applicationLogModel);                
            }

        }
    }
}
