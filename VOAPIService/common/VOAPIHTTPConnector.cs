using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace VOAPIService.common
{
    class VOAPIHTTPConnector
    {
        private string url;
        private Dictionary<string, string> requestParams = new Dictionary<string, string>();
        private Dictionary<string, string> requestHeaders = new Dictionary<string, string>();

        public string Url
        {
            get
            {
                return url;
            }
            set
            {
                url = value;
            }
        }
        public Dictionary<string, string> RequestHeaders
        {
            get
            {
                return requestHeaders;
            }
            set
            {
                requestHeaders = value;
            }
        }
        public void AddParam(string key, string value)
        {
            requestParams.Add(key, value);
        }

        public void AddHeader(string key, string value)
        {
            RequestHeaders.Add(key, value);
        }

        //TODO: Change the access-modifier to internal;
        public string Post()
        {
            try
            {
                ServicePointManager.Expect100Continue = true;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls
                       | SecurityProtocolType.Tls11
                       | SecurityProtocolType.Tls12
                       | SecurityProtocolType.Ssl3;
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Url);

                string postData = null;
                if (requestParams.Count != 0)
                {
                    foreach (KeyValuePair<string, string> param in requestParams)
                    {
                        if (postData == null)
                        {
                            postData = param.Key + "=" + param.Value;
                        }
                        else
                        {
                            postData += "&" + param.Key + "=" + param.Value;
                        }
                    }
                }
                string u = request.RequestUri.ToString();

                request.UserAgent = "Mozilla/5.0";
                byte[] data = null;
                if (postData != null)
                {
                    data = Encoding.UTF8.GetBytes(postData);
                }
                if (RequestHeaders.Count != 0)
                {
                    foreach (KeyValuePair<string, string> header in RequestHeaders)
                    {
                        if (header.Value != null && string.IsNullOrEmpty(header.Value))
                            request.Headers[header.Key] = header.Value;
                    }
                }

                request.ContentType = "application/x-www-form-urlencoded";
                if (data != null)
                    request.ContentLength = data.Length;
                request.Method = "POST";


                if (data != null)
                { 
                    using (var dataStream = request.GetRequestStream())
                    {
                        dataStream.Write(data, 0, data.Length);
                    }
                }
                var response = (HttpWebResponse)request.GetResponse();

                string responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();

                return responseString;
            }
            catch (WebException e)
            {
                throw e;
            }


        }

        //TODO: Throw Exceptions
        public string Get()
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Url);
                request.UserAgent = "Mozilla/5.0";

                if (RequestHeaders != null && RequestHeaders.Count != 0)
                {
                    foreach (KeyValuePair<string, string> header in RequestHeaders)
                    {
                        request.Headers[header.Key] = header.Value;
                    }
                }

                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                string responseString = "";
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    responseString = reader.ReadToEnd();
                }
                return responseString;
            }
            catch (WebException e)
            {
                throw e;
            }
        }

    }
}
