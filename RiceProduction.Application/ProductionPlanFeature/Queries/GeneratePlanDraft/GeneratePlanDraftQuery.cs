using System.ComponentModel.DataAnnotations;
using FluentValidation;
using MediatR;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response;
using RiceProduction.Application.Common.Models.Request;
namespace RiceProduction.Application.ProductionPlanFeature.Queries.GeneratePlanDraft;

public class GeneratePlanDraftQuery : IRequest<Result<GeneratePlanDraftResponse>>
{
    /// <summary>
    /// The ID of the Standard Plan template to base the draft on.
    /// </summary>
    [Required]
    public Guid StandardPlanId { get; set; }
    
    /// <summary>
    /// The ID of the Group which provides the actual TotalArea for calculation.
    /// </summary>
    [Required]
    public Guid GroupId { get; set; }
    
    /// <summary>
    /// The intended planting date, used as the base date for scheduling tasks and calculating material prices.
    /// </summary>
    [Required]
    public DateTime BasePlantingDate { get; set; }
}

public class GeneratePlanDraftQueryValidator : AbstractValidator<GeneratePlanDraftQuery>
{
    public GeneratePlanDraftQueryValidator()
    {
        RuleFor(x => x.StandardPlanId)
            .NotEmpty().WithMessage("Standard Plan ID is required.");
            
        RuleFor(x => x.GroupId)
            .NotEmpty().WithMessage("Group ID is required to determine the area.");
            
        RuleFor(x => x.BasePlantingDate)
            .NotEmpty().WithMessage("Base Planting Date is required for scheduling.");
    }
}