using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.Common.Models.Request.GroupRequests
{
    public class GroupListRequest
    {
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
    }
}
