using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.MaterialResponses;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.MaterialFeature.Queries.CalculateMaterialsCostByArea;

public class CalculateMaterialsCostByAreaQuery : IRequest<Result<CalculateMaterialsCostByAreaResponse>>
{
    /// <summary>
    /// Area in hectares
    /// </summary>
    [Required]
    public decimal Area { get; set; }

    /// <summary>
    /// List of materials with quantity per hectare
    /// </summary>
    [Required]
    public List<MaterialQuantityInput> Materials { get; set; } = new List<MaterialQuantityInput>();
}

public class MaterialQuantityInput
{
    [Required]
    public Guid MaterialId { get; set; }

    /// <summary>
    /// Quantity required per hectare
    /// </summary>
    [Required]
    public decimal QuantityPerHa { get; set; }
}

public class CalculateMaterialsCostByAreaQueryValidator : AbstractValidator<CalculateMaterialsCostByAreaQuery>
{
    public CalculateMaterialsCostByAreaQueryValidator()
    {
        RuleFor(x => x.Area)
            .GreaterThan(0)
            .WithMessage("Area must be greater than zero.");

        RuleForEach(x => x.Materials).ChildRules(material =>
        {
            material.RuleFor(m => m.MaterialId)
                .NotEmpty()
                .WithMessage("Material ID is required for each item.");
        });
    }
}
