using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.Common.Mappings
{
    public class SupervisorMapping : Profile
    {
        public SupervisorMapping()
        {
            CreateMap<Supervisor, SupervisorDTO>()
                .ForMember(dest => dest.SupervisorId, opt => opt.MapFrom(src => src.Id));
        }
    }
}
