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
            var rowNumber = 1;

            // Get ClusterId from ClusterManager if provided
            Guid? clusterId = null;
            if (clusterManagerId.HasValue)
            {
                var clusterManager = await _context.Set<ClusterManager>()
                    .FirstOrDefaultAsync(cm => cm.Id == clusterManagerId.Value, cancellationToken);
                clusterId = clusterManager?.ClusterId;
            }

            await using (var stream = file.OpenReadStream())
            {
                var farmerDtos = stream.Query<FarmerImportDto>().ToList();
                result.TotalRows = farmerDtos.Count;

                foreach (var dto in farmerDtos)
                {
                    rowNumber++;

                    // Validate FullName
                    if (string.IsNullOrWhiteSpace(dto.FullName))
                    {
                        result.FailureCount++;
                        result.Errors.Add(new ImportError
                        {
                            RowNumber = rowNumber,
                            FieldName = "FullName",
                            ErrorMessage = "Name cannot be empty"
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
                            ErrorMessage = "Phone cannot be empty"
                        });
                        continue;
                    }

                    
                    var existingFarmer = await _farmers
                        .FirstOrDefaultAsync(f => f.PhoneNumber == dto.PhoneNumber, cancellationToken);

                    if (existingFarmer != null)
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

                    //  Kiểm tra Username (PhoneNumber) đã tồn tại chưa (trong Identity)
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

                    // ✅ Parse Id nếu có (optional)
                    Guid? farmerId = null;
                    if (dto.Id.HasValue && dto.Id.Value != Guid.Empty)
                    {
                        farmerId = dto.Id.Value;

                        // Kiểm tra Id có bị trùng không
                        var existingById = await _farmers
                            .FirstOrDefaultAsync(f => f.Id == farmerId, cancellationToken);
                        if (existingById != null)
                        {
                            result.FailureCount++;
                            result.Errors.Add(new ImportError
                            {
                                RowNumber = rowNumber,
                                FieldName = "Id",
                                ErrorMessage = $"Id '{farmerId}' đã tồn tại trong hệ thống"
                            });
                            continue;
                        }
                    }

                    const string TEMP_PASSWORD = "Farmer@123"; 

                    var farmer = new Farmer
                    {
                        Id = farmerId ?? Guid.NewGuid(), 
                        PhoneNumber = dto.PhoneNumber,
                        UserName = dto.PhoneNumber,
                        FullName = dto.FullName,
                        Address = dto.Address,
                        FarmCode = dto.FarmCode,
                        NumberOfPlots = dto.NumberOfPlots ?? 1,
                        ClusterId = clusterId,
                        EmailConfirmed = true,
                        IsActive = true,
                    };

                    var createResult = await _userManager.CreateAsync(farmer, TEMP_PASSWORD);

                    if (createResult.Succeeded)
                    {
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
                    else
                    {
                        result.FailureCount++;
                        result.Errors.Add(new ImportError
                        {
                            RowNumber = rowNumber,
                            FieldName = "Identity Creation",
                            ErrorMessage = $"Lỗi tạo tài khoản: {string.Join(", ", createResult.Errors.Select(e => e.Description))}"
                        });
                    }
                }
            }

            return result;
        }
    }
}