using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.Common.Mappings
{
    public class MappingMaterial : Profile
    {
        public MappingMaterial()
        {
            CreateMap<Material, MaterialDTO>()
                .ForMember(dest => dest.MaterialId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.PricePerMaterial, opt => opt.MapFrom(src =>
                src.MaterialPrices
                .Select(m => m.PricePerMaterial).FirstOrDefault()));

            CreateMap<MaterialPrice, MaterialPriceDTO>()
                .ForMember(dest => dest.MaterialPriceId, opt => opt.MapFrom(src => src.Id));
        }
    }
}
