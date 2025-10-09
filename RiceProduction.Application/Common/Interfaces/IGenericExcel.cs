using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace RiceProduction.Application.Common.Interfaces
{
    public interface IGenericExcel
    {
        Task<IActionResult> DownloadGenericExcelFile<T>(List<T> inputList, string fileName = "export.xlsx") where T : class;
        Task<List<T>> ExcelToListT<T>(IFormFile excel) where T : class, new();
    }
}