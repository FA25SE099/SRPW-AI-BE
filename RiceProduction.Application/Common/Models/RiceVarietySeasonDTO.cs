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
    public class RiceVarietySeasonDTO
    {
        public Guid RiceVarietySeasonId { get; set; }
        public Guid RiceVarietyId { get; set; }
        public Guid SeasonId { get; set; }
        public int GrowthDurationDays { get; set; }
        public decimal? ExpectedYieldPerHectare { get; set; }
        public string OptimalPlantingStart { get; set; } = string.Empty;
        public string? OptimalPlantingEnd { get; set; } = string.Empty;
        public RiskLevel RiskLevel { get; set; } = RiskLevel.Medium;
        public string? SeasonalNotes { get; set; }
        public bool IsRecommended { get; set; } = true;
        public RiceVarietyDTO RiceVariety { get; set; } = null!;
        public SeasonDTO Season { get; set; } = null!;
    }
}
