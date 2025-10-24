using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Routing.Constraints;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.Common.Mappings
{
    public class ProductionPlanMapping : Profile
    {
        public ProductionPlanMapping() 
        {
            CreateMap<ProductionPlan, ProductionPlanDTO>()
                .ForMember(dest => dest.ProductionPlanId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.Group, opt => opt.MapFrom(src => src.Group))
                .ForMember(d => d.CurrentProductionStageId,
                o => o.MapFrom(s => s.CurrentProductionStages != null && s.CurrentProductionStages.Any()
                ? s.CurrentProductionStages.First().Id : (Guid?)null))
                .ForMember(d => d.CurrentProductionStages,
                 o => o.MapFrom(s => s.CurrentProductionStages != null ? s.CurrentProductionStages : new List<ProductionStage>()));
            CreateMap<ProductionPlan, ProductionPlanDetailDTO>().IncludeBase<ProductionPlan, ProductionPlanDTO>()
                .ForMember(dest => dest.ProductionStages, opt => opt.MapFrom(s => s.CurrentProductionStages));
        }
    }
}
