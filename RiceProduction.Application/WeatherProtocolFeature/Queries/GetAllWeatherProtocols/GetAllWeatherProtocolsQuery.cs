using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.WeatherProtocolResponses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.WeatherProtocolFeature.Queries.GetAllWeatherProtocols;

public class GetAllWeatherProtocolsQuery : IRequest<PagedResult<List<WeatherProtocolResponse>>>
{
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? SearchName { get; set; }
    public bool? IsActive { get; set; }
}
