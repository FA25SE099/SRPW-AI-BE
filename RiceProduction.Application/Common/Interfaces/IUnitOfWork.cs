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
<<<<<<< HEAD
        IFarmerGenericRepository FarmerRepository { get; }
        ISupervisorGenericRepository SupervisorRepository { get; }
        IClusterManagerGenericRepository ClusterManagerRepository { get; }
        IPlotGenericRepository PlotRepository { get; }
=======
        IFarmerRepository FarmerRepository { get; }
        IPlotRepository PlotRepository { get; }
>>>>>>> 3d2167984c1f2c13d6e27c6c9dbdb52ac7f9736d
    }
}
