using Microsoft.EntityFrameworkCore.Query;
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
        
        public void Update(Supervisor entity)
        {
            _context.Set<Supervisor>().Update(entity);
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
                .Include(s => s.SupervisedGroups).ThenInclude(s => s.GroupPlots).ThenInclude(gp => gp.Plot)
                .Include(s => s.SupervisedGroups).ThenInclude(s => s.ProductionPlans)
                .Include(s => s.AssignedTasks)
                .Include(s => s.SupervisorAssignments)
                .OrderBy(s => s.FullName)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Supervisor?>> GetAllSupervisorAsync(CancellationToken cancellationToken = default)
        {
            return await _supervisor
                .Include(s => s.SupervisedGroups).ThenInclude(s => s.GroupPlots).ThenInclude(gp => gp.Plot)
                .Include(s => s.SupervisedGroups).ThenInclude(s => s.ProductionPlans)
                .Include(s => s.AssignedTasks)
                .Include(s => s.SupervisorAssignments)
                .OrderBy(s => s.FullName)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Supervisor?>> GetAllSupervisorByNameOrEmailAndPhoneNumberPagingAsync(int pageNumber, int pageSize, string? search, string? phoneNumber, CancellationToken cancellationToken = default)
        {
            var query = _supervisor
                .Include(s => s.SupervisedGroups).ThenInclude(s => s.GroupPlots).ThenInclude(gp => gp.Plot)
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
        public async Task<(IEnumerable<Supervisor?>, int totalCount)> GetAllSupervisorByNameOrEmailAndPhoneNumberAndByGroupOrClusterOrFarmerOrPlotOrNamePagingAsync(int pageNumber, int pageSize, string? search, string? groupSearch, string? phoneNumber, CancellationToken cancellationToken = default)
        {
            var query = _supervisor
                .Include(s => s.SupervisedGroups).ThenInclude(s => s.GroupPlots).ThenInclude(gp => gp.Plot)
                .Include(s => s.SupervisedGroups).ThenInclude(s => s.ProductionPlans)
                .Include(s => s.AssignedTasks)
                .Include(s => s.SupervisorAssignments)
                .Include(s => s.ManagedCluster)
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
                query = query.Where(f => 
                    (f.SupervisedGroups != null
                        && (f.SupervisedGroups.Any(sv => sv.Id.ToString().ToLower().Contains(groupSearch))
                        || f.SupervisedGroups.Any(sv => sv.Cluster.ClusterName.ToLower().Contains(groupSearch))
                        || f.SupervisedGroups.Any(sv => sv.Cluster.Id.ToString().ToLower().Contains(groupSearch)))) 
                    || 
                    (f.AssignedTasks != null
                        && (f.AssignedTasks.Any(sv => sv.PlotCultivation != null && sv.PlotCultivation.Plot.Id.ToString().ToLower().Contains(groupSearch))
                        || f.AssignedTasks.Any(sv => sv.PlotCultivation.Plot.GroupPlots.Any(gp => gp.Group != null && gp.Group.Id.ToString().ToLower().Contains(groupSearch)))))
                    || 
                    (f.SupervisorAssignments != null
                        && (f.SupervisorAssignments.Any(sv => sv.Farmer.Id.ToString().ToLower().Contains(groupSearch))
                        || f.SupervisorAssignments.Any(sv => sv.Farmer.FullName != null && sv.Farmer.FullName.Contains(groupSearch))
                        || f.SupervisorAssignments.Any(sv => sv.Farmer.Email != null && sv.Farmer.Email.Contains(groupSearch))
                        || f.SupervisorAssignments.Any(sv => sv.Farmer.PhoneNumber != null && sv.Farmer.PhoneNumber.Contains(groupSearch))))
                    );
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

        public async Task<Supervisor?> GetSupervisorByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _supervisor
                .Include(s => s.SupervisedGroups).ThenInclude(s => s.GroupPlots).ThenInclude(gp => gp.Plot)
                .Include(s => s.SupervisedGroups).ThenInclude(s => s.ProductionPlans)
                .Include(s => s.AssignedTasks)
                .Include(s => s.SupervisorAssignments)
                .OrderBy(s => s.FullName)
                .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        }


        public async Task<Supervisor?> GetSupervisorByPhoneNumber(string phoneNumber, CancellationToken cancellationToken = default)
        {
            return await _supervisor
                .Include(s => s.SupervisedGroups).ThenInclude(s => s.GroupPlots).ThenInclude(gp => gp.Plot)
                .Include(s => s.SupervisedGroups).ThenInclude(s => s.ProductionPlans)
                .Include(s => s.AssignedTasks)
                .Include(s => s.SupervisorAssignments)
                .OrderBy(s => s.FullName)
                .FirstOrDefaultAsync(f => f.PhoneNumber != null && f.PhoneNumber == phoneNumber, cancellationToken);
        }

        public async Task<Supervisor?> GetSupervisorByNameOrEmail(string search, CancellationToken cancellationToken = default)
        {
            return await _supervisor
                .Include(s => s.SupervisedGroups).ThenInclude(s => s.GroupPlots).ThenInclude(gp => gp.Plot)
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
                .Include(s => s.SupervisedGroups).ThenInclude(s => s.GroupPlots).ThenInclude(gp => gp.Plot)
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
                .Include(s => s.SupervisedGroups).ThenInclude(s => s.GroupPlots).ThenInclude(gp => gp.Plot)
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
        public async Task<IReadOnlyList<Supervisor>> ListAsync(
    Expression<Func<Supervisor, bool>>? filter = null,
    Func<IQueryable<Supervisor>, IOrderedQueryable<Supervisor>>? orderBy = null,
    Func<IQueryable<Supervisor>, IIncludableQueryable<Supervisor, object>>? includeProperties = null
)
        {
            IQueryable<Supervisor> query = _context.Set<Supervisor>();

            if (filter != null)
            {
                query = query.Where(filter);
            }

            if (includeProperties != null)
            {
                query = includeProperties(query);
            }

            if (orderBy != null)
            {
                query = orderBy(query);
            }
            return await query.ToListAsync();
        }

    }
}
