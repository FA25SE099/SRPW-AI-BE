using RiceProduction.Domain.Common;
using RiceProduction.Infrastructure.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.Common.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        public IGenericRepository<T> Repository<T>() where T : BaseAuditableEntity;
        public IGenericRepository<T> CachedRepository<T>() where T : BaseAuditableEntity;
        Task<int> CompleteAsync();
        ISupervisorRepository SupervisorRepository { get; }
        IClusterManagerRepository ClusterManagerRepository { get; }
        IAgronomyExpertRepository AgronomyExpertRepository { get; }
        IFarmerRepository FarmerRepository { get; }
        IPlotRepository PlotRepository { get; }
        IClusterRepository? ClusterRepository { get; }
        IUavVendorRepository? UavVendorRepository { get; }
    }
}
