using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.PestProtocolResponses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.PestProtocolFeature.Queries.GetAllPestProtocols;

public class GetAllPestProtocolsQuery : IRequest<PagedResult<List<PestProtocolResponse>>>
{
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? SearchName { get; set; }
    public bool? IsActive { get; set; }
}
//db sửa ảnh material thành list
//tính giá riêng lúc emergency và preview standard
//thêm filter thời gian xem giá cho material 