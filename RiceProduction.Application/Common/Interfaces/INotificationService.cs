using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.Common.Interfaces
{
    public interface INotificationService
    {
        Task<bool> SendPushAsync(
            string pushToken, 
            string title, 
            string body, 
            Dictionary<string, 
                string>? data = null, 
            CancellationToken cancellationToken = default);
        Task<bool> UpdateNotification(
            Notification notification, 
            bool success, 
            string? errormessage = null);
    }
}
