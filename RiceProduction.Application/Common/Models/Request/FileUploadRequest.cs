using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace RiceProduction.Application.Common.Models.Request
{
    public class FileUploadRequest
    {
        public IFormFile File { get; set; }
    }
}
