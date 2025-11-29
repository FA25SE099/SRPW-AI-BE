using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.NotificationFeature.Command.GetPushToken
{
    public class RegisterPushTokenCommandHandler : IRequestHandler<RegisterPushTokenCommand, Result<string>>
    {
        private readonly ILogger<RegisterPushTokenCommandHandler> _logger;
        private readonly IMemoryCache _cache;
        private static readonly Dictionary<string, List<DeviceInfo>> _userTokens = new();

        public RegisterPushTokenCommandHandler(ILogger<RegisterPushTokenCommandHandler> logger, IMemoryCache cache)
        {
            _logger = logger;
            _cache = cache;
        }

        public async Task<Result<string>> Handle(RegisterPushTokenCommand request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Registering push token for user {UserId}, device type: {DeviceType}",
                    request.UserId, request.DeviceType);

                // Save token to memory cache
                var cacheKey = $"push_tokens_{request.UserId}";

                var deviceInfo = new DeviceInfo
                {
                    PushToken = request.PushToken,
                    DeviceType = request.DeviceType,
                    DeviceModel = request.DeviceModel,
                    AppVersion = request.AppVersion,
                    UserAgent = request.UserAgent,
                    IsActive = true,
                    RegisteredAt = DateTime.UtcNow
                };

                // Get current list of tokens for this user
                if (!_cache.TryGetValue(cacheKey, out List<DeviceInfo>? userDevices) || userDevices == null)
                {
                    userDevices = new List<DeviceInfo>();
                }

                // Remove old token if duplicate
                userDevices.RemoveAll(d => d.PushToken == request.PushToken);

                // Deactivate devices of same type (keep only 1 device per type)
                userDevices.Where(d => d.DeviceType == request.DeviceType)
                    .ToList()
                    .ForEach(d => d.IsActive = false);

                // Add new device
                userDevices.Add(deviceInfo);

                // Save to cache with 24-hour expiration
                _cache.Set(cacheKey, userDevices, TimeSpan.FromDays(1));

                // Save to static dictionary for testing (use Redis in production)
                lock (_userTokens)
                {
                    _userTokens[request.UserId] = userDevices;
                }

                _logger.LogInformation(
                    "Successfully registered push token for user {UserId}. Total devices: {DeviceCount}, Active: {ActiveCount}",
                    request.UserId,
                    userDevices.Count,
                    userDevices.Count(d => d.IsActive));

                return Result<string>.Success(
                    "Push token registered successfully",
                    $"Registered {request.DeviceType} device for user {request.UserId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering push token for user {UserId}", request.UserId);
                return Result<string>.Failure($"Failed to register push token: {ex.Message}");
            }
        }

        // Helper method to get user's active tokens
        public static List<string> GetActiveTokensForUser(string userId)
        {
            lock (_userTokens)
            {
                if (_userTokens.TryGetValue(userId, out var devices))
                {
                    return devices.Where(d => d.IsActive).Select(d => d.PushToken).ToList();
                }
            }
            return new List<string>();
        }
    }

    public class DeviceInfo
    {
        public string PushToken { get; set; } = string.Empty;
        public string DeviceType { get; set; } = string.Empty;
        public string? DeviceModel { get; set; }
        public string? AppVersion { get; set; }
        public string? UserAgent { get; set; }
        public bool IsActive { get; set; }
        public DateTime RegisteredAt { get; set; }
    }
}