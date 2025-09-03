using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VOAPIService.common;

namespace VOAPIService
{
    public class VOApiRestSharp
    {
        private VOAPIOAuthParams oAuthParams;
        private static VOApiRestSharp client = null;

        public VOApiRestSharp(VOAPIOAuthParams oAuthParams)
        {
            this.oAuthParams = oAuthParams;
        }

        public static VOApiRestSharp GetInstance(VOAPIOAuthParams oAuthParams)
        {
            client = new VOApiRestSharp(oAuthParams);
            return client;
        }

        public static VOApiRestSharp GetInstance()
        {
            return client;
        }
        public string GetBaseUrl()
        {
            return oAuthParams.BaseUrl;
        }
        public string GetModuleURL()
        {
            return GetBaseUrl() + oAuthParams.Module;
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

        public VOAPIAuthtoken GenerateAccessToken()
        {
            try
            {

                var client = new RestClient(oAuthParams.AuthUrl);
                client.Timeout = -1;
                var request = new RestRequest(Method.POST);


                request.AddHeader("apikey", oAuthParams.ApiKey);
                var response = client.Execute(request);
                JObject responseJSON = JObject.Parse(response.Content);
                VOAPIAuthtoken tokens = GetTokensFromJSON(responseJSON);
                return tokens;
            }
            catch (Exception e)
            {
                throw e;
            }

        }

        public bool IsLogin(string user, string pwd)
        {
            bool result = false;
            string url = GetModuleURL();
            var client = new RestClient(url);
            client.Timeout = -1;
            var request = new RestRequest(Method.POST);
            request.AddHeader("Content-Type", "application/json");
            var body = new { email=user, password=pwd };
            var data = System.Text.Json.JsonSerializer.Serialize(body);
            request.AddParameter("application/json", data, ParameterType.RequestBody);
            var response = client.Execute(request);
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                result = true;
            }
            else
            {
                result = false;
            }
            return result;
            
        }

        public string Login(string user, string pwd)
        {
            //string result = "";
            string url = GetModuleURL();
            var client = new RestClient(url);
            client.Timeout = -1;
            var request = new RestRequest(Method.POST);
            request.AddHeader("Content-Type", "application/json");
            var body = new { email = user, password = pwd };
            var data = System.Text.Json.JsonSerializer.Serialize(body);
            request.AddParameter("application/json", data, ParameterType.RequestBody);
            var response = client.Execute(request);
            string result = response.Content;
            return result;
        }

        public string GetModuleResult(string token)
        {
            //string result = "";
            string url = GetModuleURL();
            var client = new RestClient(url);
            client.Timeout = -1;
            var request = new RestRequest(Method.GET);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Authorization", "Bearer " + token);
            var response = client.Execute(request);
            string result = response.Content;
            return result;
        }

        public string PostModuleResult(string token, object data)
        {
            //string result = "";
            string url = GetModuleURL();
            var client = new RestClient(url);
            client.Timeout = -1;
            var request = new RestRequest(Method.POST);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Authorization", "Bearer " + token);
            //var formdata = System.Text.Json.JsonSerializer.Serialize(data.ToString());
            var formdata = JsonConvert.SerializeObject(data);
            request.AddParameter("application/json", formdata, ParameterType.RequestBody);
            var response = client.Execute(request);
            string result = response.Content;
            return result;
        }

        public string DeleteModuleResult(string token, object data)
        {
            //string result = "";
            string url = GetModuleURL();
            var client = new RestClient(url);
            client.Timeout = -1;
            var request = new RestRequest(Method.DELETE);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Authorization", "Bearer " + token);
            //var formdata = System.Text.Json.JsonSerializer.Serialize(data.ToString());
            var formdata = JsonConvert.SerializeObject(data);
            request.AddParameter("application/json", formdata, ParameterType.RequestBody);
            var response = client.Execute(request);
            string result = response.Content;
            return result;
        }

    }
}
