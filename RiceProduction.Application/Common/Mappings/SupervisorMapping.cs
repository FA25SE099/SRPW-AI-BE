using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.Common.Mappings
{
    public class SupervisorMapping : Profile
    {
        public SupervisorMapping()
        {
            CreateMap<Supervisor, SupervisorDTO>()
                .ForMember(dest => dest.SupervisorId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.SupervisorName, opt => opt.MapFrom(src => src.FullName))  
                .ForMember(dest => dest.ClusterId, opt => opt.MapFrom(src => src.ClusterId))
                .ForMember(dest => dest.MaxFarmerCapacity, opt => opt.MapFrom(src => src.MaxFarmerCapacity))
                .ForMember(dest => dest.CurrentFarmerCount, opt => opt.MapFrom(src => src.CurrentFarmerCount));
            CreateMap<Group, GroupDTO>()
                .ForMember(dest => dest.GroupId, opt => opt.MapFrom(src => src.Id));

            CreateMap<CultivationTask, CultivationTaskDTO>()
                .ForMember(dest => dest.CultivationTaskId, opt => opt.MapFrom(src => src.Id));

            CreateMap<SupervisorFarmerAssignment, SupervisorFarmerAssignmentDTO>()
                .ForMember(dest => dest.SupervisorFarmerAssignmentId, opt => opt.MapFrom(src => src.Id));
        }
    }
}
