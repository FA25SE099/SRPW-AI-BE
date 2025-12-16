using MediatR;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.MaterialCostCalculationRequests;
using RiceProduction.Application.Common.Models.Response.MaterialResponses;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace RiceProduction.Application.MaterialFeature.Queries.CalculateCostByPlotAndStandardPlan;

/// <summary>
/// Calculate material costs for a plot using either a StandardPlan or manual input
/// </summary>
public class CalculateCostByPlotAndStandardPlanQuery : IRequest<Result<CalculateMaterialsCostByAreaResponse>>
{
    /// <summary>
    /// Plot ID to retrieve the area from
    /// </summary>
    [Required]
    public Guid PlotId { get; set; }

    /// <summary>
    /// Optional StandardPlan ID to get tasks and materials from.
    /// If null, uses Tasks and SeedServices inputs instead.
    /// </summary>
    public Guid? StandardPlanId { get; set; }

    /// <summary>
    /// Manual task inputs (used only if StandardPlanId is null)
    /// </summary>
    public List<TaskWithMaterialsInput>? Tasks { get; set; }

}

public class CalculateCostByPlotAndStandardPlanQueryValidator : AbstractValidator<CalculateCostByPlotAndStandardPlanQuery>
{
    public CalculateCostByPlotAndStandardPlanQueryValidator()
    {
        RuleFor(x => x.PlotId)
            .NotEmpty()
            .WithMessage("Plot ID is required.");

        // Either StandardPlanId OR (Tasks/SeedServices) must be provided
        RuleFor(x => x)
            .Must(x => x.StandardPlanId.HasValue ||
                      (x.Tasks != null && x.Tasks.Any()))
            .WithMessage("Either StandardPlanId or at least one Task/SeedService must be provided.");

        // If using manual input (no StandardPlanId), validate tasks and seed services
        When(x => !x.StandardPlanId.HasValue, () =>
        {
            RuleForEach(x => x.Tasks).ChildRules(task =>
            {
                task.RuleFor(t => t.TaskName).NotEmpty().WithMessage("Task name is required.");
                task.RuleFor(t => t.Materials).NotEmpty().WithMessage("Each task must have at least one material.");

                task.RuleForEach(t => t.Materials).ChildRules(material =>
                {
                    material.RuleFor(m => m.MaterialId).NotEmpty().WithMessage("Material ID is required.");
                    material.RuleFor(m => m.QuantityPerHa).GreaterThan(0).WithMessage("Quantity must be greater than zero.");
                });
            });
        });
    }
}