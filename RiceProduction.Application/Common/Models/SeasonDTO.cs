using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.Common.Models
{
    public class SeasonDTO
    {
        public Guid SeasonId { get; set; }
        public string SeasonName { get; set; } = string.Empty;
        public string StartDate { get; set; } = string.Empty;
        public string EndDate { get; set; } = string.Empty;
        public string? SeasonType { get; set; }
        public bool IsActive { get; set; }
    }
}
