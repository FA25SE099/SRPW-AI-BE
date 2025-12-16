using MediatR;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.MaterialCostCalculationRequests;
using RiceProduction.Application.Common.Models.Response.MaterialResponses;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace RiceProduction.Application.MaterialFeature.Queries.CalculateCostByStandardPlanAndArea;

/// <summary>
/// Calculate material costs using a StandardPlan for a specific area
/// </summary>
public class CalculateCostByStandardPlanAndAreaQuery : IRequest<Result<CalculateMaterialsCostByAreaResponse>>
{
    /// <summary>
    /// StandardPlan ID to get tasks and materials from
    /// </summary>
    [Required]
    public Guid StandardPlanId { get; set; }

    /// <summary>
    /// Area in hectares
    /// </summary>
    [Required]
    public decimal Area { get; set; }
}

public class CalculateCostByStandardPlanAndAreaQueryValidator : AbstractValidator<CalculateCostByStandardPlanAndAreaQuery>
{
    public CalculateCostByStandardPlanAndAreaQueryValidator()
    {
        RuleFor(x => x.StandardPlanId)
            .NotEmpty()
            .WithMessage("StandardPlan ID is required.");

        RuleFor(x => x.Area)
            .GreaterThan(0)
            .WithMessage("Area must be greater than zero.");
    }
}