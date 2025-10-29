using FluentValidation;
using MediatR;
using RiceProduction.Application.Common.Models;
using System;
using System.ComponentModel.DataAnnotations;
namespace RiceProduction.Application.ProductionPlanFeature.Commands.ApproveRejectPlan;

public class ApproveRejectPlanCommand : IRequest<Result<Guid>>
{
    [Required]
    public Guid PlanId { get; set; }

    /// <summary>
    /// true để phê duyệt, false để từ chối.
    /// </summary>
    [Required]
    public bool Approved { get; set; }

    /// <summary>
    /// Ghi chú phê duyệt/từ chối. Bắt buộc nếu từ chối.
    /// </summary>
    [MaxLength(1000)]
    public string? Notes { get; set; }
    public Guid? ExpertId { get; set; }
}

public class ApproveRejectPlanCommandValidator : AbstractValidator<ApproveRejectPlanCommand>
{
    public ApproveRejectPlanCommandValidator()
    {
        RuleFor(x => x.PlanId).NotEmpty().WithMessage("Plan ID is required.");

        RuleFor(x => x.Notes)
            .NotEmpty().WithMessage("Notes are required when rejecting the plan.")
            .When(x => !x.Approved); // Bắt buộc có ghi chú khi từ chối (Approved = false)
    }
}