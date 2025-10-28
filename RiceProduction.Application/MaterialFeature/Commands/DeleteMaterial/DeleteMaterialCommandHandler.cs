using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.MaterialFeature.Commands.DeleteMaterial
{
    public class DeleteMaterialCommandHandler : IRequestHandler<DeleteMaterialCommand, Result<Guid>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<DeleteMaterialCommandHandler> _logger;

        public DeleteMaterialCommandHandler(IUnitOfWork unitOfWork, ILogger<DeleteMaterialCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<Guid>> Handle(DeleteMaterialCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var materialRepo = _unitOfWork.Repository<Material>();

                var material = await materialRepo.FindAsync(m => m.Id == request.MaterialId);
                if (material == null)
                {
                    return Result<Guid>.Failure($"Material with ID {request.MaterialId} not found");
                }

                material.IsActive = false;
                materialRepo.Update(material);
                await _unitOfWork.CompleteAsync();

                _logger.LogInformation("Soft deleted material with ID: {MaterialId}", request.MaterialId);
                return Result<Guid>.Success(request.MaterialId, "Material deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting material with ID: {MaterialId}", request.MaterialId);
                return Result<Guid>.Failure("Failed to delete material");
            }
        }
    }
}

