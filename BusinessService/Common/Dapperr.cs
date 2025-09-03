using Dapper;
using System.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using System.Linq;
using System.Threading.Tasks;

namespace BusinessService.Common
{
    public class Dapperr : IDapper
    {

        private string Connectionstring = "BusinessDBEntities";

        public Dapperr()
        {

        }
        public void Dispose()
        {

        }
        private string LoadConnectionString(string id = "BusinessDBEntities")
        {
            return ConfigurationManager.ConnectionStrings[id].ConnectionString;
        }
        public DbConnection GetDbconnection()
        {
            return new SQLiteConnection(ConfigurationManager.ConnectionStrings[Connectionstring].ConnectionString);
        }
        public int Execute(string sp, DynamicParameters parms, CommandType commandType = CommandType.StoredProcedure)
        {
            throw new NotImplementedException();
        }

        public T Get<T>(string sp, DynamicParameters parms, CommandType commandType = CommandType.Text)
        {
            using (IDbConnection db = new SQLiteConnection(LoadConnectionString()))
            {
                return db.Query<T>(sp, parms, commandType: commandType).FirstOrDefault();
            }
        }
        public List<T> GetAll<T>(string sp, DynamicParameters parms, CommandType commandType = CommandType.StoredProcedure)
        {
            using (IDbConnection db = new SQLiteConnection(LoadConnectionString()))
            {
                return db.Query<T>(sp, parms, commandType: commandType).ToList();
            }
        }
        public T Insert<T>(string sp, DynamicParameters parms, CommandType commandType = CommandType.StoredProcedure)
        {
            T result;
            using (IDbConnection db = new SQLiteConnection(LoadConnectionString()))
            {
                try
                {
                    if (db.State == ConnectionState.Closed)
                        db.Open();
                    //db.Execute("insert into tbl_applicationlog(appname, apppath) values('Visual Basic','')");
                    using (var tran = db.BeginTransaction())
                    {
                        try
                        {
                            result = db.Query<T>(sp, parms, commandType: commandType, transaction: tran).FirstOrDefault();
                            tran.Commit();
                        }
                        catch (Exception ex)
                        {
                            tran.Rollback();
                            throw ex;
                        }
                    }

                }
                catch (Exception ex)
                {
                    throw ex;
                }
                finally
                {
                    if (db.State == ConnectionState.Open)
                        db.Close();
                }

                return result;
            }
        }

        public T Update<T>(string sp, DynamicParameters parms, CommandType commandType = CommandType.StoredProcedure)
        {
            T result;
            using (IDbConnection db = new SQLiteConnection(LoadConnectionString()))
            {
                try
                {
                    if (db.State == ConnectionState.Closed)
                        db.Open();

                    using (var tran = db.BeginTransaction())
                    {
                        try
                        {
                            result = db.Query<T>(sp, parms, commandType: commandType, transaction: tran).FirstOrDefault();
                            tran.Commit();
                        }
                        catch (Exception ex)
                        {
                            tran.Rollback();
                            throw ex;
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
                finally
                {
                    if (db.State == ConnectionState.Open)
                        db.Close();
                }

                return result;
            }

        }
    }
}

