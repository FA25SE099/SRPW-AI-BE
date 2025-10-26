using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.Common.Models
{
    public class AgronomyExpertDTO
    {
        public Guid AgronomyExpertId { get; set; }  
        public ICollection<ProductionPlanDTO> ApprovedProductionPlans { get; set; } = new List<ProductionPlanDTO>();
    }
}
