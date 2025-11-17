using RiceProduction.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.Common.Models.Request.AdminRequests
{
    public class GetAllUsersRequest
    {
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? SearchEmailAndName { get; set; }
        public string? SearchPhoneNumber { get; set; }
        public string? Role { get; set; }
        public bool? IsActive { get; set; }
        public SortBy SortBy { get; set; }
    }
}
