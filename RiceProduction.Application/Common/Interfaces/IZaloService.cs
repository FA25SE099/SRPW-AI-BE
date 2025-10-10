using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.Common.Interfaces
{
    public interface IZaloService
    {
        Task<string> GetOAAccessTokenAsync();
        Task<ZnsResult> SendZNSAsync(ZnsRequest request);

        Task<List<ZnsResult>> SendBulkZNSAsync(List<ZnsRequest> requests,
            CancellationToken cancellationToken = default);

        Task<ZnsResult> SendZNSWithSemaphoreAsync(ZnsRequest request, CancellationToken ct);
        bool IsValidZaloPhone(string phone);
        void Dispose();
    }
}
