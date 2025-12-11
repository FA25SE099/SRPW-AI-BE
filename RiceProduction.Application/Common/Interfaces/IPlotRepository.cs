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
        
        /// <summary>
        /// Gets all plots assigned to a specific group
        /// </summary>
        Task<IEnumerable<Plot>> GetPlotsForGroupAsync(Guid groupId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Checks if a plot is assigned to a group. If groupId is null, checks if plot is assigned to any group.
        /// </summary>
        Task<bool> IsPlotAssignedToGroupAsync(Guid plotId, Guid? groupId = null, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Checks if a plot is assigned to a group for a specific season. 
        /// This enforces the business rule: one plot can belong to multiple groups, but only one group per season.
        /// </summary>
        Task<bool> IsPlotAssignedToGroupForSeasonAsync(Guid plotId, Guid seasonId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Checks if a plot is assigned to a group for a specific year-season combination.
        /// This enforces the business rule: one plot can belong to multiple groups, but only one group per season.
        /// </summary>
        Task<bool> IsPlotAssignedToGroupForYearSeasonAsync(Guid plotId, Guid yearSeasonId, CancellationToken cancellationToken = default);
    }
}
