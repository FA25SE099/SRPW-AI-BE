using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace RiceProduction.Application.Common.Models
{
    public class FileUploadRequest
    {
        public IFormFile File { get; set; }
    }
}
