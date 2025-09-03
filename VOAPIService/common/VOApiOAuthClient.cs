using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VOAPIService.common
{
    public class VOAPIOAuthClient
    {
        private static VOAPIOAuthClient client = null;

        private VOAPIOAuthParams oAuthParams;

        public VOAPIOAuthClient(VOAPIOAuthParams oAuthParams)
        {
            this.oAuthParams = oAuthParams;
        }

        public static VOAPIOAuthClient GetInstance(VOAPIOAuthParams oAuthParams)
        {
            client = new VOAPIOAuthClient(oAuthParams);
            return client;
        }

        public static VOAPIOAuthClient GetInstance()
        {
            return client;
        }

        //TODO: Throw exceptions;
        public static void Initialize() { }

        public string GetIAMUrl()
        {
            return oAuthParams.AuthUrl;
        }
        public string GetRefreshTokenURL()
        {
            return GetIAMUrl();
        }

        private VOAPIHTTPConnector GetVOAPIConnector(string url)
        {
            VOAPIHTTPConnector conn = new VOAPIHTTPConnector() { Url = url };
            conn.AddHeader("apikey", oAuthParams.ApiKey);
            return conn;
        }


        private VOAPIAuthtoken GetTokensFromJSON(JObject responseJSON)
        {
            try
            {
                VOAPIAuthtoken tokens = new VOAPIAuthtoken();

                tokens.AccessToken = responseJSON["access_token"].ToString();
                return tokens;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public VOAPIAuthtoken GenerateAccessTokenFromRefreshToken()
        {
            try
            {
                VOAPIHTTPConnector conn = GetVOAPIConnector(GetRefreshTokenURL());


                string response = conn.Post();

                JObject responseJSON = JObject.Parse(response);
                VOAPIAuthtoken tokens = GetTokensFromJSON(responseJSON);
                return tokens;
            }
            catch (Exception e)
            {
                throw e;
            }

        }
    }
}
