using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.FarmerFeature.Queries.DownloadFarmerImportTemplate;

public class DownloadFarmerImportTemplateQueryHandler 
    : IRequestHandler<DownloadFarmerImportTemplateQuery, Result<IActionResult>>
{
    private readonly IGenericExcel _genericExcel;
    private readonly ILogger<DownloadFarmerImportTemplateQueryHandler> _logger;

    public DownloadFarmerImportTemplateQueryHandler(
        IGenericExcel genericExcel,
        ILogger<DownloadFarmerImportTemplateQueryHandler> logger)
    {
        _genericExcel = genericExcel;
        _logger = logger;
    }

    public async Task<Result<IActionResult>> Handle(
        DownloadFarmerImportTemplateQuery request, 
        CancellationToken cancellationToken)
    {
        try
        {
            // Create sample data to demonstrate the format
            var sampleData = new List<FarmerImportDto>
            {
                new FarmerImportDto
                {
                    FullName = "Nguyen Van A",
                    PhoneNumber = "0901234567",
                    Address = "123 Main Street, District 1, Ho Chi Minh City",
                    FarmCode = "FARM001",
                    NumberOfPlots = 2,
                },
                new FarmerImportDto
                {
                    FullName = "Tran Van B",
                    PhoneNumber = "0909876543",
                    Address = "456 Second Street, District 2, Ho Chi Minh City",
                    FarmCode = "FARM002",
                    NumberOfPlots = 4,
                },
                new FarmerImportDto
                {
                    FullName = "Le Thi C",
                    PhoneNumber = "0912345678",
                    Address = "789 Third Avenue, District 3, Ho Chi Minh City",
                    FarmCode = "FARM003",
                    NumberOfPlots = 3,
                },
                new FarmerImportDto
                {
                    FullName = "Pham Van D",
                    PhoneNumber = "0923456789",
                    Address = "321 Fourth Road, District 4, Ho Chi Minh City",
                    FarmCode = "FARM004",
                    NumberOfPlots = 1,
                },
                new FarmerImportDto
                {
                    FullName = "Pham Van E",
                    PhoneNumber = "0923456777",
                    Address = "321 Fourth Road, District 4, Ho Chi Minh City",
                    FarmCode = "FARM005",
                    NumberOfPlots = 1,
                }
            };

            var fileName = $"Farmer_Import_Template_{DateTime.Now:yyyyMMdd}.xlsx";
            var result = await _genericExcel.DownloadGenericExcelFile(
                sampleData,
                DateTime.Now.ToString("yyyy-MM-dd"),
                fileName);

            _logger.LogInformation("Generated farmer import template with {SampleCount} sample rows", sampleData.Count);

            return Result<IActionResult>.Success(
                result, 
                "Farmer import template generated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating farmer import template");
            return Result<IActionResult>.Failure(
                $"Failed to generate template: {ex.Message}");
        }
    }
}

