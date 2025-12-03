using MediatR;
using RiceProduction.Application.Common.Models;
using FluentValidation;
using System;
using System.ComponentModel.DataAnnotations;

namespace RiceProduction.Application.MaterialFeature.Queries.CalculatePrice;

public class CalculateMaterialPriceQuery : IRequest<Result<MaterialPriceResponse>>
{
    [Required]
    public Guid MaterialId { get; set; }

    [Required]
    public decimal Quantity { get; set; }
}

public class CalculateMaterialPriceQueryValidator : AbstractValidator<CalculateMaterialPriceQuery>
{
    public CalculateMaterialPriceQueryValidator()
    {
        RuleFor(x => x.MaterialId).NotEmpty().WithMessage("Material ID is required.");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Quantity must be greater than zero.");
    }
}