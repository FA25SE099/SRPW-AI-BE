using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Domain.Common;
using RiceProduction.Infrastructure.Data;
using RiceProduction.Infrastructure.Repository;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        private readonly IMemoryCache _memoryCache;
        private IFarmerRepository? _farmerRepository;
        private IPlotRepository? _plotRepository;

        // ===================================
        // === Constructors
        // ===================================
        public UnitOfWork(
            ApplicationDbContext context,
            ILoggerFactory loggerFactory,
            IMemoryCache memoryCache)
        {
            _dbContext = context ?? throw new ArgumentNullException(nameof(context));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
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
        public IGenericRepository<T> CachedRepository<T>() where T : BaseAuditableEntity
        {
            if (_repos == null) _repos = new ConcurrentDictionary<string, object>();

            var typeEntityName = typeof(T).Name;

            // Using reflection to create an instance of CachedGenericRepository<T> with type T
            // This wraps a plain GenericRepository<T> as the inner decorator
            // Passing db context, logger, and memory cache for each repository
            var repoInstanceTypeT = _repos.GetOrAdd(typeEntityName,
                valueFactory: _ =>
                {
                    var innerRepoType = typeof(GenericRepository<T>);
                    var innerRepoLogger = _loggerFactory.CreateLogger<GenericRepository<T>>();
                    var innerRepoInstance = Activator.CreateInstance(
                        innerRepoType,
                        _dbContext,
                        innerRepoLogger);

                    var cachedRepoType = typeof(DecoratorGenericRepository<T>);
                    var cachedRepoLogger = _loggerFactory.CreateLogger<DecoratorGenericRepository<T>>();

                    var cachedRepoInstance = Activator.CreateInstance(
                        cachedRepoType,
                        (IGenericRepository<T>)innerRepoInstance,
                        _memoryCache,
                        cachedRepoLogger);

                    return cachedRepoInstance;
                });

            return (IGenericRepository<T>)repoInstanceTypeT;
        }
        public IFarmerRepository FarmerRepository
        {
            get
            {
                if (_farmerRepository == null)
                {
                    _farmerRepository = new FarmerRepository(_dbContext);
                }
                return _farmerRepository;
            }
        }
        
        public IPlotRepository PlotRepository
        {
            get
            {
                if (_plotRepository == null)
                {
                    _plotRepository = new PlotRepository(_dbContext);
                }
                return _plotRepository;
            }
        }
    }

}
