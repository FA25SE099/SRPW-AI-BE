using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.FarmerFeature.Queries.GetFarmer.GetById;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.FarmerFeature.Command.CreateFarmer
{
    public class CreateFarmersCommandHandler : IRequestHandler<CreateFarmersCommand, Result<FarmerDTO>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<CreateFarmersCommand> _logger;
        private readonly IFarmerRepository _farmerRepository;
        private readonly IMapper _mapper;
        private readonly UserManager<ApplicationUser> _userManager;
        public CreateFarmersCommandHandler(IUnitOfWork unitOfWork, ILogger<CreateFarmersCommand> logger, IFarmerRepository farmerRepository, IMapper mapper, UserManager<ApplicationUser> userManager)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _farmerRepository = farmerRepository;
            _mapper = mapper;
            _userManager = userManager;
        }

        public async Task<Result<FarmerDTO>> Handle(CreateFarmersCommand request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Creating farmer: {FullName}", request.FullName);
                var phoneNumberExist = await _unitOfWork.FarmerRepository.GetFarmerByPhoneNumber(request.PhoneNumber);
                if (phoneNumberExist != null)
                {
                    _logger.LogError("Phone number {PhoneNumber} already exist", request.PhoneNumber);
                    return Result<FarmerDTO>.Failure($"Số điện thoại {request.PhoneNumber} đã tồn tại.");
                }
                var newFarmer = new Farmer
               {
                   Id = Guid.NewGuid(),
                   FullName = request.FullName,
                   UserName = request.PhoneNumber,
                   PhoneNumber = request.PhoneNumber,
                   Address = request.Address,
                   FarmCode = request.FarmCode,
                   IsActive =  false,
               };
                var result = await _userManager.CreateAsync(newFarmer);
                if (!result.Succeeded)
                {
                    var error = result.Errors.Select(e => e.Description).ToList();
                    return Result<FarmerDTO>.Failure(error);
                }
                var farmerDTOs = _mapper.Map<FarmerDTO>(newFarmer);
                return Result<FarmerDTO>.Success(farmerDTOs, "Tạo nông dân thành công.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                   "Error occurred while creating farmer with phone: {PhoneNumber}",
                   request.PhoneNumber);
                return Result<FarmerDTO>.Failure(
                    $"Error creating farmer: {ex.Message}");
            }
        }
    }
}
