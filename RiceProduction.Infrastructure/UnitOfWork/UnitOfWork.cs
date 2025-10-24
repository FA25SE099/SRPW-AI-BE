using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Infrastructure.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RiceProduction.Domain.Common;
using RiceProduction.Infrastructure.Repository;

namespace RiceProduction.Infrastructure.UnitOfWork
{
    public class UnitOfWork : IUnitOfWork
    {
        // ===================================
        // === Fields & Prop
        // ===================================

        private readonly ApplicationDbContext _dbContext;

        private readonly ILoggerFactory _loggerFactory;

        private ConcurrentDictionary<string, object> _repos;

        private IFarmerGenericRepository? _farmerRepository;
        private ISupervisorGenericRepository? _supervisorRepository;
        private IClusterManagerGenericRepository? _clusterManagerGenericRepository;
        private IPlotGenericRepository? _plotRepository;

        // ===================================
        // === Constructors
        // ===================================
        public UnitOfWork(ApplicationDbContext context, ILoggerFactory loggerFactory)
        {
            _dbContext = context;
            _loggerFactory = loggerFactory;
        }

        // ===================================
        // === Methods
        // ===================================

        /// <summary>
        ///     Dispose object
        /// </summary>
        public void Dispose()
        {
            _dbContext.Dispose();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Save change async
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<int> CompleteAsync() => await _dbContext.SaveChangesAsync();

        /// <summary>
        ///     For example type = "Customer", then 1 GenericRepository of type Customer is created or acccesed if it's created
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public IGenericRepository<T> Repository<T>() where T : BaseAuditableEntity
        {
            if (_repos == null) _repos = new ConcurrentDictionary<string, object>();

            var typeEntityName = typeof(T).Name;

            // Using reflection to create an instanceof GenericRepository with type T
            // Passing db context for each repository
            var repoInstanceTypeT = _repos.GetOrAdd(typeEntityName,
            valueFactory: _ =>
            {
                var repoType = typeof(GenericRepository<T>);
                var repoLogger = _loggerFactory.CreateLogger<GenericRepository<T>>();

                var repoInstance = Activator.CreateInstance(
                repoType,
                _dbContext,
                repoLogger);

                return repoInstance;
            });

            return (IGenericRepository<T>)repoInstanceTypeT;
        }

        public IFarmerGenericRepository FarmerRepository
        {
            get
            {
                if (_farmerRepository == null)
                {
                    _farmerRepository = new FarmerGenericRepository(_dbContext);
                }
                return _farmerRepository;
            }
        }

        public ISupervisorGenericRepository SupervisorRepository
        {
            get
            {
                if (_supervisorRepository == null)
                {
                    _supervisorRepository = new SupervisorGenericRepository(_dbContext);
                }
                return _supervisorRepository;
            }
        }

        public IClusterManagerGenericRepository ClusterManagerRepository
        {
            get
            {
                if (_clusterManagerGenericRepository == null)
                {
                    _clusterManagerGenericRepository = new ClusterManagerGenericRepository(_dbContext);
                }
                return _clusterManagerGenericRepository;
            }
        }
        
        public IPlotGenericRepository PlotRepository
        {
            get
            {
                if (_plotRepository == null)
                {
                    _plotRepository = new PlotGenericRepository(_dbContext);
                }
                return _plotRepository;
            }
        }
    }

}
