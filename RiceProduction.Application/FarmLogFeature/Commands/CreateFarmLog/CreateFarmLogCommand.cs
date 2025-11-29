using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.FarmLogFeature.Commands.CreateFarmLog;
namespace RiceProduction.Application.FarmLogFeature.Commands.CreateFarmLog;

public class CreateFarmLogCommand : IRequest<Result<Guid>>
{
    [Required]
    public Guid CultivationTaskId { get; set; }

    [Required]
    public Guid PlotCultivationId { get; set; }

    public string? WorkDescription { get; set; }

    public decimal? ActualAreaCovered { get; set; }

    public decimal? ServiceCost { get; set; }
    public string? ServiceNotes { get; set; }

    public string? WeatherConditions { get; set; }
    public string? InterruptionReason { get; set; }

    // Danh sách ảnh minh chứng
    public List<IFormFile> ProofImages { get; set; } = new();

    // Danh sách vật tư sử dụng
    public List<FarmLogMaterialRequest> Materials { get; set; } = new();
    
    // ID nông dân (sẽ được gán từ Controller)
    public Guid? FarmerId { get; set; }
}

public class CreateFarmLogCommandValidator : AbstractValidator<CreateFarmLogCommand>
{
    public CreateFarmLogCommandValidator()
    {
        RuleFor(x => x.CultivationTaskId).NotEmpty();
        RuleFor(x => x.PlotCultivationId).NotEmpty();
        
        // Validate ảnh (Optional: giới hạn size, type)
        RuleForEach(x => x.ProofImages).Must(file => file.Length > 0)
            .WithMessage("File content cannot be empty.");
            
        RuleForEach(x => x.Materials).SetValidator(new FarmLogMaterialRequestValidator());
    }
}

public class FarmLogMaterialRequestValidator : AbstractValidator<FarmLogMaterialRequest>
{
    public FarmLogMaterialRequestValidator()
    {
        RuleFor(x => x.MaterialId).NotEmpty();
        RuleFor(x => x.ActualQuantityUsed).GreaterThan(0);
    }
}