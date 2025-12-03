using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.NotificationFeature.Command.GetPushToken
{
    public class RegisterPushTokenCommand : IRequest<Result<string>>
    {
        [Required(ErrorMessage = "User ID is required")]
        public string UserId { get; set; } = string.Empty;
        [Required(ErrorMessage = "Push token is required")]
        public string PushToken { get; set; } = string.Empty;
        [Required(ErrorMessage = "Device type is required")]
        public string DeviceType { get; set; } = string.Empty;
        public string? DeviceModel { get; set; }
        public string? AppVersion { get; set; }
        public string? UserAgent { get; set; }
    } 
   }
