using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.Common.Models
{
    public class PlotCultivationDTO
    {
        public Guid PlotId { get; set; }
        public Guid SeasonId { get; set; }
        public Guid RiceVarietyId { get; set; }
        public DateTime PlantingDate { get; set; }
        public decimal? ActualYield { get; set; }
        public CultivationStatus Status { get; set; } = CultivationStatus.Planned;
        public Guid? CurrentProductionStageId { get; set; }
        public DateTime? CurrentStageStartDate { get; set; }
    }
}
