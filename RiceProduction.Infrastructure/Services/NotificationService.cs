using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using System.Text.Json;

namespace RiceProduction.Infrastructure.Services
{

    public class NotificationService : INotificationService
    {
        private readonly FirebaseMessaging _fcm;
        private readonly ILogger<NotificationService> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _configuration;

        public NotificationService(ILogger<NotificationService> logger, IUnitOfWork unitOfWork, IConfiguration configuration)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
            _configuration = configuration;

            InitializeFirebase();
            _fcm = FirebaseMessaging.DefaultInstance ?? throw new InvalidOperationException("FirebaseMessaging cannot initialize");
        }

        private void InitializeFirebase()
        {
            if (FirebaseApp.DefaultInstance == null)
            {
                try
                {
                    var serviceAccountJson = _configuration.GetSection("Firebase:ServiceAccountJson").Get<Dictionary<string, object>>();

                    if (serviceAccountJson != null)
                    {
                        var jsonString = JsonSerializer.Serialize(serviceAccountJson);
                        var credential = GoogleCredential.FromJson(jsonString);

                        FirebaseApp.Create(new AppOptions()
                        {
                            Credential = credential
                        });

                        _logger.LogInformation("Firebase initialized from appsettings.json");
                    }
                    else
                    {
                        FirebaseApp.Create(new AppOptions()
                        {
                            Credential = GoogleCredential.GetApplicationDefault()
                        });

                        _logger.LogInformation("Firebase initialized from default credentials");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize Firebase");
                    _logger.LogWarning("Firebase credentials not configured → Push notifications will be disabled");
                }
            }
        }

        public async Task<bool> SendPushAsync(string pushToken, string title, string body, Dictionary<string, string>? data = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(pushToken))
            {
                _logger.LogDebug("Push token empty → Not sending notification.");
                return false;
            }

            if (FirebaseApp.DefaultInstance == null)
            {
                _logger.LogWarning("Firebase not initialized → Cannot send push notification");
                return false;
            }

            var message = new Message
            {
                Token = pushToken,
                Notification = new FirebaseAdmin.Messaging.Notification
                {
                    Title = title,
                    Body = body
                },
                Data = data,

                //Android configuration
                Android = new AndroidConfig
                {
                    Priority = Priority.High,
                    Notification = new AndroidNotification
                    {
                        Sound = "default",
                        ChannelId = "default",
                        Priority = NotificationPriority.HIGH,
                        Icon = "notification_icon",
                        Color = "#4CAF50",
                        Tag = "rice_production"
                    }
                },

                //iOS configuration
                Apns = new ApnsConfig
                {
                    Headers = new Dictionary<string, string>
                        {
                            { "apns-priority", "10" }
                        },
                    Aps = new Aps
                    {
                        Sound = "default",
                        Badge = 1,
                        ContentAvailable = true,
                        MutableContent = true,
                        Alert = new ApsAlert
                        {
                            Title = title,
                            Body = body
                        }
                    }
                },

                //Web push configuration
                Webpush = new WebpushConfig
                {
                    Notification = new WebpushNotification
                    {
                        Title = title,
                        Body = body,
                        Icon = "/icon.png",
                        Badge = "/badge.png"
                    }
                }
            };

            try
            {
                string messageId = await _fcm.SendAsync(message, cancellationToken);
                _logger.LogInformation("Push sent successfully → MessageId: {MessageId}", messageId);
                return true;
            }
            catch (FirebaseException fcmEx)
            {
                _logger.LogError(fcmEx, "FCM error → Token: {TokenStart} | ErrorCode: {ErrorCode}",
                     pushToken.Length > 10 ? pushToken.Substring(0, 10) + "..." : pushToken,
                     fcmEx.ErrorCode);

                if (fcmEx.Message?.Contains("NotRegistered") == true ||
                     fcmEx.Message?.Contains("InvalidRegistration") == true ||
                     fcmEx.Message?.Contains("unregistered") == true ||
                     fcmEx.Message?.Contains("invalid") == true)
                {
                    _logger.LogWarning("FCM token not valid or retrieved -> Delete from DB");
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Undetected error while pushing notification");
                return false;
            }
        }

        public async Task<bool> UpdateNotification(Domain.Entities.Notification notification, bool success, string? errormessage = null)
        {
            try
            {
                notification.PushStatus = success ? "sent" : "failed";
                notification.PushError = errormessage?.Length > 500
                    ? errormessage.Substring(0, 500)
                    : errormessage;
                if (!success)
                {
                    notification.RetryCount++;
                    if (notification.RetryCount < notification.MaxRetries)
                    {
                        notification.NextRetryAt = DateTime.UtcNow.AddMinutes(Math.Pow(2, notification.RetryCount));
                    }
                }
                _unitOfWork.Repository<Domain.Entities.Notification>().Update(notification);
                await _unitOfWork.CompleteAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cannot update Notification status Id = {Id}", notification.Id);
                return false;
            }
        }
    }
}
