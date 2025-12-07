using RiceProduction.Application.Common.Interfaces.External;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace RiceProduction.Infrastructure.Implementation.NotificationImplementation.SpeedSMS
{
  
   public class SpeedSMSAPI : ISmSService
   {
       public const int TYPE_QC = 1;
       public const int TYPE_CSKH = 2;
       public const int TYPE_BRANDNAME = 3;
       public const int TYPE_BRANDNAME_NOTIFY = 4; // Gửi sms sử dụng brandname Notify
       public const int TYPE_GATEWAY = 5; // Gửi sms sử dụng app android từ số di động cá nhân, download app tại đây: https://speedsms.vn/sms-gateway-service/
       private readonly HttpClient _httpClient;
       const String rootURL = "https://api.speedsms.vn/index.php";
       private String accessToken = "S-Kyl0ISrgtM7EArMd8Rfonn5hh-eKl8";

       public SpeedSMSAPI()
       {

       }

       public SpeedSMSAPI(String token)
       {
           this.accessToken = token;
           _httpClient = new HttpClient();
           _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{token}:x")));
           _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
       }

       private string EncodeNonAsciiCharacters(string value)
       {
           System.Text.StringBuilder sb = new System.Text.StringBuilder();
           foreach (char c in value)
           {
               if (c > 127)
               {
                   // This character is too big for ASCII
                   string encodedValue = "\\u" + ((int)c).ToString("x4");
                   sb.Append(encodedValue);
               }
               else
               {
                   sb.Append(c);
               }
           }
           return sb.ToString();
       }

       public String getUserInfo()
       {
           String url = rootURL + "/user/info";
           NetworkCredential myCreds = new NetworkCredential(accessToken, ":x");
           WebClient client = new WebClient();
           client.Credentials = myCreds;
           Stream data = client.OpenRead(url);
           StreamReader reader = new StreamReader(data);
           return reader.ReadToEnd();
       }

       public String sendSMS(String[] phones, String content, int type, string sender)
       {
           String url = rootURL + "/sms/send";
           if (phones.Length <= 0)
               return "";
           if (content.Equals(""))
               return "";

           if (type == TYPE_BRANDNAME && sender.Equals(""))
               return "";

           NetworkCredential myCreds = new NetworkCredential(accessToken, ":x");
           WebClient client = new WebClient();
           client.Credentials = myCreds;
           client.Headers[HttpRequestHeader.ContentType] = "application/json";

           string builder = "{\"to\":[";

           for (int i = 0; i < phones.Length; i++)
           {
               builder += "\"" + phones[i] + "\"";
               if (i < phones.Length - 1)
               {
                   builder += ",";
               }
           }
           builder += "], \"content\": \"" + Uri.EscapeDataString(content) + "\", \"type\":" + type + ", \"sender\": \"" + sender + "\"}";

           String json = builder.ToString();
           return client.UploadString(url, json);
       }
       public async Task<string> SendSMSAsync(string[] phones, string content, int type = TYPE_QC, string sender = "")
       {
           if (phones?.Length == 0 || string.IsNullOrEmpty(content)) return string.Empty;

           if (type == TYPE_BRANDNAME && string.IsNullOrEmpty(sender)) return string.Empty;

           var requestBody = new
           {
               to = phones,
               content = Uri.EscapeDataString(content),
               type,
               sender
           };

           var json = JsonSerializer.Serialize(requestBody);
           var contentBody = new StringContent(json, Encoding.UTF8, "application/json");

           var response = await _httpClient.PostAsync($"{rootURL}/sms/send", contentBody);
           return await response.Content.ReadAsStringAsync();
       }
       public String sendMMS(String[] phones, String content, String link, String sender)
       {
           String url = rootURL + "/mms/send";
           if (phones.Length <= 0)
               return "";
           if (content.Equals(""))
               return "";

           NetworkCredential myCreds = new NetworkCredential(accessToken, ":x");
           WebClient client = new WebClient();
           client.Credentials = myCreds;
           client.Headers[HttpRequestHeader.ContentType] = "application/json";

           string builder = "{\"to\":[";

           for (int i = 0; i < phones.Length; i++)
           {
               builder += "\"" + phones[i] + "\"";
               if (i < phones.Length - 1)
               {
                   builder += ",";
               }
           }
           builder += "], \"content\": \"" + Uri.EscapeDataString(content) + "\", \"link\": \"" + link + "\", \"sender\": \"" + sender + "\"}";

           String json = builder.ToString();
           return client.UploadString(url, json);
       }
   }

}
