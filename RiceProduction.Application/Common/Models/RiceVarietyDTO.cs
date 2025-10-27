using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.Common.Models
{
    public class RiceVarietyDTO
    {
        public Guid RiveVarietyId {  get; set; }
        public string VarietyName { get; set; } = string.Empty;
        public Guid CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public int? BaseGrowthDurationDays { get; set; }
        public decimal? BaseYieldPerHectare { get; set; }
        public string? Description { get; set; }
        public string? Characteristics { get; set; }
        public bool IsActive { get; set; } = true;
        public ICollection<PlotCultivationDTO> PlotCultivations { get; set; } = new List<PlotCultivationDTO>();
        public ICollection<RiceVarietySeasonDTO> RiceVarietySeasons { get; set; } = new List<RiceVarietySeasonDTO>();
        public ICollection<GroupDTO> Groups { get; set; } = new List<GroupDTO>();
    }
}
