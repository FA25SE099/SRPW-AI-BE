using RiceProduction.Domain.Common;
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
        IFarmerRepository FarmerRepository { get; }
        IPlotRepository PlotRepository { get; }
    }
}
