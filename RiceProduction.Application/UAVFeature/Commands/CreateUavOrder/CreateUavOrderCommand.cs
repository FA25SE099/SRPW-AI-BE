using MediatR;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Enums;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RiceProduction.Application.UAVFeature.Commands.CreateUavOrder;

public class CreateUavOrderCommand : IRequest<Result<Guid>>
{
    [Required]
    public Guid GroupId { get; set; }
    
    [Required]
    public Guid UavVendorId { get; set; }

    [Required]
    public DateTime ScheduledDate { get; set; }

    [Required]
    public List<Guid> SelectedPlotIds { get; set; } = new();

    [MaxLength(255)]
    public string? OrderNameOverride { get; set; }
    
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;

    public Guid? ClusterManagerId { get; set; }
}

public class CreateUavOrderCommandValidator : AbstractValidator<CreateUavOrderCommand>
{
    public CreateUavOrderCommandValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty().WithMessage("Group ID is required.");
        RuleFor(x => x.UavVendorId).NotEmpty().WithMessage("UAV Vendor ID is required.");
        RuleFor(x => x.ScheduledDate).NotEmpty().WithMessage("Scheduled Date is required.")
            .GreaterThan(DateTime.Today).WithMessage("Scheduled Date must be in the future.");
        RuleFor(x => x.SelectedPlotIds).NotEmpty().WithMessage("At least one Plot ID must be selected for the order.");
    }
}