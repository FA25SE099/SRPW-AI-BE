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
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
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

                // Convert to dictionary list with JsonPropertyName as keys
                var excelData = ConvertToExcelData(inputList);

                var properties = typeof(T).GetProperties();
                var columns = properties.Select(prop =>
                {
                    var maxLength = inputList
                        .Select(item => prop.GetValue(item)?.ToString()?.Length ?? 0)
                        .DefaultIfEmpty(prop.Name.Length)
                        .Max();

                    // Width calculation: character count * 1.2 + 2 (padding)
                    var width = Math.Min(maxLength * 1.2 + 2, 50); // Cap at 50

                    return new DynamicExcelColumn(prop.Name) { Width = width };
                }).ToArray();

                var config = new OpenXmlConfiguration
                {
                    DynamicColumns = columns
                };
                var sanitizedSheetName = date.Replace(":", "-").Replace("/", "-");
                memoryStream.SaveAs(excelData, sheetName: sanitizedSheetName, configuration: config);
                memoryStream.Seek(0, SeekOrigin.Begin);

                return new FileStreamResult(memoryStream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
                {
                    FileDownloadName = fileName
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating Excel file: {ex.Message}");
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
                stream.Position = 0;

                // Read as dynamic first
                var dynamicRows = stream.Query(useHeaderRow: true).ToList();
                
                // Convert back to strongly typed using JsonPropertyName mapping
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

        private List<Dictionary<string, object>> ConvertToExcelData<T>(List<T> inputList) where T : class
        {
            var properties = typeof(T).GetProperties();
            var result = new List<Dictionary<string, object>>();

            foreach (var item in inputList)
            {
                var row = new Dictionary<string, object>();
                
                foreach (var prop in properties)
                {
                    // Get JsonPropertyName attribute or use property name
                    var jsonAttr = prop.GetCustomAttribute<JsonPropertyNameAttribute>();
                    var columnName = jsonAttr?.Name ?? prop.Name;
                    
                    var value = prop.GetValue(item);
                    row[columnName] = value ?? string.Empty;
                }
                
                result.Add(row);
            }

            return result;
        }

        private List<T> ConvertFromExcelData<T>(IEnumerable<dynamic> dynamicRows) where T : class, new()
        {
            var properties = typeof(T).GetProperties();
            var result = new List<T>();

            // Create mapping from JsonPropertyName to PropertyInfo
            var propertyMapping = new Dictionary<string, PropertyInfo>();
            foreach (var prop in properties)
            {
                var jsonAttr = prop.GetCustomAttribute<JsonPropertyNameAttribute>();
                var columnName = jsonAttr?.Name ?? prop.Name;
                propertyMapping[columnName] = prop;
            }

            foreach (IDictionary<string, object> row in dynamicRows)
            {
                var instance = new T();
                
                foreach (var kvp in row)
                {
                    if (propertyMapping.TryGetValue(kvp.Key, out var prop))
                    {
                        try
                        {
                            var value = kvp.Value;
                            
                            if (value != null && value.ToString() != string.Empty)
                            {
                                // Handle type conversion
                                object convertedValue;
                                
                                if (prop.PropertyType.IsEnum)
                                {
                                    convertedValue = Enum.Parse(prop.PropertyType, value.ToString());
                                }
                                else if (prop.PropertyType == typeof(Guid) || prop.PropertyType == typeof(Guid?))
                                {
                                    convertedValue = Guid.Parse(value.ToString());
                                }
                                else if (Nullable.GetUnderlyingType(prop.PropertyType) != null)
                                {
                                    var underlyingType = Nullable.GetUnderlyingType(prop.PropertyType);
                                    convertedValue = Convert.ChangeType(value, underlyingType);
                                }
                                else
                                {
                                    convertedValue = Convert.ChangeType(value, prop.PropertyType);
                                }
                                
                                prop.SetValue(instance, convertedValue);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"Failed to set property {prop.Name}: {ex.Message}");
                        }
                    }
                }
                
                result.Add(instance);
            }

            return result;
        }
    }
}
