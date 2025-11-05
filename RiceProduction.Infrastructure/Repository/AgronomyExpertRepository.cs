using RiceProduction.Domain.Entities;
using RiceProduction.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Infrastructure.Repository
{

    public class AgronomyExpertRepository : IAgronomyExpertRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly DbSet<AgronomyExpert> _agronomyExpert;

        public AgronomyExpertRepository(ApplicationDbContext context)
        {
            _context = context;
            _agronomyExpert = context.Set<AgronomyExpert>();
        }
        public void UpdateRange(IEnumerable<AgronomyExpert> entities)
        {
            _context.Set<AgronomyExpert>().UpdateRange(entities);
        }

        public async Task<bool> ExistAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _agronomyExpert.AnyAsync(f => f.Id == id, cancellationToken);
        }

        public async Task<IEnumerable<AgronomyExpert>> FindAsync(Expression<Func<AgronomyExpert, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _agronomyExpert
                .Include(s => s.ManagedCluster)
                .OrderBy(s => s.AssignedDate)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<AgronomyExpert?>> GetAllAgronomyExpertAsync(CancellationToken cancellationToken = default)
        {
            return await _agronomyExpert
                .Include(s => s.ManagedCluster)
                .OrderBy(s => s.AssignedDate)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<AgronomyExpert?>> GetAllAgronomyExpertByNameOrEmailAndPhoneNumberPagingAsync(int pageNumber, int pageSize, string? search, string? phoneNumber, CancellationToken cancellationToken = default)
        {
            var query = _agronomyExpert
                .Include(s => s.ManagedCluster)
                .OrderBy(s => s.AssignedDate)
                .AsQueryable();
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(f => (f.FullName != null && f.FullName.Contains(search)) ||
                                         (f.Email != null && f.Email.Contains(search)));
            }
            if (!string.IsNullOrEmpty(phoneNumber))
            {
                query = query.Where(f => f.PhoneNumber != null && f.PhoneNumber == phoneNumber);
            }
            if (pageSize == 0)
            {
                return await query
                .OrderBy(s => s.FullName)
                .ToListAsync(cancellationToken);
            }
            return await query
                .OrderBy(s => s.FullName)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<AgronomyExpert?>> GetAllAgronomyExpertAssignedOrNotByNameOrEmailAndPhoneNumberPagingAsync(int pageNumber, int pageSize, string? search, string? phoneNumber, bool? freeOrAssigned, CancellationToken cancellationToken = default)
        {
            var query = _agronomyExpert
                .Include(s => s.ManagedCluster)
                .OrderBy(s => s.AssignedDate)
                .AsQueryable();
            if (freeOrAssigned.HasValue)
            {
                if (freeOrAssigned.Value)
                    query = query.Where(s => s.AssignedDate == null && !s.ClusterId.HasValue);
                else
                    query = query.Where(s => s.AssignedDate != null && s.ClusterId.HasValue);
            }
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(f => (f.FullName != null && f.FullName.Contains(search)) ||
                                         (f.Email != null && f.Email.Contains(search)));
            }
            if (!string.IsNullOrEmpty(phoneNumber))
            {
                query = query.Where(f => f.PhoneNumber != null && f.PhoneNumber == phoneNumber);
            }
            if (pageSize == 0)
            {
                return await query
                .OrderBy(s => s.FullName)
                .ToListAsync(cancellationToken);
            }
            return await query
                .OrderBy(s => s.FullName)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
        }

        public async Task<AgronomyExpert?> GetAgronomyExpertByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _agronomyExpert
                .Include(s => s.ManagedCluster)
                .OrderBy(s => s.AssignedDate)
                .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        }


        public async Task<AgronomyExpert?> GetAgronomyExpertByPhoneNumber(string phoneNumber, CancellationToken cancellationToken = default)
        {
            return await _agronomyExpert
                .Include(s => s.ManagedCluster)
                .OrderBy(s => s.AssignedDate)
                .FirstOrDefaultAsync(f => f.PhoneNumber != null && f.PhoneNumber == phoneNumber, cancellationToken);
        }

        public async Task<AgronomyExpert?> GetAgronomyExpertByNameOrEmail(string search, CancellationToken cancellationToken = default)
        {
            return await _agronomyExpert
                .Include(s => s.ManagedCluster)
                .OrderBy(s => s.AssignedDate)
                .FirstOrDefaultAsync(f => (f.FullName != null && f.FullName.Contains(search)) ||
                                          (f.Email != null && f.Email.Contains(search)), cancellationToken);
        }

        public async Task<AgronomyExpert?> GetAgronomyExpertByClusterId(Guid ClusterId, CancellationToken cancellationToken = default)
        {
            return await _agronomyExpert
                .Include(s => s.ManagedCluster)
                .OrderBy(s => s.AssignedDate)
                .FirstOrDefaultAsync(s => s.ClusterId == ClusterId, cancellationToken);
        }

        public async Task<(IEnumerable<AgronomyExpert> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, Expression<Func<AgronomyExpert, bool>>? predicate = null, CancellationToken cancellationToken = default)
        {
            var query = _agronomyExpert
                .Include(s => s.ManagedCluster)
                .OrderBy(s => s.AssignedDate)
                .AsQueryable();
            if (predicate != null)
            {
                query = query.Where(predicate);
            }

            var totalCount = await query.CountAsync(cancellationToken);
            var items = await query.OrderBy(f => f.FullName).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
            return (items, totalCount);
        }

        public IQueryable<AgronomyExpert> GetQueryable()
        {
            return _agronomyExpert.AsQueryable();
        }
    }
}
