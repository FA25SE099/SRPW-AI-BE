using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.Common.Mappings
{
    public class AgronomyExpertMapping : Profile
    {
        public AgronomyExpertMapping()
        {
            CreateMap<AgronomyExpert, AgronomyExpertDTO>()
                .ForMember(dest => dest.AgronomyExpertId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.ApprovedProductionPlans, opt => opt.MapFrom(src => src.ApprovedProductionPlans));
        }
    }
}
