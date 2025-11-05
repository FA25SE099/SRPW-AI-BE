using RiceProduction.Application.Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.AgronomyExpertFeature.Commands.CreateAgronomyExpert
{
    public class CreateAgronomyExpertCommand : IRequest<Result<Guid>>
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
    }
}
