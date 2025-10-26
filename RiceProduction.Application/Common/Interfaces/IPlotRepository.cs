using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.Common.Interfaces
{
    public interface IPlotRepository
    {
        Task<(IEnumerable<Plot> Items, int TotalCount)> GetAllPlotPagedAsync (
            int pageNumber,
            int pageSize,
            Expression<Func<Plot, bool>>? predicate = null,
            CancellationToken cancellationToken = default); 
        Task<Plot?> GetPlotByIDAsync (Guid id, CancellationToken cancellationToken = default);
        Task <IEnumerable<Plot?>> GetPlotsByFarmerIdAsync (Guid farmerId, CancellationToken cancellationToken = default); 
        Task<IEnumerable<Plot?>> GetPlotsByGroupIdAsync (Guid groupId, CancellationToken cancellationToken = default);
        Task <bool> ExistPlotAsync (Guid id, CancellationToken cancellationToken = default);
        Task<int> CountPlotAsync(Expression<Func<Plot, bool>>? predicate = null, CancellationToken cancellationToken = default);
        IQueryable<Plot> PlotQueryable();
    }
}
