using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.MaterialFeature.Queries.DownloadAllMaterialExcel;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.FarmerFeature.Queries
{
    public class DownloadAllFarmerExcelQueryHandler : IRequestHandler<DownloadAllFarmerExcelQuery, Result<IActionResult>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IDownloadGenericExcel _downloadGenericExcel;
        private readonly IMapper _mapper;

        public DownloadAllFarmerExcelQueryHandler (IUnitOfWork unitOfWork, IDownloadGenericExcel downloadGenericExcel, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _downloadGenericExcel = downloadGenericExcel;
            _mapper = mapper;
        }

        public async Task<Result<IActionResult>> Handle(DownloadAllFarmerExcelQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var farmers = await _unitOfWork.FarmerRepository.GetAllFarmerAsync(cancellationToken);

                if (farmers == null || !farmers.Any())
                {
                    return Result<IActionResult>.Failure("No farmers found to export");
                }
                var farmerDTOs = _mapper.Map<List<FarmerDTO>>(farmers);
                var result = await _downloadGenericExcel.DownloadGenericExcelFile(farmerDTOs, "Farmer date:" + request.InputDate);
                return Result<IActionResult>.Success(result, "Farmer download successfully");
            }
            catch (Exception ex)
            {
                return Result<IActionResult>.Failure("File created fail");
            }
        }
    }
}
