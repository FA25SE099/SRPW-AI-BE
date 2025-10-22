using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using AutoMapper;
using NetTopologySuite.IO;
using NetTopologySuite.Geometries;

namespace RiceProduction.Application.Common.Mappings
{
    public class PlotMapping : Profile
    {
        public PlotMapping()
        {
            CreateMap<Plot, PlotDTO>()
                .ForMember(dest => dest.PlotId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.FarmerName, opt => opt.MapFrom(src => src.Farmer != null ? src.Farmer.FullName : null))
                .ForMember(dest => dest.VarietyName, opt => opt.MapFrom(src => src.Group != null && src.Group.RiceVariety != null ? src.Group.RiceVariety.VarietyName : string.Empty))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status))
                .ForMember(dest => dest.BoundaryGeoJson, opt => opt.MapFrom(src => src.Boundary))
                .ForMember(dest => dest.CoordinateGeoJson, opt => opt.MapFrom(src => src.Coordinate));
            CreateMap<Plot, PlotDetailDTO>().IncludeBase<Plot, PlotDTO>()
                .ForMember(dest => dest.ProductionPlans, opt => opt.MapFrom(src => src.PlotCultivations.SelectMany(p => p.ProductionPlans).Distinct()));
        }

        
    }
}
