using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.Common.Models
{
    public class FarmerDetailDTO : FarmerDTO
    {
        public ICollection<GroupDetailDTO> Groups { get; set; }
        public ICollection<PlotDetailDTO> Plots { get; set; } = new List<PlotDetailDTO>();
       
    }
    public class GroupDetailDTO : GroupDTO
    {
        public ICollection<PlotDTO> Plots { get; set; } 
    }
    public class PlotDetailDTO : PlotDTO
    {
        public ICollection<ProductionPlanDTO> ProductionPlans { get; set; }
    }
    public class ProductionPlanDetailDTO : ProductionPlanDTO
    {
        public ICollection<ProductionStageDTO> ProductionStages { get; set; }
    }
    public class ProductionStageDetailDTO : ProductionStageDTO
    {
        public ICollection<ProductionPlanTaskDTO> ProductionPlanTasks { get; set; } = new List<ProductionPlanTaskDTO>();
    }


}
