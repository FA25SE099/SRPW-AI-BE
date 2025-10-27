using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.Common.Interfaces
{
    public interface ICacheable
    {
        bool BypassCache { get => false; } 
        string CacheKey { get => string.Empty; } 
        int SlidingExpirationInMinutes { get => 30; } 
        int AbsoluteExpirationInMinutes { get => 60; }  
    }
}
