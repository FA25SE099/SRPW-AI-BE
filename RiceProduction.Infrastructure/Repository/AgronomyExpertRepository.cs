using RiceProduction.Domain.Entities;
using RiceProduction.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace RiceProduction.Infrastructure.Repository
{
    public class AgronomyExpertRepository : IAgronomyExpertRepository
    {
        private readonly DbSet<AgronomyExpert> _agronomyExpert;

        public AgronomyExpertRepository(ApplicationDbContext context)
        {
            _agronomyExpert = context.Set<AgronomyExpert>();
        }

        public void Update(AgronomyExpert entity)
        {
            _agronomyExpert.Update(entity);
        }

        public void UpdateRange(IEnumerable<AgronomyExpert> entities)
        {
            _agronomyExpert.UpdateRange(entities);
        }

        public async Task<bool> ExistAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _agronomyExpert.AnyAsync(f => f.Id == id, cancellationToken);
        }

        public async Task<IEnumerable<AgronomyExpert>> FindAsync(Expression<Func<AgronomyExpert, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await QueryWithDetails().ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<AgronomyExpert?>> GetAllAgronomyExpertAsync(CancellationToken cancellationToken = default)
        {
            return await QueryWithDetails().ToListAsync(cancellationToken);
        }

        public async Task<(IEnumerable<AgronomyExpert?>, int totalCount)> GetAllAgronomyExpertByNameOrEmailAndPhoneNumberPagingAsync(int pageNumber, int pageSize, string? search, string? phoneNumber, CancellationToken cancellationToken = default)
        {
            var query = QueryWithDetails();
            var totalCount = await query.CountAsync(cancellationToken);
            query = ApplySearchFilters(query, search, phoneNumber);

            var items = await ApplyOrderingAndPaging(query, pageNumber, pageSize)
                .ToListAsync(cancellationToken);

            return (items, totalCount);
        }

        public async Task<(IEnumerable<AgronomyExpert?>, int totalCount)> GetAllAgronomyExpertAssignedOrNotByNameOrEmailAndPhoneNumberPagingAsync(int pageNumber, int pageSize, string? search, string? phoneNumber, bool? freeOrAssigned, CancellationToken cancellationToken = default)
        {
            var query = QueryWithDetails();
            var totalCount = await query.CountAsync(cancellationToken);

            if (freeOrAssigned.HasValue)
            {
                query = freeOrAssigned.Value
                    ? query.Where(s => !s.ClusterId.HasValue)
                    : query.Where(s => s.ClusterId.HasValue);
            }

            query = ApplySearchFilters(query, search, phoneNumber);

            var items = await ApplyOrderingAndPaging(query, pageNumber, pageSize)
                .ToListAsync(cancellationToken);

            return (items, totalCount);
        }

        public async Task<AgronomyExpert?> GetAgronomyExpertByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await QueryWithDetails()
                .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        }

        public async Task<AgronomyExpert?> GetAgronomyExpertByPhoneNumber(string phoneNumber, CancellationToken cancellationToken = default)
        {
            return await QueryWithDetails()
                .FirstOrDefaultAsync(f => f.PhoneNumber != null && f.PhoneNumber.Contains(phoneNumber), cancellationToken);
        }

        public async Task<AgronomyExpert?> GetAgronomyExpertByNameOrEmail(string search, CancellationToken cancellationToken = default)
        {
            search = search.ToLower();
            return await QueryWithDetails()
                .FirstOrDefaultAsync(f => (f.FullName != null && f.FullName.ToLower().Contains(search)) ||
                                          (f.Email != null && f.Email.ToLower().Contains(search)), cancellationToken);
        }

        public async Task<AgronomyExpert?> GetAgronomyExpertByClusterId(Guid ClusterId, CancellationToken cancellationToken = default)
        {
            return await QueryWithDetails()
                .FirstOrDefaultAsync(s => s.ClusterId == ClusterId, cancellationToken);
        }

        public async Task<(IEnumerable<AgronomyExpert> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, Expression<Func<AgronomyExpert, bool>>? predicate = null, CancellationToken cancellationToken = default)
        {
            var query = QueryWithDetails();
            if (predicate != null)
            {
                query = query.Where(predicate);
            }

            var totalCount = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderBy(f => f.FullName)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
            return (items, totalCount);
        }

        public IQueryable<AgronomyExpert> GetQueryable()
        {
            return _agronomyExpert.AsQueryable();
        }

        private IQueryable<AgronomyExpert> QueryWithDetails()
        {
            return _agronomyExpert
                .Include(s => s.ManagedCluster)
                .OrderBy(s => s.AssignedDate);
        }

        private static IQueryable<AgronomyExpert> ApplySearchFilters(IQueryable<AgronomyExpert> query, string? search, string? phoneNumber)
        {
            search = search?.ToLower();
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(f => (f.FullName != null && f.FullName.ToLower().Contains(search)) ||
                                         (f.Email != null && f.Email.ToLower().Contains(search)));
            }

            if (!string.IsNullOrEmpty(phoneNumber))
            {
                query = query.Where(f => f.PhoneNumber != null && f.PhoneNumber.Contains(phoneNumber));
            }

            return query;
        }

        private static IQueryable<AgronomyExpert> ApplyOrderingAndPaging(IQueryable<AgronomyExpert> query, int pageNumber, int pageSize)
        {
            var ordered = query.OrderBy(s => s.FullName);
            return pageSize == 0
                ? ordered
                : ordered.Skip((pageNumber - 1) * pageSize).Take(pageSize);
        }
    }
}
