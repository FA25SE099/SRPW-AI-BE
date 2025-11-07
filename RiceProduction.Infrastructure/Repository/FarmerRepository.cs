using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Query;
using MiniExcelLibs;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Infrastructure.Repository
{
    public class FarmerRepository : IFarmerRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly DbSet<Farmer> _farmers;

        public FarmerRepository(ApplicationDbContext context)
        {
            _context = context;
            _farmers = context.Set<Farmer>();
        }

        public async Task<int> CountAsync(Expression<Func<Farmer, bool>>? predicate = null, CancellationToken cancellationToken = default)
        {
            if (predicate != null)
            {
                return await _farmers.CountAsync(predicate, cancellationToken);

            }

            return await _farmers.CountAsync(cancellationToken);
        }

        public async Task<bool> ExistAsync (Guid id, CancellationToken cancellationToken = default)
        {
            return await _farmers.AnyAsync (f => f.Id == id, cancellationToken);
        }

        public async Task<IEnumerable<Farmer>> FindAsync(Expression<Func<Farmer, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _farmers.Include(f => f.OwnedPlots)
                .Where(predicate)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Farmer?>> GetAllFarmerAsync(CancellationToken cancellationToken = default)
        {
            return await _farmers
                .Include(f => f.OwnedPlots)
                .Where(f => f.IsActive)
                .OrderBy(f => f.FullName)
                .ToListAsync(cancellationToken);
        }


        public async Task<Farmer?> GetFarmerByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _farmers
                .Include(f => f.OwnedPlots)
                .Include(f => f.FarmerAssignments)
                .ThenInclude(a => a.Supervisor)
                .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        }

        public async Task<Farmer?> GetFarmerByPhoneNumber(string phoneNumber, CancellationToken cancellationToken = default)
        {
            return await _farmers
                .Include (f => f.OwnedPlots)
                .FirstOrDefaultAsync(f => f.PhoneNumber != null && f.PhoneNumber == phoneNumber, cancellationToken);
        }

        public async Task<Farmer?> GetFarmerByPlotId(Guid plotId, CancellationToken cancellationToken = default)
        {
            return await _farmers
                .Include(f => f.OwnedPlots)
                .FirstOrDefaultAsync(f => f.OwnedPlots != null && f.OwnedPlots
                .Any(p => p.Id == plotId), cancellationToken);
        }

        public async Task<Farmer?> GetFarmerDetailByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _farmers
                .Include(f => f.OwnedPlots)
                    .ThenInclude(p => p.Group)
                        .ThenInclude(g => g.ProductionPlans)
                            .ThenInclude(pp => pp.CurrentProductionStages)
                                .ThenInclude(ps => ps.ProductionPlanTasks)
                .Include(f => f.OwnedPlots).ThenInclude(p => p.Group)
                .Include(f => f.OwnedPlots).ThenInclude(p => p.Group).ThenInclude(r => r.RiceVariety)
                .Include(f => f.OwnedPlots).ThenInclude(p => p.Group).ThenInclude(c => c.Cluster)
                .Include(f => f.OwnedPlots).ThenInclude(p => p.Group).ThenInclude(s => s.Supervisor)
                .Include(f => f.FarmerAssignments).ThenInclude(a => a.Supervisor)
                .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        }

        public async Task<(IEnumerable<Farmer> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, Expression<Func<Farmer, bool>>? predicate = null, CancellationToken cancellationToken = default)
        {
            var query = _farmers.Include(f => f.OwnedPlots).AsQueryable();
            if (predicate != null)
            {
                query = query.Where(predicate);
            }

            var totalCount = await query.CountAsync(cancellationToken);
            var items = await query.OrderBy(f => f.FullName).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
            return (items, totalCount);
        }



        public IQueryable<Farmer> GetQueryable()
        {
            return _farmers.AsQueryable();
        }
        public async Task<IReadOnlyList<Farmer>> ListAsync(
    Expression<Func<Farmer , bool>>? filter = null,
    Func<IQueryable<Farmer>, IOrderedQueryable<Farmer>>? orderBy = null,
    Func<IQueryable<Farmer>, IIncludableQueryable<Farmer, object>>? includeProperties = null
)
        {
            IQueryable<Farmer> query = _context.Set<Farmer>();

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
