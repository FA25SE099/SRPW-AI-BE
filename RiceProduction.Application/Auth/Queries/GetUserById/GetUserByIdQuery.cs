using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response;

namespace RiceProduction.Application.Auth.Queries.GetUserById
{
    public class GetUserByIdQuery : IRequest<Result<UserDto>>
    {
        public string UserId { get; set; } = string.Empty;
    }
}
