using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MiniExcelLibs;
using MiniExcelLibs.Attributes;
using MiniExcelLibs.OpenXml;
using RiceProduction.Application.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RiceProduction.Infrastructure.Implementation.MiniExcelImplementation
{
    public class GenericExcel : IGenericExcel
    {
        private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
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
                var excelData = ConvertToExcelData(inputList);
                var columns = BuildDynamicColumns(inputList);
                var config = new OpenXmlConfiguration { DynamicColumns = columns };
                var sanitizedSheetName = date.Replace(":", "-").Replace("/", "-");

                memoryStream.SaveAs(excelData, sheetName: sanitizedSheetName, configuration: config);

                return CreateExcelFileResult(memoryStream, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating Excel file: {ex.Message}");
                return null;
            }
        }

        public async Task<IActionResult> DownloadMultiSheetExcelFile(Dictionary<string, object> sheets, string fileName)
        {
            if (sheets == null || !sheets.Any())
            {
                _logger.LogWarning("Sheet dictionary is null or empty");
                return null;
            }

            try
            {
                var memoryStream = new MemoryStream();
                MiniExcel.SaveAs(memoryStream, sheets);

                return CreateExcelFileResult(memoryStream, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating multi-sheet Excel file: {ex.Message}");
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
                using var stream = new MemoryStream();
                await excel.CopyToAsync(stream);
                stream.Position = 0;

                var dynamicRows = stream.Query(useHeaderRow: true).ToList();
                var result = ConvertFromExcelData<T>(dynamicRows);

                _logger.LogInformation($"Successfully parsed {result.Count} rows from Excel file: {excel.FileName}");

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error parsing Excel file: {excel.FileName}. Error: {ex.Message}");
                return new List<T>();
            }
        }

        private static FileStreamResult CreateExcelFileResult(MemoryStream memoryStream, string fileName)
        {
            memoryStream.Seek(0, SeekOrigin.Begin);
            return new FileStreamResult(memoryStream, ExcelContentType)
            {
                FileDownloadName = fileName
            };
        }

        private static DynamicExcelColumn[] BuildDynamicColumns<T>(List<T> inputList) where T : class
        {
            return typeof(T).GetProperties().Select(prop =>
            {
                var maxLength = inputList
                    .Select(item => prop.GetValue(item)?.ToString()?.Length ?? 0)
                    .DefaultIfEmpty(prop.Name.Length)
                    .Max();

                // Width calculation: character count * 1.2 + 2 (padding), capped at 50
                var width = Math.Min(maxLength * 1.2 + 2, 50);
                return new DynamicExcelColumn(prop.Name) { Width = width };
            }).ToArray();
        }

        private static string GetColumnName(PropertyInfo prop) =>
            prop.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? prop.Name;

        private List<Dictionary<string, object>> ConvertToExcelData<T>(List<T> inputList) where T : class
        {
            var propertyColumns = typeof(T).GetProperties()
                .Select(prop => (Property: prop, ColumnName: GetColumnName(prop)))
                .ToArray();

            var result = new List<Dictionary<string, object>>(inputList.Count);
            foreach (var item in inputList)
            {
                var row = new Dictionary<string, object>(propertyColumns.Length);
                foreach (var (prop, columnName) in propertyColumns)
                {
                    row[columnName] = prop.GetValue(item) ?? string.Empty;
                }

                result.Add(row);
            }

            return result;
        }

        private List<T> ConvertFromExcelData<T>(IEnumerable<dynamic> dynamicRows) where T : class, new()
        {
            var propertyMapping = new Dictionary<string, PropertyInfo>();
            foreach (var prop in typeof(T).GetProperties())
            {
                propertyMapping[GetColumnName(prop)] = prop;
            }

            var result = new List<T>();
            foreach (IDictionary<string, object> row in dynamicRows)
            {
                var instance = new T();
                foreach (var kvp in row)
                {
                    if (!propertyMapping.TryGetValue(kvp.Key, out var prop))
                    {
                        continue;
                    }

                    var value = kvp.Value;
                    if (value == null || value.ToString() == string.Empty)
                    {
                        continue;
                    }

                    try
                    {
                        prop.SetValue(instance, ConvertCellValue(value, prop.PropertyType));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to set property {prop.Name}: {ex.Message}");
                    }
                }

                result.Add(instance);
            }

            return result;
        }

        private static object ConvertCellValue(object value, Type targetType)
        {
            if (targetType.IsEnum)
            {
                return Enum.Parse(targetType, value.ToString());
            }

            if (targetType == typeof(Guid) || targetType == typeof(Guid?))
            {
                return Guid.Parse(value.ToString());
            }

            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            return Convert.ChangeType(value, underlyingType);
        }
    }
}
