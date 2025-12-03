using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentValidation;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.NotificationFeature.Command.PushNotification
{
    public class SendPushNotificationCommand : IRequest<SendPushNotificationResult>
    {
        [Required(ErrorMessage = "Push token is required")]
        public string PushToken { get; set; } = string.Empty;
        [Required(ErrorMessage = "User ID is required")]
        public string UserId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Title is required")]
        [StringLength(100, ErrorMessage = "Title cannot exceed 100 characters")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Body is required")]
        [StringLength(500, ErrorMessage = "Body cannot exceed 500 characters")]
        public string Body { get; set; } = string.Empty;

        public Dictionary<string, string>? Data { get; set; }
        public Guid? NotificationId { get; set; }
        public bool SaveToDatabase { get; set; } = true;
        public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
        public int? BadgeCount { get; set; }
        public string? Sound { get; set; } = "default";
        public string? ChannelId { get; set; } = "default";
        public string? Tag { get; set; }
        public int? TimeToLive { get; set; }
        public bool SendImmediately { get; set; } = true;
        public DateTime? ScheduledTime { get; set; }
    }


    public enum NotificationPriority
    {
        Normal = 0,
        High = 1
    }

    public class SendPushNotificationResult
    {
        public bool Success { get; set; }
        public string? MessageId { get; set; }
        public string? ErrorMessage { get; set; }
        public Guid? NotificationId { get; set; }
        public DateTime SentAt { get; set; }
        public bool IsTokenValid { get; set; } = true;
        public int? RetryCount { get; set; }
        public DateTime? NextRetryAt { get; set; }
        public string? TechnicalDetails { get; set; }
        public int DevicesSentTo { get; set; }
        public int TotalDevices { get; set; }
    }
}
