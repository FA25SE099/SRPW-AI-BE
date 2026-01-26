using MediatR;
using RiceProduction.Application.Common.Interfaces.External;
using RiceProduction.Application.Common.Models;
using System;

namespace RiceProduction.Application.ReportFeature.Queries.GetAIRecommendations;

public class GetAIRecommendationsQuery : IRequest<Result<EmergencyPlanRecommendation>>
{
    public Guid ReportId { get; set; }
}

