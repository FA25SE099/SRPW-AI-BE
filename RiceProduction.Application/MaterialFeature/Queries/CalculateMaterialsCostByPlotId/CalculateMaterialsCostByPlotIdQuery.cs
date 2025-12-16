using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.MaterialCostCalculationRequests;
using RiceProduction.Application.Common.Models.Response.MaterialResponses;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.MaterialFeature.Queries.CalculateMaterialsCostByPlotId;

public class CalculateMaterialsCostByPlotIdQuery : IRequest<Result<CalculateMaterialsCostByAreaResponse>>
{
    /// <summary>
    /// Plot ID to retrieve the area from
    /// </summary>
    [Required]
    public Guid PlotId { get; set; }

    /// <summary>
    /// List of tasks with their materials
    /// </summary>
    public List<TaskWithMaterialsInput> Tasks { get; set; } = new();

    /// <summary>
    /// List of seed/service materials at the same level as tasks
    /// </summary>
    public List<SeedServiceInput> SeedServices { get; set; } = new();
}

public class CalculateMaterialsCostByPlotIdQueryValidator : AbstractValidator<CalculateMaterialsCostByPlotIdQuery>
{
    public CalculateMaterialsCostByPlotIdQueryValidator()
    {
        RuleFor(x => x.PlotId)
            .NotEmpty()
            .WithMessage("Plot ID is required.");

        // At least one of Tasks or SeedServices must be provided
        RuleFor(x => x)
            .Must(x => x.Tasks.Any() || x.SeedServices.Any())
            .WithMessage("At least one task or seed service must be provided.");

        // Validate each task
        RuleForEach(x => x.Tasks).ChildRules(task =>
        {
            task.RuleFor(t => t.TaskName).NotEmpty().WithMessage("Task name is required.");
            task.RuleFor(t => t.Materials).NotEmpty().WithMessage("Each task must have at least one material.");

            task.RuleForEach(t => t.Materials).ChildRules(material =>
            {
                material.RuleFor(m => m.MaterialId).NotEmpty().WithMessage("Material ID is required for each item.");
                material.RuleFor(m => m.QuantityPerHa).GreaterThan(0).WithMessage("Quantity per hectare must be greater than zero.");
            });
        });

        // Validate each seed service
        RuleForEach(x => x.SeedServices).ChildRules(seedService =>
        {
            seedService.RuleFor(s => s.MaterialId).NotEmpty().WithMessage("Material ID is required for seed service.");
            seedService.RuleFor(s => s.QuantityPerHa).GreaterThan(0).WithMessage("Quantity per hectare must be greater than zero.");

        });
    }
}