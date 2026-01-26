using MediatR;
using RiceProduction.Application.Common.Interfaces.External;
using RiceProduction.Application.Common.Models;
using System;

namespace RiceProduction.Application.ReportFeature.Queries.GetContextualAISuggestions;

public class GetContextualAISuggestionsQuery : IRequest<Result<ContextualPlanSuggestions>>
{
    public Guid ReportId { get; set; }
    public Guid PlotCultivationId { get; set; }
}

