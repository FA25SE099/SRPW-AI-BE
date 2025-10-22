using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.Common.Mappings
{
    public class GroupMapping : Profile
    {
        public GroupMapping()
        {
            CreateMap<RiceProduction.Domain.Entities.Group, GroupDTO>()
                .ForMember(dest => dest.GroupId, opt => opt.MapFrom(src => src.Id));
            CreateMap<RiceProduction.Domain.Entities.Group, GroupDetailDTO>().IncludeBase<RiceProduction.Domain.Entities.Group, GroupDTO>()
                .ForMember(dest => dest.Plots, opt => opt.MapFrom(src => src.Plots));
        }
    }
}
