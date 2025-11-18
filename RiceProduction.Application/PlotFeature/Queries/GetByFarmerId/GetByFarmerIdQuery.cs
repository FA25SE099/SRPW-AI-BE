using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.PlotFeature.Queries.GetByFarmerId;
public class GetByFarmerIdQuery: IRequest<PagedResult<List<PlotListResponse>>>, ICacheable
{
    public Guid? FarmerId { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    
    // --- Bộ lọc tùy chọn ---
    /// Lọc theo trạng thái của thửa đất (Active, Inactive...).
    public PlotStatus? Status { get; set; }
    
    /// Lọc các thửa đất chưa được gán vào Group (true) hoặc đã được gán (false).
    public bool? IsUnassigned { get; set; } 

    // CacheKey động dựa trên FarmerId và các bộ lọc
    public string CacheKey => $"Plots:Farmer:{FarmerId}:Page{CurrentPage}:Size{PageSize}:Sts{Status?.ToString() ?? "All"}:Unassigned{IsUnassigned?.ToString() ?? "All"}";
}

public class GetByFarmerIdQueryValidator : AbstractValidator<GetByFarmerIdQuery>
{
    public GetByFarmerIdQueryValidator()
    {
        RuleFor(x => x.FarmerId).NotEmpty().WithMessage("Farmer ID is required.");
        RuleFor(x => x.CurrentPage).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).GreaterThanOrEqualTo(1);
    }
}