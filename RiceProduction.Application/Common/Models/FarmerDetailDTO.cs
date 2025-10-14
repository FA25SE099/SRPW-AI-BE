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
       public ICollection<PlotDTO> Plots { get; set; } = new List<PlotDTO>();
       public ICollection<ProductionPlanDTO> ProductionPlans { get; set; } = new List<ProductionPlanDTO>();
       public ICollection<ProductionPlanTaskDTO> ProductionPlansTask { get; set; } = new List<ProductionPlanTaskDTO>();
    }
}
