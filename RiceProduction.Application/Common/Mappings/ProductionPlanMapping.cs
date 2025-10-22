using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.Common.Mappings
{
    public class ProductionPlanMapping : Profile
    {
        public ProductionPlanMapping() 
        {
            CreateMap<ProductionPlan, ProductionPlanDTO>()
                .ForMember(dest => dest.ProductionPlanId, opt => opt.MapFrom(src => src.Id));
            CreateMap<ProductionPlan, ProductionPlanDetailDTO>().IncludeBase<ProductionPlan, ProductionPlanDTO>()
                .ForMember(dest => dest.ProductionStages, opt => opt.MapFrom(s => s.CurrentProductionStages));
        }
    }
}
