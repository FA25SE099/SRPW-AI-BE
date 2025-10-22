using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response;
using RiceProduction.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.MaterialFeature.Queries.DownloadAllMaterialExcel
{
    public class DownloadAllMaterialExcelQuery : IRequest<Result<IActionResult>>
    {
        public DateTime InputDate { get; set; }
    }
}

//private static TimeZoneInfo GetVietnamTimeZone()
//{
//    try
//    {
//        // Try Windows timezone ID first
//        return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
//    }
//    catch (TimeZoneNotFoundException)
//    {
//        // Fallback to IANA timezone ID for Linux/Mac
//        return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
//    }
//}
///// <summary>
///// Gets current time in Vietnam timezone, then converts to UTC for database storage
///// </summary>
//private DateTime GetVietnamTimeAsUtc()
//{
//    var vietnamNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, GetVietnamTimeZone());
//    return TimeZoneInfo.ConvertTimeToUtc(vietnamNow, GetVietnamTimeZone());
//}

///// <summary>
///// Gets current Vietnam time (for logging/display purposes)
///// </summary>
//private DateTime GetVietnamTime()
//{
//    return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, GetVietnamTimeZone());
//}