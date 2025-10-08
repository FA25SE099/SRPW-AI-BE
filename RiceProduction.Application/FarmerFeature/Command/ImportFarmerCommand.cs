using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.AspNetCore.Http;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.FarmerFeature.Command
{
    public class ImportFarmerCommand : IRequest<ImportFarmerResult>
    {
        public IFormFile File { get; set; }
        public ImportFarmerCommand(IFormFile file)
        {
            File = file;
        }
    }
}
