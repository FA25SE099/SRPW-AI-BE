using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Domain.Common;
using RiceProduction.Infrastructure.Data;
using RiceProduction.Infrastructure.Repository;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace RiceProduction.Infrastructure.UnitOfWork
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IMemoryCache _memoryCache;
        private readonly ConcurrentDictionary<string, object> _repos = new();

        private ISupervisorRepository? _supervisorRepository;
        private IClusterManagerRepository? _clusterManagerRepository;
        private IAgronomyExpertRepository? _agronomyExpertRepository;
        private IFarmerRepository? _farmerRepository;
        private IPlotRepository? _plotRepository;
        private IClusterRepository? _clusterRepository;
        private IUavVendorRepository? _uavVendorRepository;
        private ILateFarmerRecordRepository? _lateFarmerRecordRepository;

        public UnitOfWork(
            ApplicationDbContext context,
            ILoggerFactory loggerFactory,
            IMemoryCache memoryCache)
        {
            _dbContext = context ?? throw new ArgumentNullException(nameof(context));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            GC.SuppressFinalize(this);
        }

        public Task<int> CompleteAsync() => _dbContext.SaveChangesAsync();

        public Task<int> SaveChangesAsync(CancellationToken token) => CompleteAsync();

        public IGenericRepository<T> Repository<T>() where T : BaseAuditableEntity
        {
            return (IGenericRepository<T>)_repos.GetOrAdd(
                typeof(T).Name,
                _ => new GenericRepository<T>(_dbContext, _loggerFactory.CreateLogger<GenericRepository<T>>()));
        }

        public IGenericRepository<T> CachedRepository<T>() where T : BaseAuditableEntity
        {
            return (IGenericRepository<T>)_repos.GetOrAdd(
                typeof(T).Name,
                _ =>
                {
                    var innerRepo = new GenericRepository<T>(
                        _dbContext,
                        _loggerFactory.CreateLogger<GenericRepository<T>>());

                    return new DecoratorGenericRepository<T>(
                        innerRepo,
                        _memoryCache,
                        _loggerFactory.CreateLogger<DecoratorGenericRepository<T>>());
                });
        }

        public IFarmerRepository FarmerRepository =>
            _farmerRepository ??= new FarmerRepository(_dbContext);

        public ISupervisorRepository SupervisorRepository =>
            _supervisorRepository ??= new SupervisorRepository(_dbContext);

        public IClusterManagerRepository ClusterManagerRepository =>
            _clusterManagerRepository ??= new ClusterManagerRepository(_dbContext);

        public IAgronomyExpertRepository AgronomyExpertRepository =>
            _agronomyExpertRepository ??= new AgronomyExpertRepository(_dbContext);

        public IPlotRepository PlotRepository =>
            _plotRepository ??= new PlotRepository(_dbContext);

        public IClusterRepository ClusterRepository =>
            _clusterRepository ??= new ClusterRepository(_dbContext);

        public IUavVendorRepository UavVendorRepository =>
            _uavVendorRepository ??= new UavVendorRepository(_dbContext);

        public ILateFarmerRecordRepository LateFarmerRecordRepository =>
            _lateFarmerRecordRepository ??= new LateFarmerRecordRepository(_dbContext);
    }
}
