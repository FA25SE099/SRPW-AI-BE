using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.Common.Interfaces
{
    public interface ISmSService
    {
        String getUserInfo();
        String sendSMS(String[] phones, String content, int type, String sender);
        String sendMMS(String[] phones, String content, String link, String sender);
        Task<string> SendSMSAsync(string[] phones, string content, int type = 3, string sender = "");
    }
}
