using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Query;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.Common.Interfaces
{
    public interface IFarmerRepository
    {
       Task<Farmer?> GetFarmerByIdAsync (Guid id, CancellationToken cancellationToken = default);
       Task<Farmer?> GetFarmerDetailByIdAsync (Guid id, CancellationToken cancellationToken = default);
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

        Task<IReadOnlyList<Farmer>> ListAsync(
   Expression<Func<Farmer, bool>>? filter = null,
   Func<IQueryable<Farmer>, IOrderedQueryable<Farmer>>? orderBy = null,
   Func<IQueryable<Farmer>, IIncludableQueryable<Farmer, object>>? includeProperties = null
);
    }
}
