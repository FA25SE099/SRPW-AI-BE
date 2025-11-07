using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.AspNetCore.Http;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.FarmerFeature.Command.ImportFarmer
{
    public class ImportFarmerCommand : IRequest<ImportFarmerResult>
    {
        public IFormFile File { get; set; }
        public Guid? ClusterManagerId { get; set; }
        
        public ImportFarmerCommand(IFormFile file, Guid? clusterManagerId = null)
        {
            File = file;
            ClusterManagerId = clusterManagerId;
        }
    }
}
