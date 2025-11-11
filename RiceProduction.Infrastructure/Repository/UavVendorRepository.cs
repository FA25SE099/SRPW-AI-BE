using RiceProduction.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Infrastructure.Repository
{
    public class UavVendorRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly DbSet<UavVendor> _uavVendor;

        public UavVendorRepository(ApplicationDbContext context)
        {
            _context = context;
            _uavVendor = context.Set<UavVendor>();
        }
        public void UpdateRange(IEnumerable<UavVendor> entities)
        {
            _context.Set<UavVendor>().UpdateRange(entities);
        }

        public async Task<bool> ExistAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _uavVendor.AnyAsync(f => f.Id == id, cancellationToken);
        }

        public async Task<IEnumerable<UavVendor>> FindAsync(Expression<Func<UavVendor, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _uavVendor
                .Include(s => s.AssignedTasks)
                .Include(s => s.UavInvoices)
                .Include(s => s.UavServiceOrders)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<UavVendor?>> GetAllUavVendorAsync(CancellationToken cancellationToken = default)
        {
            return await _uavVendor
                .Include(s => s.AssignedTasks)
                .Include(s => s.UavInvoices)
                .Include(s => s.UavServiceOrders)
                .ToListAsync(cancellationToken);
        }

        public async Task<(IEnumerable<UavVendor?>, int totalCount)> GetAllUavVendorByNameOrEmailAndPhoneNumberPagingAsync(int pageNumber, int pageSize, string? search, string? phoneNumber, CancellationToken cancellationToken = default)
        {
            var query = _uavVendor
                .Include(s => s.AssignedTasks)
                .Include(s => s.UavInvoices)
                .Include(s => s.UavServiceOrders)
                .AsQueryable();
            var totalCount = await query.CountAsync(cancellationToken);
            search = search?.ToLower();
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(f => (f.FullName != null && f.FullName.ToLower().Contains(search)) ||
                                         (f.VendorName != null && f.VendorName.ToLower().Contains(search)) ||
                                         (f.Email != null && f.Email.ToLower().Contains(search)));
            }
            if (!string.IsNullOrEmpty(phoneNumber))
            {
                query = query.Where(f => f.PhoneNumber != null && f.PhoneNumber.Contains(phoneNumber));
            }
            if (pageSize == 0)
            {
                return (await query
                .OrderBy(s => s.FullName)
                .ToListAsync(cancellationToken), totalCount);
            }
            return (await query
                .OrderBy(s => s.FullName)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken), totalCount);
        }

        public async Task<(IEnumerable<UavVendor?>, int totalCount)> GetAllUavVendorByNameOrEmailAndPhoneAndByGroupIdOrGroupNameNumberPagingAsync(int pageNumber, int pageSize, string? search, string? groupSearch, string? phoneNumber, CancellationToken cancellationToken = default)
        {
            var query = _uavVendor
                .Include(s => s.AssignedTasks)
                .Include(s => s.UavInvoices)
                .Include(s => s.UavServiceOrders)
                .AsQueryable();
            var totalCount = await query.CountAsync(cancellationToken);
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
            if (!string.IsNullOrEmpty(groupSearch))
            {
                query = query.Where(f => f.Culti != null && f.PhoneNumber.Contains(phoneNumber));
            }
            if (pageSize == 0)
            {
                return (await query
                .OrderBy(s => s.FullName)
                .ToListAsync(cancellationToken), totalCount);
            }
            return (await query
                .OrderBy(s => s.FullName)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken), totalCount);
        }

        public async Task<UavVendor?> GetUavVendorByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _uavVendor
                .Include(s => s.AssignedTasks)
                .Include(s => s.UavInvoices)
                .Include(s => s.UavServiceOrders)
                .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        }


        public async Task<UavVendor?> GetUavVendorByPhoneNumber(string phoneNumber, CancellationToken cancellationToken = default)
        {
            return await _uavVendor
                .Include(s => s.AssignedTasks)
                .Include(s => s.UavInvoices)
                .Include(s => s.UavServiceOrders)
                .FirstOrDefaultAsync(f => f.PhoneNumber != null && f.PhoneNumber.Contains(phoneNumber), cancellationToken);
        }

        public async Task<UavVendor?> GetUavVendorByNameOrEmail(string search, CancellationToken cancellationToken = default)
        {
            search = search.ToLower();
            return await _uavVendor
                .Include(s => s.AssignedTasks)
                .Include(s => s.UavInvoices)
                .Include(s => s.UavServiceOrders)
                .FirstOrDefaultAsync(f => (f.FullName != null && f.FullName.ToLower().Contains(search)) ||
                                          (f.VendorName != null && f.VendorName.ToLower().Contains(search))||
                                          (f.Email != null && f.Email.ToLower().Contains(search)), cancellationToken);
        }

        public async Task<UavVendor?> GetUavVendorByClusterId(Guid ClusterId, CancellationToken cancellationToken = default)
        {
            return await _uavVendor
                .Include(s => s.AssignedTasks)
                .Include(s => s.UavInvoices)
                .Include(s => s.UavServiceOrders)
                .FirstOrDefaultAsync(s => s.ClusterId == ClusterId, cancellationToken);
        }

        public async Task<(IEnumerable<UavVendor> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, Expression<Func<UavVendor, bool>>? predicate = null, CancellationToken cancellationToken = default)
        {
            var query = _uavVendor
                .Include(s => s.AssignedTasks)
                .Include(s => s.UavInvoices)
                .Include(s => s.UavServiceOrders)
                .AsQueryable();
            if (predicate != null)
            {
                query = query.Where(predicate);
            }

            var totalCount = await query.CountAsync(cancellationToken);
            var items = await query.OrderBy(f => f.FullName).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
            return (items, totalCount);
        }

        public IQueryable<UavVendor> GetQueryable()
        {
            return _uavVendor.AsQueryable();
        }
    }
}
