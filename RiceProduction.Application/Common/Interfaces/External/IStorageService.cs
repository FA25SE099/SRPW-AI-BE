using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.Common.Interfaces.External
{
    public interface IStorageService
    {

        Task<(string Url, string FileName)> UploadAsync(IFormFile file, string folder = null);


        Task<List<(string Url, string FileName)>> UploadMultipleAsync(IEnumerable<IFormFile> files, string folder = null);


        Task<bool> DeleteAsync(string blobName, string folder = null);


        Task<string> GetSasUrlAsync(string blobName, string folder = null, TimeSpan? expiry = null);
    }
}
