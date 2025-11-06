using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.Common.Mappings
{
    public class PlotCultivationMapping : Profile
    {
        public PlotCultivationMapping()
        {
            CreateMap<PlotCultivation, PlotCultivationDTO>()
               .ForMember(dest => dest.PlotCultivationId, opt => opt.MapFrom(src => src.Id))
               .ForMember(dest => dest.CultivationTasks, opt => opt.MapFrom(src => src.CultivationTasks));

            CreateMap<CultivationTask, CultivationTaskDTO>()
                .ForMember(dest => dest.CultivationTaskId, opt => opt.MapFrom(src => src.Id));
        }

    }
}
