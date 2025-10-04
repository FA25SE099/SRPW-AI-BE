using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Query;
using RiceProduction.Domain.Common;

namespace RiceProduction.Application.Common.Interfaces
{
    public interface IGenericRepository<T>
        where T : BaseAuditableEntity
    {
        Task<T?> GetEntityByIdAsync(int id);
        Task<IReadOnlyList<T>> ListAllAsync();
        Task<T?> FindAsync(Expression<Func<T, bool>> match);
        Task AddAsync(T entity);
        void Update(T entity);
        T? Delete(T entityToDelete);
        T? Delete(object id);
        Task<int> CountAsync();
        Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate);

        // Batch operations
        Task AddRangeAsync(IEnumerable<T> entities);
        void UpdateRange(IEnumerable<T> entities);
        void DeleteRange(IEnumerable<T> entities);
        Task<bool> DeleteRangeAsync(Expression<Func<T, bool>> predicate);

        Task<int> SaveChangesAsync();
        //Task<IReadOnlyList<T>> ListAsync(
        //    Expression<Func<T, bool>>? filter = null,
        //    Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null
        //);
        Task<IReadOnlyList<T>> ListAsync(
            Expression<Func<T, bool>>? filter = null,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
            Func<IQueryable<T>, IIncludableQueryable<T, object>>? includeProperties = null
        );
        Task<T?> FindAsync(
            Expression<Func<T, bool>> match,
            Func<IQueryable<T>, IIncludableQueryable<T, object>>? includeProperties = null
        );

    }

}
