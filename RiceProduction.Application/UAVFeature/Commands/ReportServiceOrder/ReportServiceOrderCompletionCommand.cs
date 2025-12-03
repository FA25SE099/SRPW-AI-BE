using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.UAVFeature.Commands.ReportServiceOrder;
public class ReportServiceOrderCompletionCommand : IRequest<Result<Guid>>
{
    [Required]
    public Guid OrderId { get; set; }

    [Required]
    public Guid PlotId { get; set; } 

    public Guid? VendorId { get; set; }

    [Required]
    public decimal ActualCost { get; set; } 
    public string? Notes { get; set; }
    public decimal ActualAreaCovered { get; set; }

    public List<IFormFile> ProofFiles { get; set; } = new();
}

public class ReportServiceOrderCompletionCommandValidator : AbstractValidator<ReportServiceOrderCompletionCommand>
{
    public ReportServiceOrderCompletionCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.PlotId).NotEmpty();
        RuleFor(x => x.ActualCost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ActualAreaCovered).GreaterThan(0);
        
        RuleForEach(x => x.ProofFiles).Must(file => file.Length > 0)
            .WithMessage("Proof files content cannot be empty.");
    }
}