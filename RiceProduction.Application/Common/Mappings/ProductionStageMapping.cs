using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.Common.Mappings
{
    public class ProductionStageMapping : Profile
    {
        public ProductionStageMapping() 
        {
            CreateMap<ProductionStage, ProductionStageDTO>()
                .ForMember(dest => dest.ProductionStageId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.ProductionPlanTasks, opt => opt.MapFrom(src => src.ProductionPlanTasks ?? new List<ProductionPlanTask>()));
            CreateMap<ProductionStage, ProductionStageDetailDTO>().IncludeBase<ProductionStage, ProductionStageDTO>();
        
        }
    }
}
