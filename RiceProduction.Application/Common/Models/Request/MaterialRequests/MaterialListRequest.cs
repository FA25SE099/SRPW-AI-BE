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
    }
}
