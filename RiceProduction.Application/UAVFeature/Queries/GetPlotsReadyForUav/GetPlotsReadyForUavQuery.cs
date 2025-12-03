using MediatR;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Enums;
using FluentValidation;
using System;
using System.Collections.Generic;

namespace RiceProduction.Application.UAVFeature.Queries.GetPlotsReadyForUav;

public class GetPlotsReadyForUavQuery : IRequest<Result<List<UavPlotReadinessResponse>>>
{
    public Guid GroupId { get; set; }
    public TaskType? RequiredTaskType { get; set; } = TaskType.PestControl; // Mặc định là Phun xịt (PestControl)
}

public class GetPlotsReadyForUavQueryValidator : AbstractValidator<GetPlotsReadyForUavQuery>
{
    public GetPlotsReadyForUavQueryValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty().WithMessage("Group ID is required.");
    }
}