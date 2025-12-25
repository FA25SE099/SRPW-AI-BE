using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.MaterialCostCalculationRequests;
using RiceProduction.Application.Common.Models.Response.MaterialResponses;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace RiceProduction.Application.MaterialFeature.Queries.CalculateMaterialsCostByArea;

public class CalculateMaterialsCostByAreaQuery : IRequest<Result<CalculateMaterialsCostByAreaResponse>>
{
    /// <summary>
    /// Area in hectares
    /// </summary>
    [Required]
    public decimal Area { get; set; }

    /// <summary>
    /// List of tasks with their materials
    /// </summary>
    [Required]
    public List<TaskWithMaterialsInput> Tasks { get; set; } = new();
}

public class CalculateMaterialsCostByAreaQueryValidator : AbstractValidator<CalculateMaterialsCostByAreaQuery>
{
    public CalculateMaterialsCostByAreaQueryValidator()
    {
        RuleFor(x => x.Area)
            .GreaterThan(0)
            .WithMessage("Area must be greater than zero.");

        // At least one task must be provided
        RuleFor(x => x.Tasks)
            .NotEmpty()
            .WithMessage("At least one task must be provided.");

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
    }
}