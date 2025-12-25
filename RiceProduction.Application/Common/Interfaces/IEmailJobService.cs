using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static RiceProduction.Application.FarmerFeature.Events.SendEmailEvent.FarmerImportedEvent;

namespace RiceProduction.Application.Common.Interfaces
{
    public interface IEmailJobService
    {
        Task SendBulkFarmerAccountEmailsAsync(
            List<ImportedFarmerInfo> farmers,
            CancellationToken cancellationToken = default);
    }
}
