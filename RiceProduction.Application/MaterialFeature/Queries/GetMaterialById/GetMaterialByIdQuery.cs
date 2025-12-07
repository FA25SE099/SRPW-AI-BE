using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.MaterialResponses;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.MaterialFeature.Queries.GetMaterialById;

public class GetMaterialByIdQuery : IRequest<Result<MaterialResponseForList>>
{
    [Required]
    public Guid MaterialId { get; set; }
}
