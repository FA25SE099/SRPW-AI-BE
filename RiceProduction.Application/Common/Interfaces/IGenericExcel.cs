using Microsoft.AspNetCore.Http;
using RiceProduction.Application.Common.Models.Response;

namespace RiceProduction.Application.Common.Interfaces;

public interface IGenericExcel
{
    Task<FileExportResult?> DownloadGenericExcelFile<T>(List<T> inputList, string date, string fileName = "export.xlsx") where T : class;
    Task<List<T>> ExcelToListT<T>(IFormFile excel) where T : class, new();

    /// <summary>
    /// Generates a multi-sheet Excel file from a dictionary of sheet names and data.
    /// </summary>
    Task<FileExportResult?> DownloadMultiSheetExcelFile(Dictionary<string, object> sheets, string fileName);
}
