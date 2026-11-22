using RiceProduction.Domain.Common;

namespace RiceProduction.Application.Common.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IGenericRepository<T> Repository<T>() where T : BaseAuditableEntity;
    IGenericRepository<T> CachedRepository<T>() where T : BaseAuditableEntity;
    Task<int> CompleteAsync();
    Task<int> SaveChangesAsync(CancellationToken token);
    ISupervisorRepository SupervisorRepository { get; }
    IClusterManagerRepository ClusterManagerRepository { get; }
    IAgronomyExpertRepository AgronomyExpertRepository { get; }
    IFarmerRepository FarmerRepository { get; }
    IPlotRepository PlotRepository { get; }
    IClusterRepository ClusterRepository { get; }
    IUavVendorRepository UavVendorRepository { get; }
    ILateFarmerRecordRepository LateFarmerRecordRepository { get; }
}
