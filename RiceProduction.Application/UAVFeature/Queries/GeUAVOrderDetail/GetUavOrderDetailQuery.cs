using MediatR;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using FluentValidation;
using System;

namespace RiceProduction.Application.UAVFeature.Queries.GeUAVOrderDetail;
public class GetUavOrderDetailQuery : IRequest<Result<UavOrderDetailResponse>>
{
    /// <summary>
    /// ID của đơn hàng dịch vụ UAV.
    /// </summary>
    public Guid OrderId { get; set; }

    /// <summary>
    /// ID của Vendor hiện tại (dùng để xác thực quyền sở hữu Order).
    /// </summary>
    public Guid? VendorId { get; set; }
}

public class GetUavOrderDetailQueryValidator : AbstractValidator<GetUavOrderDetailQuery>
{
    public GetUavOrderDetailQueryValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty().WithMessage("Order ID is required.");
        RuleFor(x => x.VendorId).NotEmpty().WithMessage("Vendor ID is required for authorization.");
    }
}