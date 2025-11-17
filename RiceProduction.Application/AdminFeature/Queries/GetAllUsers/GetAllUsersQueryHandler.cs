using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.AdminResponses;
using RiceProduction.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.AdminFeature.Queries.GetAllUsers
{
    public class GetAllUsersQueryHandler : IRequestHandler<GetAllUsersQuery, PagedResult<List<UserResponse>>>
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<GetAllUsersQueryHandler> _logger;

        public GetAllUsersQueryHandler(
            UserManager<ApplicationUser> userManager,
            ILogger<GetAllUsersQueryHandler> logger)
        {
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<PagedResult<List<UserResponse>>> Handle(GetAllUsersQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var query = _userManager.Users.AsQueryable();

                // Apply search email and name filter
                if (!string.IsNullOrWhiteSpace(request.SearchEmailAndName))
                {
                    query = query.Where(u =>
                        (u.FullName != null && u.FullName.ToLower().Contains(request.SearchEmailAndName.ToLower())) ||
                        (u.Email != null && u.Email.ToLower().Contains(request.SearchEmailAndName.ToLower())));
                }

                // Apply search phone number filter
                if (!string.IsNullOrWhiteSpace(request.SearchPhoneNumber))
                {
                    query = query.Where(u => u.PhoneNumber != null && u.PhoneNumber.Contains(request.SearchPhoneNumber));
                }

                // Apply IsActive filter
                if (request.IsActive.HasValue)
                {
                    query = query.Where(u => u.IsActive == request.IsActive.Value);
                }

                // Apply IsActive filter
                if (request.SortBy == Domain.Enums.SortBy.NameAscending)
                {
                    query = query.OrderBy(u => u.FullName);
                }
                else
                {
                    query = query.OrderByDescending(u => u.FullName);
                }

                // If role filter is specified, we need to filter after getting roles
                List<UserResponse> userResponses;
                int totalCount;

                if (!string.IsNullOrWhiteSpace(request.Role))
                {
                    // Get all matching users (without pagination first)
                    var allUsers = await query.ToListAsync(cancellationToken);
                    var filteredUsers = new List<UserResponse>();

                    foreach (var user in allUsers)
                    {
                        var roles = await _userManager.GetRolesAsync(user);
                        var userRole = roles.FirstOrDefault() ?? "None";

                        if (userRole.Equals(request.Role, StringComparison.OrdinalIgnoreCase))
                        {
                            filteredUsers.Add(new UserResponse
                            {
                                Id = user.Id.ToString(),
                                FullName = user.FullName,
                                Email = user.Email ?? string.Empty,
                                PhoneNumber = user.PhoneNumber,
                                Address = user.Address,
                                Role = userRole,
                                IsActive = user.IsActive,
                                IsVerified = user.IsVerified,
                                LastActivityAt = user.LastActivityAt
                            });
                        }
                    }

                    totalCount = filteredUsers.Count;
                    userResponses = filteredUsers
                        .Skip((request.CurrentPage - 1) * request.PageSize)
                        .Take(request.PageSize)
                        .ToList();
                }
                else
                {
                    // Get total count
                    totalCount = await query.CountAsync(cancellationToken);

                    // Apply pagination
                    var users = await query
                        .Skip((request.CurrentPage - 1) * request.PageSize)
                        .Take(request.PageSize)
                        .ToListAsync(cancellationToken);

                    // Map to response
                    userResponses = new List<UserResponse>();
                    foreach (var user in users)
                    {
                        var roles = await _userManager.GetRolesAsync(user);
                        var role = roles.FirstOrDefault() ?? "None";

                        userResponses.Add(new UserResponse
                        {
                            Id = user.Id.ToString(),
                            FullName = user.FullName,
                            Email = user.Email ?? string.Empty,
                            PhoneNumber = user.PhoneNumber,
                            Address = user.Address,
                            Role = role,
                            IsActive = user.IsActive,
                            IsVerified = user.IsVerified,
                            LastActivityAt = user.LastActivityAt
                        });
                    }
                }

                _logger.LogInformation("Retrieved {Count} users out of {Total}", userResponses.Count, totalCount);

                return PagedResult<List<UserResponse>>.Success(
                    userResponses,
                    request.CurrentPage,
                    request.PageSize,
                    totalCount,
                    "Users retrieved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting users");
                return PagedResult<List<UserResponse>>.Failure("An error occurred while processing your request");
            }
        }
    }
}
