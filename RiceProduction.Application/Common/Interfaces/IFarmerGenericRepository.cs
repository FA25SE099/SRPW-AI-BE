using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.Common.Interfaces
{
    public interface IFarmerGenericRepository
    {
       Task<Farmer?> GetFarmerByIdAsync (Guid id, CancellationToken cancellationToken = default);
       Task<IEnumerable<Farmer?>> GetAllFarmerAsync (CancellationToken cancellationToken = default);
       Task<Farmer?> GetFarmerByPhoneNumber (string phoneNumber, CancellationToken cancellationToken = default);
       Task<Farmer?> GetFarmerByPlotId (Guid plotId, CancellationToken cancellationToken = default);

       IQueryable<Farmer> GetQueryable();
        Task<IEnumerable<Farmer>> FindAsync(
       Expression<Func<Farmer, bool>> predicate,
       CancellationToken cancellationToken = default);

        Task<int> CountAsync(
       Expression<Func<Farmer, bool>>? predicate = null,
       CancellationToken cancellationToken = default);
        
        Task<bool> ExistAsync (Guid id, CancellationToken cancellationToken = default);

        Task<(IEnumerable<Farmer> Items, int TotalCount)> GetPagedAsync(
       int pageNumber,
       int pageSize,
       Expression<Func<Farmer, bool>>? predicate = null,
       CancellationToken cancellationToken = default);
        

    }
}
