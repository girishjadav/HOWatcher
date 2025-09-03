using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VOAPIService.common
{
    public class VOAPIAuthtoken
    {
        private string accessToken;
        public string AccessToken
        {
            get
            {
                return accessToken;
            }
            set
            {
                accessToken = value;
            }
        }

    }
}
