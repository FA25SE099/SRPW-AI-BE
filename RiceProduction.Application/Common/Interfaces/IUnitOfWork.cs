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

        Task<int> CompleteAsync();
        IFarmerGenericRepository FarmerRepository { get; }
        ISupervisorGenericRepository SupervisorRepository { get; }
        IPlotGenericRepository PlotRepository { get; }
    }
}
