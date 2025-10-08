using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response;

namespace RiceProduction.Application.Auth.Queries.GetUserById
{
    public class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, Result<UserDto>>
    {
        private readonly IIdentityService _identityService;

        public GetUserByIdQueryHandler(IIdentityService identityService)
        {
            _identityService = identityService;
        }

        public async Task<Result<UserDto>> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var user = await _identityService.GetUserAsync(request.UserId);

                if (user == null)
                {
                    return Result<UserDto>.Failure(new[] { "User not found." });
                }

                return Result<UserDto>.Success(user);
            }
            catch (Exception ex)
            {
                return Result<UserDto>.Failure(new[] { $"An error occurred while retrieving the user: {ex.Message}" });
            }
        }
    }
}