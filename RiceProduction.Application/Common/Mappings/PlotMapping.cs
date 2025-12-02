using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

public class PlotMapping : Profile
{
    public PlotMapping()
    {
        CreateMap<Plot, PlotDTO>()
            .ForMember(dest => dest.PlotId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.FarmerName,
                opt => opt.MapFrom(src => src.Farmer != null ? src.Farmer.FullName : null))
            .ForMember(dest => dest.VarietyName, opt => opt.MapFrom(src =>
                src.PlotCultivations
                    .OrderByDescending(pc => pc.PlantingDate)
                    .Select(pc => pc.RiceVariety != null ? pc.RiceVariety.VarietyName : null)
                    .FirstOrDefault()
                ?? string.Empty
            ))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status))
            .ForMember(dest => dest.BoundaryGeoJson, opt => opt.MapFrom(src => src.Boundary))
            .ForMember(dest => dest.CoordinateGeoJson, opt => opt.MapFrom(src => src.Coordinate))
            .ForMember(dest => dest.Seasons, opt => opt.MapFrom(src =>
                src.PlotCultivations
                    .Where(pc => pc.Season != null)
                    .Select(pc => pc.Season)
                    .Distinct()
            ));

        CreateMap<Plot, PlotDetailDTO>()
            .IncludeBase<Plot, PlotDTO>()       
            .ForMember(dest => dest.PlotCultivations,
                opt => opt.MapFrom(src => src.PlotCultivations))
            .ForMember(dest => dest.ProductionPlans,
                opt => opt.MapFrom(src =>
                    src.Group != null ? src.Group.ProductionPlans : new List<ProductionPlan>()
                ));

        CreateMap<Season, SeasonDTO>()
            .ForMember(dest => dest.SeasonId, opt => opt.MapFrom(src => src.Id));
    }
}
