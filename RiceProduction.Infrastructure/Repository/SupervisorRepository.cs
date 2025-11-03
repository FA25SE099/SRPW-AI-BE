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
    public class SupervisorRepository : ISupervisorRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly DbSet<Supervisor> _supervisor;

        public SupervisorRepository(ApplicationDbContext context)
        {
            _context = context;
            _supervisor = context.Set<Supervisor>();
        }
        public void UpdateRange(IEnumerable<Supervisor> entities)
        {
            _context.Set<Supervisor>().UpdateRange(entities);
        }

        public async Task<bool> ExistAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _supervisor.AnyAsync(f => f.Id == id, cancellationToken);
        }

        public async Task<IEnumerable<Supervisor>> FindAsync(Expression<Func<Supervisor, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _supervisor
                .Include(s => s.SupervisedGroups).ThenInclude(s => s.Plots)
                .Include(s => s.SupervisedGroups).ThenInclude(s => s.ProductionPlans)
                .Include(s => s.AssignedTasks)
                .Include(s => s.SupervisorAssignments)
                .OrderBy(s => s.FullName)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Supervisor?>> GetAllSupervisorAsync(CancellationToken cancellationToken = default)
        {
            return await _supervisor
                .Include(s => s.SupervisedGroups).ThenInclude(s => s.Plots)
                .Include(s => s.SupervisedGroups).ThenInclude(s => s.ProductionPlans)
                .Include(s => s.AssignedTasks)
                .Include(s => s.SupervisorAssignments)
                .OrderBy(s => s.FullName)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Supervisor?>> GetAllSupervisorByNameOrEmailAndPhoneNumberPagingAsync(int pageNumber, int pageSize, string? search, string? phoneNumber, CancellationToken cancellationToken = default)
        {
            var query = _supervisor
                .Include(s => s.SupervisedGroups).ThenInclude(s => s.Plots)
                .Include(s => s.SupervisedGroups).ThenInclude(s => s.ProductionPlans)
                .Include(s => s.AssignedTasks)
                .Include(s => s.SupervisorAssignments)
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

        public async Task<Supervisor?> GetSupervisorByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _supervisor
                .Include(s => s.SupervisedGroups).ThenInclude(s => s.Plots)
                .Include(s => s.SupervisedGroups).ThenInclude(s => s.ProductionPlans)
                .Include(s => s.AssignedTasks)
                .Include(s => s.SupervisorAssignments)
                .OrderBy(s => s.FullName)
                .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        }


        public async Task<Supervisor?> GetSupervisorByPhoneNumber(string phoneNumber, CancellationToken cancellationToken = default)
        {
            return await _supervisor
                .Include(s => s.SupervisedGroups).ThenInclude(s => s.Plots)
                .Include(s => s.SupervisedGroups).ThenInclude(s => s.ProductionPlans)
                .Include(s => s.AssignedTasks)
                .Include(s => s.SupervisorAssignments)
                .OrderBy(s => s.FullName)
                .FirstOrDefaultAsync(f => f.PhoneNumber != null && f.PhoneNumber == phoneNumber, cancellationToken);
        }

        public async Task<Supervisor?> GetSupervisorByNameOrEmail(string search, CancellationToken cancellationToken = default)
        {
            return await _supervisor
                .Include(s => s.SupervisedGroups).ThenInclude(s => s.Plots)
                .Include(s => s.SupervisedGroups).ThenInclude(s => s.ProductionPlans)
                .Include(s => s.AssignedTasks)
                .Include(s => s.SupervisorAssignments)
                .OrderBy(s => s.FullName)
                .FirstOrDefaultAsync(f => (f.FullName != null && f.FullName.Contains(search)) ||
                                          (f.Email != null && f.Email.Contains(search)), cancellationToken);
        }

        public async Task<Supervisor?> GetSupervisorByPlotId(Guid plotId, CancellationToken cancellationToken = default)
        {
            return await _supervisor
                .Include(s => s.SupervisedGroups).ThenInclude(s => s.Plots)
                .Include(s => s.SupervisedGroups).ThenInclude(s => s.ProductionPlans)
                .Include(s => s.AssignedTasks)
                .Include(s => s.SupervisorAssignments)
                .OrderBy(s => s.FullName)
                .FirstOrDefaultAsync(f => f.SupervisedGroups != null && f.SupervisedGroups
                .Any(p => p.Id == plotId), cancellationToken);
        }

        public async Task<(IEnumerable<Supervisor> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, Expression<Func<Supervisor, bool>>? predicate = null, CancellationToken cancellationToken = default)
        {
            var query = _supervisor
                .Include(s => s.SupervisedGroups).ThenInclude(s => s.Plots)
                .Include(s => s.SupervisedGroups).ThenInclude(s => s.ProductionPlans)
                .Include(s => s.AssignedTasks)
                .Include(s => s.SupervisorAssignments)
                .AsQueryable();
            if (predicate != null)
            {
                query = query.Where(predicate);
            }

            var totalCount = await query.CountAsync(cancellationToken);
            var items = await query.OrderBy(f => f.FullName).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
            return (items, totalCount);
        }

        public IQueryable<Supervisor> GetQueryable()
        {
            return _supervisor.AsQueryable();
        }
    }
}
