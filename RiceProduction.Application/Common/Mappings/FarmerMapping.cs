using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.Common.Mappings
{

    public class FarmerMapping : Profile
    {
        public FarmerMapping()
        {
            CreateMap<Farmer, FarmerDTO>()
                .ForMember(dest => dest.FarmerId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.FullName))
                .ForMember(dest => dest.PhoneNumber, opt => opt.MapFrom(src => src.PhoneNumber))
                .ForMember(dest => dest.FarmCode, opt => opt.MapFrom(src => src.FarmCode))
                .ForMember(dest => dest.LastActivityAt, opt => opt.MapFrom(src => src.LastActivityAt))
                .ForMember(dest => dest.Address, opt => opt.MapFrom(src => src.Address))
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.IsActive))
                .ForMember(dest => dest.IsVerified, opt => opt.MapFrom(src => src.IsVerified))
                .ForMember(dest => dest.PlotCount, opt => opt.MapFrom(src => src.OwnedPlots != null ? src.OwnedPlots.Count : 0));

            CreateMap<Farmer, FarmerDetailDTO>().IncludeBase<Farmer, FarmerDTO>()
            .ForMember(dest => dest.Plots, opt => opt.MapFrom(src => src.OwnedPlots))
            .ForMember(dest => dest.Groups, opt => opt.MapFrom(src =>
            src.OwnedPlots
            .SelectMany(p => p.GroupPlots)
            .Select(gp => gp.Group)
            .Where(g => g != null)
            .Distinct()
            ));
        }
    }
}
