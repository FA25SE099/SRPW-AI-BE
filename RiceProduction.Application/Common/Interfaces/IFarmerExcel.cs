using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.Common.Interfaces
{
    public interface IFarmerExcel 
    {
        Task<ImportFarmerResult> ImportFarmerFromExcelAsync(IFormFile file, CancellationToken cancellationToken = default);
    }
}
