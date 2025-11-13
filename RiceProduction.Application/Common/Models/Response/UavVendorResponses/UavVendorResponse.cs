using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.Common.Models.Response.UavVendorResponses
{
    public class UavVendorResponse
    {
        public Guid UavVendorId { get; set; }
        public string? UavVendorFullName { get; set; }
        public string? VendorName { get; set; }
        public string? UavVendorPhoneNumber { get; set; }
        public string? Email { get; set; }
    }
}
