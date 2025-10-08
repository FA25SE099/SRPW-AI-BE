using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MiniExcelLibs;
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
    public class DownloadGenericExcel : IDownloadGenericExcel
    {
        private readonly ILogger<DownloadGenericExcel> _logger;

        public DownloadGenericExcel(ILogger<DownloadGenericExcel> logger)
        {
            _logger = logger;
        }

        public async Task<IActionResult> DownloadGenericExcelFile<T>(List<T> inputList, string fileName = "export.xlsx") where T : class
        {
            if (inputList == null || !inputList.Any())
            {
                return null;
            }

            try
            {
                var memoryStream = new MemoryStream();
                memoryStream.SaveAs(inputList);
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
    }
}
