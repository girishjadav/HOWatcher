using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VOAPIService.common
{
    public class VOAPIOAuthParams
    {
        private string apikey;
        private string authurl;
        private string baseurl;
        private string module;
        public VOAPIOAuthParams() { }

        public string ApiKey
        {
            get
            {
                return apikey;
            }
            set
            {
                apikey = value;
            }
        }
        public string AuthUrl
        {
            get
            {
                return authurl;
            }
            set
            {
                authurl = value;
            }
        }
        public string BaseUrl
        {
            get
            {
                return baseurl;
            }
            set
            {
                baseurl = value;
            }
        }        
        public string Module
        {
            get
            {
                return module;
            }
            set
            {
                module = value;
            }
        }
    }
}
