using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MiniExcelLibs;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Infrastructure.Data;
using RiceProduction.Infrastructure.Identity;

namespace RiceProduction.Infrastructure.Implementation.MiniExcelImplementation
{
    public class FarmerExcelImplement : IFarmerExcel
    {
        private const string FarmerRole = "Farmer";
        private const string TempPassword = "123456";

        private readonly ApplicationDbContext _context;
        private readonly DbSet<Farmer> _farmers;
        private readonly UserManager<ApplicationUser> _userManager;

        public FarmerExcelImplement(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _farmers = context.Set<Farmer>();
            _userManager = userManager;
        }

        public async Task<ImportFarmerResult> ImportFarmerFromExcelAsync(IFormFile file, Guid? clusterManagerId = null, CancellationToken cancellationToken = default)
        {
            var result = new ImportFarmerResult();
            Guid? clusterId = null;

            if (clusterManagerId.HasValue)
            {
                var clusterManager = await _context.Set<ClusterManager>()
                    .FirstOrDefaultAsync(cm => cm.Id == clusterManagerId.Value, cancellationToken);
                clusterId = clusterManager?.ClusterId;
            }

            await using var stream = file.OpenReadStream();
            var farmerDtos = ParseFarmerRows(stream.Query(useHeaderRow: true));
            result.TotalRows = farmerDtos.Count;

            // Excel header is row 1; data starts at row 2
            var rowNumber = 1;
            foreach (var dto in farmerDtos)
            {
                rowNumber++;

                if (string.IsNullOrWhiteSpace(dto.FullName))
                {
                    AddImportError(result, rowNumber, "FullName", "Tên không được để trống");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(dto.PhoneNumber))
                {
                    AddImportError(result, rowNumber, "PhoneNumber", "Số điện thoại không được để trống");
                    continue;
                }

                if (await _farmers.AnyAsync(f => f.PhoneNumber == dto.PhoneNumber, cancellationToken))
                {
                    AddImportError(result, rowNumber, "PhoneNumber", $"Số điện thoại '{dto.PhoneNumber}' đã tồn tại trong hệ thống");
                    continue;
                }

                if (await _userManager.FindByNameAsync(dto.PhoneNumber) != null)
                {
                    AddImportError(result, rowNumber, "PhoneNumber", $"Số điện thoại '{dto.PhoneNumber}' đã được sử dụng làm tài khoản");
                    continue;
                }

                var farmer = new Farmer
                {
                    Id = Guid.NewGuid(),
                    UserName = dto.PhoneNumber,
                    PhoneNumber = dto.PhoneNumber,
                    Email = dto.Email,
                    FullName = dto.FullName,
                    Address = dto.Address,
                    FarmCode = dto.FarmCode,
                    NumberOfPlots = dto.NumberOfPlots ?? 1,
                    ClusterId = clusterId,
                    EmailConfirmed = true,
                    PhoneNumberConfirmed = true,
                    IsActive = true
                };

                var createResult = await _userManager.CreateAsync(farmer, TempPassword);
                if (!createResult.Succeeded)
                {
                    AddImportError(
                        result,
                        rowNumber,
                        "Identity Creation",
                        $"Lỗi tạo tài khoản: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
                    continue;
                }

                var roleResult = await _userManager.AddToRoleAsync(farmer, FarmerRole);
                if (!roleResult.Succeeded)
                {
                    AddImportError(
                        result,
                        rowNumber,
                        "Role Assignment",
                        $"Tạo tài khoản thành công nhưng gán vai trò 'Farmer' thất bại: {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
                    continue;
                }

                result.SuccessCount++;
                result.ImportedFarmers.Add(new ImportedFarmerData
                {
                    PhoneNumber = dto.PhoneNumber,
                    FullName = dto.FullName,
                    Address = dto.Address,
                    FarmCode = dto.FarmCode,
                    NumberOfPlots = dto.NumberOfPlots,
                    Email = dto.Email,
                    TempPassword = TempPassword
                });
            }

            return result;
        }

        private static List<FarmerImportDto> ParseFarmerRows(IEnumerable<dynamic> rows)
        {
            return rows
                .OfType<IDictionary<string, object>>()
                .Select(dict => new FarmerImportDto
                {
                    FullName = GetOptionalString(dict, "FullName") ?? string.Empty,
                    PhoneNumber = GetOptionalString(dict, "PhoneNumber") ?? string.Empty,
                    Address = GetOptionalString(dict, "Address"),
                    FarmCode = GetOptionalString(dict, "FarmCode"),
                    NumberOfPlots = GetOptionalInt(dict, "NumberOfPlots"),
                    Email = GetOptionalString(dict, "Email")
                })
                .ToList();
        }

        private static string? GetOptionalString(IDictionary<string, object> dict, string key) =>
            dict.TryGetValue(key, out var value) ? value?.ToString() : null;

        private static int? GetOptionalInt(IDictionary<string, object> dict, string key) =>
            dict.TryGetValue(key, out var value) && value != null ? Convert.ToInt32(value) : null;

        private static void AddImportError(ImportFarmerResult result, int rowNumber, string fieldName, string errorMessage)
        {
            result.FailureCount++;
            result.Errors.Add(new ImportError
            {
                RowNumber = rowNumber,
                FieldName = fieldName,
                ErrorMessage = errorMessage
            });
        }
    }
}