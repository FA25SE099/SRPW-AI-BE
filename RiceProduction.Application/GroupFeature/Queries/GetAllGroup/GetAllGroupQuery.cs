using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.GroupFeature.Queries.GetAllGroup
{
    public class GetAllGroupQuery : IRequest<Result<IEnumerable<GroupDTO>>>
    {
      
        
    }
}
