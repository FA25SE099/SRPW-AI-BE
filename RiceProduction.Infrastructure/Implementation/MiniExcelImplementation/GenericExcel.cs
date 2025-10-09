using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MiniExcelLibs;
using MiniExcelLibs.Attributes;
using MiniExcelLibs.OpenXml;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models.Response;
using RiceProduction.Infrastructure.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Infrastructure.Implementation.MiniExcelImplementation
{
    public class GenericExcel : IGenericExcel
    {
        private readonly ILogger<GenericExcel> _logger;

        public GenericExcel(ILogger<GenericExcel> logger)
        {
            _logger = logger;
        }

        public async Task<IActionResult> DownloadGenericExcelFile<T>(List<T> inputList, string date, string fileName = "export.xlsx") where T : class
        {
            if (inputList == null || !inputList.Any())
            {
                return null;
            }

            try
            {
                var memoryStream = new MemoryStream();
                var sheetName = Path.GetFileNameWithoutExtension(fileName);
                memoryStream.SaveAs(inputList, sheetName: date.ToString());
                memoryStream.Seek(0, SeekOrigin.Begin);

                return new FileStreamResult(memoryStream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
                {
                    FileDownloadName = fileName
                };
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"Error generating Excel file: {ex.Message}");
                return null; 
            }
        }
        public async Task<List<T>> ExcelToListT<T>(IFormFile excel) where T : class, new()
        {
            if (excel == null || excel.Length == 0)
            {
                _logger.LogWarning("Uploaded file is null or empty");
                return new List<T>();
            }

            try
            {
                var stream = new MemoryStream();
                await excel.CopyToAsync(stream);
                stream.Position = 0; // Reset stream position to beginning

                var rows = stream.Query<T>().ToList();

                _logger.LogInformation($"Successfully parsed {rows.Count} rows from Excel file: {excel.FileName}");

                return rows;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error parsing Excel file: {excel.FileName}. Error: {ex.Message}");
                return null;
            }
        }
    }
}
