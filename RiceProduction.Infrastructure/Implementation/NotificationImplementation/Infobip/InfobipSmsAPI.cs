using Infobip.Api.Client;
using Infobip.Api.Client.Api;
using Infobip.Api.Client.Model;
using RiceProduction.Application.Common.Interfaces.External;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace RiceProduction.Infrastructure.Implementation.NotificationImplementation.Infobip
{
    public class InfobipSmsAPI : ISmSService
    {
        private readonly SmsApi _smsApi;
        private readonly string _defaultSender;

        public InfobipSmsAPI(string apiKey, string basePath, string defaultSender = "InfoSMS")
        {
            var configuration = new Configuration()
            {
                BasePath = basePath,
                ApiKey = apiKey
            };

            _smsApi = new SmsApi(configuration);
            _defaultSender = defaultSender;
        }

        public InfobipSmsAPI(HttpClient httpClient, string apiKey, string basePath, string defaultSender = "InfoSMS")
        {
            var configuration = new Configuration()
            {
                BasePath = basePath,
                ApiKey = apiKey
            };

            _smsApi = new SmsApi(httpClient, configuration);
            _defaultSender = defaultSender;
        }

        public string getUserInfo()
        {
            throw new NotImplementedException("Infobip does not provide a getUserInfo endpoint. Use account management portal.");
        }

        public string sendSMS(string[] phones, string content, int type, string sender)
        {
            var task = SendSMSAsync(phones, content, type, sender);
            task.Wait();
            return task.Result;
        }

        public async Task<string> SendSMSAsync(string[] phones, string content, int type = 3, string sender = "")
        {
            try
            {
                if (phones == null || phones.Length == 0)
                {
                    return JsonSerializer.Serialize(new
                    {
                        status = "error",
                        message = "Phone numbers are required"
                    });
                }

                if (string.IsNullOrEmpty(content))
                {
                    return JsonSerializer.Serialize(new
                    {
                        status = "error",
                        message = "Content is required"
                    });
                }

                var senderName = string.IsNullOrEmpty(sender) ? _defaultSender : sender;

                var destinations = phones.Select(phone => new SmsDestination(to: phone)).ToList();

                var smsMessage = new SmsMessage(
                    sender: senderName,
                    destinations: destinations,
                    content: new SmsMessageContent(
                        new SmsTextContent(text: content)
                    )
                );

                var smsRequest = new SmsRequest(
                    messages: new List<SmsMessage> { smsMessage }
                );

                var smsResponse = _smsApi.SendSmsMessages(smsRequest);

                return JsonSerializer.Serialize(new
                {
                    status = "success",
                    bulkId = smsResponse.BulkId,
                    messages = smsResponse.Messages.Select(m => new
                    {
                        messageId = m.MessageId,
                        status = new
                        {
                            groupId = m.Status?.GroupId,
                            groupName = m.Status?.GroupName,
                            id = m.Status?.Id,
                            name = m.Status?.Name,
                            description = m.Status?.Description
                        }
                    })
                });
            }
            catch (ApiException ex)
            {
                return JsonSerializer.Serialize(new
                {
                    status = "error",
                    errorCode = ex.ErrorCode,
                    message = ex.ErrorContent,
                    headers = ex.Headers
                });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new
                {
                    status = "error",
                    message = ex.Message
                });
            }
        }

        public string sendMMS(string[] phones, string content, string link, string sender)
        {
            throw new NotImplementedException("MMS is not implemented for Infobip. Use advanced messaging APIs instead.");
        }

        public async Task<SmsDeliveryResult> GetDeliveryReportsAsync(string bulkId = null, string messageId = null, int limit = 50)
        {
            try
            {
                return _smsApi.GetOutboundSmsMessageDeliveryReports(
                    bulkId: bulkId,
                    messageId: messageId,
                    limit: limit
                );
            }
            catch (ApiException ex)
            {
                throw new Exception($"Failed to get delivery reports: {ex.ErrorContent}", ex);
            }
        }

        public async Task<SmsPreviewResponse> PreviewSmsAsync(string text)
        {
            try
            {
                var smsPreviewRequest = new SmsPreviewRequest(text: text);
                return _smsApi.PreviewSmsMessage(smsPreviewRequest);
            }
            catch (ApiException ex)
            {
                throw new Exception($"Failed to preview SMS: {ex.ErrorContent}", ex);
            }
        }
    }
}

