using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.Common.Models.Request.WeatherProtocolRequests;

public class WeatherProtocolListRequest
{
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? SearchName { get; set; }
    public bool? IsActive { get; set; }
}
