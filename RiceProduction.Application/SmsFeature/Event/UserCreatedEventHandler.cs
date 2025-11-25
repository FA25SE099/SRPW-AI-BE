//using Microsoft.Extensions.Logging;
//using Npgsql.EntityFrameworkCore.PostgreSQL.Query.Expressions.Internal;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using RiceProduction.Application.Common.Interfaces.External;

//namespace RiceProduction.Application.SmsFeature.Event
//{
//    public class UserCreatedEventHandler : INotificationHandler<UserCreatedEvent>
//    {
//        private readonly ISmSService _speedSMS;
//        private readonly ILogger<UserCreatedEventHandler> _logger;

//        public UserCreatedEventHandler(ISmSService speedSMS, ILogger<UserCreatedEventHandler> logger)
//        {
//            _speedSMS = speedSMS;
//            _logger = logger;
//        }

//        public async Task Handle(UserCreatedEvent notification, CancellationToken cancellationToken)
//        {
//            var phones = new[] { notification.PhoneNumber };
//            var content = $"Welcome, {notification.Name}! Your account (Name: {notification. Name}) is ready.";
//            var sender = "RicePlatform";  

//            try
//            {
//                var response = await _speedSMS.SendSMSAsync(phones, content, 3, sender);
//                _logger.LogInformation("SMS sent: {Response}", response);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Failed to send SMS to {Phone}", notification.PhoneNumber);
//            }
//        }
//    }
//}
