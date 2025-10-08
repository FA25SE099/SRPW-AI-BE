using Microsoft.AspNetCore.Mvc;

namespace RiceProduction.Application.Common.Interfaces
{
    public interface IDownloadGenericExcel
    {
        Task<IActionResult> DownloadGenericExcelFile<T>(List<T> inputList, string fileName = "export.xlsx") where T : class;
    }
}