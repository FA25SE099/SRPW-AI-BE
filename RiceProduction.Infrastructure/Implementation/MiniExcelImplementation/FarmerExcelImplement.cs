using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            var rowNumber = 1; // Excel usually has header at row 1, data starts at row 2
            Guid? clusterId = null;

            if (clusterManagerId.HasValue)
            {
                var clusterManager = await _context.Set<ClusterManager>()
                    .FirstOrDefaultAsync(cm => cm.Id == clusterManagerId.Value, cancellationToken);
                clusterId = clusterManager?.ClusterId;
            }

            await using var stream = file.OpenReadStream();
            var farmerDtos = stream.Query<FarmerImportDto>(startCell: "A2") // Skip header row
                .ToList();

            result.TotalRows = farmerDtos.Count;

            const string FARMER_ROLE = "Farmer"; // Make sure this role exists in DB
            const string TEMP_PASSWORD = "Farmer@123";

            foreach (var dto in farmerDtos)
            {
                rowNumber++; // Current data row

                // Validate FullName
                if (string.IsNullOrWhiteSpace(dto.FullName))
                {
                    result.FailureCount++;
                    result.Errors.Add(new ImportError
                    {
                        RowNumber = rowNumber,
                        FieldName = "FullName",
                        ErrorMessage = "Tên không được để trống"
                    });
                    continue;
                }

                // Validate PhoneNumber
                if (string.IsNullOrWhiteSpace(dto.PhoneNumber))
                {
                    result.FailureCount++;
                    result.Errors.Add(new ImportError
                    {
                        RowNumber = rowNumber,
                        FieldName = "PhoneNumber",
                        ErrorMessage = "Số điện thoại không được để trống"
                    });
                    continue;
                }

                // Check if phone already exists in Farmers table
                var existingFarmer = await _farmers
                    .AnyAsync(f => f.PhoneNumber == dto.PhoneNumber, cancellationToken);

                if (existingFarmer)
                {
                    result.FailureCount++;
                    result.Errors.Add(new ImportError
                    {
                        RowNumber = rowNumber,
                        FieldName = "PhoneNumber",
                        ErrorMessage = $"Số điện thoại '{dto.PhoneNumber}' đã tồn tại trong hệ thống"
                    });
                    continue;
                }

                // Check if username (PhoneNumber) already used in Identity
                var existingUser = await _userManager.FindByNameAsync(dto.PhoneNumber);
                if (existingUser != null)
                {
                    result.FailureCount++;
                    result.Errors.Add(new ImportError
                    {
                        RowNumber = rowNumber,
                        FieldName = "PhoneNumber",
                        ErrorMessage = $"Số điện thoại '{dto.PhoneNumber}' đã được sử dụng làm tài khoản"
                    });
                    continue;
                }

                // Create new Farmer (which inherits from IdentityUser or ApplicationUser)
                var farmer = new Farmer
                {
                    Id = Guid.NewGuid(),
                    UserName = dto.PhoneNumber,
                    PhoneNumber = dto.PhoneNumber,
                    FullName = dto.FullName,
                    Address = dto.Address,
                    FarmCode = dto.FarmCode,
                    NumberOfPlots = dto.NumberOfPlots ?? 1,
                    ClusterId = clusterId,
                    EmailConfirmed = true,
                    PhoneNumberConfirmed = true, 
                    IsActive = true,
                };

                var createResult = await _userManager.CreateAsync(farmer, TEMP_PASSWORD);

                if (!createResult.Succeeded)
                {
                    result.FailureCount++;
                    result.Errors.Add(new ImportError
                    {
                        RowNumber = rowNumber,
                        FieldName = "Identity Creation",
                        ErrorMessage = $"Lỗi tạo tài khoản: {string.Join(", ", createResult.Errors.Select(e => e.Description))}"
                    });
                    continue;
                }

                // === ADD ROLE "Farmer" ===
                var roleResult = await _userManager.AddToRoleAsync(farmer, FARMER_ROLE);

                if (!roleResult.Succeeded)
                {
                    // Even if role fails, user was created — you might want to log or delete user?
                    result.FailureCount++;
                    result.Errors.Add(new ImportError
                    {
                        RowNumber = rowNumber,
                        FieldName = "Role Assignment",
                        ErrorMessage = $"Tạo tài khoản thành công nhưng gán vai trò 'Farmer' thất bại: {string.Join(", ", roleResult.Errors.Select(e => e.Description))}"
                    });
                    // Optionally: delete the created user if role is mandatory
                    // await _userManager.DeleteAsync(farmer);
                    continue;
                }

                // Success
                result.SuccessCount++;
                result.ImportedFarmers.Add(new ImportedFarmerData
                {
                    PhoneNumber = dto.PhoneNumber,
                    FullName = dto.FullName,
                    Address = dto.Address,
                    FarmCode = dto.FarmCode,
                    NumberOfPlots = dto.NumberOfPlots
                });
            }

            return result;
        }
    }
}