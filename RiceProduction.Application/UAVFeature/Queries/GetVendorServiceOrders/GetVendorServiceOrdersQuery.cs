using MediatR;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Enums;
using FluentValidation;
using System;
using System.Collections.Generic;
using RiceProduction.Application.UAVFeature.Queries.GeUAVOrderDetail;

namespace RiceProduction.Application.UAVFeature.Queries.GetVendorServiceOrders;

public class GetVendorServiceOrdersQuery : IRequest<PagedResult<List<UavServiceOrderResponse>>>
{
    public Guid VendorId { get; set; }
    public int CurrentPage { get; set; } = 1; // Thêm phân trang
    public int PageSize { get; set; } = 20; // Thêm phân trang
    public List<RiceProduction.Domain.Enums.TaskStatus>? StatusFilter { get; set; } 
}

public class GetVendorServiceOrdersQueryValidator : AbstractValidator<GetVendorServiceOrdersQuery>
{
    public GetVendorServiceOrdersQueryValidator()
    {
        RuleFor(x => x.VendorId).NotEmpty();
        RuleFor(x => x.CurrentPage).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).GreaterThanOrEqualTo(1);
    }
}