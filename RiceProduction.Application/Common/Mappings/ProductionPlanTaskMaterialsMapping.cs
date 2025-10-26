using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.Common.Mappings
{
    public class ProductionPlanTaskMaterialsMapping : Profile
    {
        public ProductionPlanTaskMaterialsMapping()
        {
            CreateMap<ProductionPlanTask, ProductionPlanTaskDTO>()
                .ForMember(dest => dest.ProductionPlanTaskId, opt => opt.MapFrom(src => src.Id));
            CreateMap<ProductionPlanTaskMaterial, ProductionPlanTaskMaterialDTO>()
                .ForMember(dest => dest.ProductionPlanTaskMaterialId, opt => opt.MapFrom(src => src.Id));
        }
        
    }
}
