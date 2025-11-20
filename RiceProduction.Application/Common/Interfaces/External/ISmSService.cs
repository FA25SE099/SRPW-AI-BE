using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.Common.Interfaces.External
{
    public interface ISmSService
    {
        string getUserInfo();
        string sendSMS(string[] phones, string content, int type, string sender);
        string sendMMS(string[] phones, string content, string link, string sender);
        Task<string> SendSMSAsync(string[] phones, string content, int type = 3, string sender = "");
    }
}
