using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.SupervisorFeature.Queries.GetAllSupervisorForClusterManager
{
    public class GetAllSupervisorQueryValidator : AbstractValidator<GetAllSupervisorQuery>
    {
        public GetAllSupervisorQueryValidator()
        {
            RuleFor(x => x.SearchNameOrEmail)
                .MaximumLength(100).WithMessage("Search term cannot exceed 100 characters")
                .When(x => !string.IsNullOrEmpty(x.SearchNameOrEmail));
            RuleFor(x => x.SearchPhoneNumber)
                .MaximumLength(15).WithMessage("Phone number cannot exceed 15 characters")
                .When(x => !string.IsNullOrEmpty(x.SearchPhoneNumber));
        }
    }
}
