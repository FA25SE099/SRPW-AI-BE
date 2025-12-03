using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.MaterialResponses;
using RiceProduction.Application.MaterialFeature.Queries.CalculateMaterialsCostByArea;
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
    /// List of materials with quantity per hectare
    /// </summary>
    [Required]
    public List<MaterialQuantityInput> Materials { get; set; } = new List<MaterialQuantityInput>();
}

public class CalculateMaterialsCostByPlotIdQueryValidator : AbstractValidator<CalculateMaterialsCostByPlotIdQuery>
{
    public CalculateMaterialsCostByPlotIdQueryValidator()
    {
        RuleFor(x => x.PlotId)
            .NotEmpty()
            .WithMessage("Plot ID is required.");


        RuleForEach(x => x.Materials).ChildRules(material =>
        {
            material.RuleFor(m => m.MaterialId)
                .NotEmpty()
                .WithMessage("Material ID is required for each item.");
        });
    }
}
