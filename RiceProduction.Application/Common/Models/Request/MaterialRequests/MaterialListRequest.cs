using RiceProduction.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.Common.Models.Request.MaterialRequests
{
    public class MaterialListRequest
    {
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; }
        public MaterialType Type { get; set; }

        /// <summary>
        /// Optional: Date and time to retrieve prices at. If null, uses current DateTime.
        /// Format: yyyy-MM-ddTHH:mm:ss
        /// </summary>
        public DateTime? PriceDateTime { get; set; }
    }
}
