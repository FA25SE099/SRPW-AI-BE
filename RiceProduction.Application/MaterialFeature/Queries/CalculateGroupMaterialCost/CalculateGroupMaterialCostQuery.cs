using MediatR;
using RiceProduction.Application.Common.Models;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace RiceProduction.Application.MaterialFeature.Queries.CalculateGroupMaterialCost;
public class CalculateGroupMaterialCostQuery : IRequest<Result<CalculateGroupMaterialCostResponse>>
{
    [Required]
    public Guid GroupId { get; set; }
    
    [Required]
    public List<MaterialInputModel> Materials { get; set; } = new List<MaterialInputModel>();
}

public class CalculateGroupMaterialCostQueryValidator : AbstractValidator<CalculateGroupMaterialCostQuery>
{
    public CalculateGroupMaterialCostQueryValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty().WithMessage("Group ID is required.");
        RuleFor(x => x.Materials).NotEmpty().WithMessage("Material list cannot be empty.");
        
        RuleForEach(x => x.Materials).ChildRules(material =>
        {
            material.RuleFor(m => m.MaterialId).NotEmpty().WithMessage("Material ID is required for each item.");
            material.RuleFor(m => m.Quantity).GreaterThan(0).WithMessage("Quantity must be greater than zero for each item.");
        });
    }
}