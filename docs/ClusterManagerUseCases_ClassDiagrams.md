# Cluster Manager Use Cases - Class Diagrams

## Overview

This document provides comprehensive class diagrams for the SRPW-AI-BE system, organized by domain areas to support all Cluster Manager use cases.

**Architecture**: Clean Architecture with Domain-Driven Design  
**Pattern**: CQRS (Command Query Responsibility Segregation)  
**Framework**: ASP.NET Core 8.0 with Entity Framework Core

---

## 1. Domain Model - User Management & Authentication

```plantuml
@startuml UserManagement
!theme plain
title Domain Model: User Management & Authentication

abstract class ApplicationUser {
  + Id: Guid
  + UserName: string
  + Email: string
  + PhoneNumber: string
  + FullName: string
  + PasswordHash: string
  + IsActive: bool
  + EmailConfirmed: bool
  + PhoneNumberConfirmed: bool
  + CreatedAt: DateTime
  + UpdatedAt: DateTime
  + ClusterId: Guid?
  --
  + Cluster: Cluster?
}

class Farmer {
  + Address: string
  + FarmCode: string
  + NumberOfPlots: int
  + IsVerified: bool
  --
  + Plots: ICollection<Plot>
}

class ClusterManager {
  + AssignedDate: DateTime?
  --
  + ManagedCluster: Cluster?
}

class Supervisor {
  + SupervisionStartDate: DateTime?
  --
  + Groups: ICollection<Group>
}

class AgronomyExpert {
  + Specialization: string
  + YearsOfExperience: int
  --
  + ManagedCluster: Cluster?
}

class Admin {
  + Permissions: string
  + AccessLevel: int
}

class Cluster {
  + Id: Guid
  + ClusterName: string
  + ClusterManagerId: Guid?
  + AgronomyExpertId: Guid?
  + CreatedAt: DateTime
  + UpdatedAt: DateTime
  --
  + ClusterManager: ClusterManager?
  + AgronomyExpert: AgronomyExpert?
  + Farmers: ICollection<Farmer>
  + Supervisors: ICollection<Supervisor>
  + Groups: ICollection<Group>
  + YearSeasons: ICollection<YearSeason>
}

class RefreshToken {
  + Id: Guid
  + Token: string
  + UserId: Guid
  + ExpiresAt: DateTime
  + IsRevoked: bool
  + RevokedAt: DateTime?
  + ReplacedByToken: string?
  --
  + User: ApplicationUser
  + IsActive: bool
}

enum UserRole {
  Admin
  ClusterManager
  AgronomyExpert
  Supervisor
  Farmer
  UavVendor
}

ApplicationUser <|-- Farmer
ApplicationUser <|-- ClusterManager
ApplicationUser <|-- Supervisor
ApplicationUser <|-- AgronomyExpert
ApplicationUser <|-- Admin

ApplicationUser "1" --> "0..1" Cluster : belongs to
Cluster "1" --> "0..1" ClusterManager : managed by
Cluster "1" --> "0..1" AgronomyExpert : supervised by
Cluster "1" --> "0..*" Supervisor : has
Cluster "1" --> "0..*" Farmer : contains
ApplicationUser "1" --> "0..*" RefreshToken : has

note right of ApplicationUser
  Uses ASP.NET Core Identity
  Discriminator pattern for inheritance
  TPH (Table Per Hierarchy)
end note

@enduml
```

---

## 2. Domain Model - Cluster & Season Management

```plantuml
@startuml ClusterSeasonManagement
!theme plain
title Domain Model: Cluster & Season Management

class Cluster {
  + Id: Guid
  + ClusterName: string
  + ClusterManagerId: Guid?
  + AgronomyExpertId: Guid?
  + Description: string?
  + Location: string?
  + TotalArea: decimal
  + CreatedAt: DateTime
  + UpdatedAt: DateTime
  --
  + GetCurrentSeason(): YearSeason?
  + GetSeasonHistory(): IEnumerable<YearSeason>
}

class Season {
  + Id: Guid
  + SeasonName: string
  + Description: string?
  + StartMonth: int
  + EndMonth: int
  + OptimalPlantingStart: DateTime?
  + OptimalPlantingEnd: DateTime?
  + CreatedAt: DateTime
  --
  + YearSeasons: ICollection<YearSeason>
  --
  + IsCurrentSeason(year: int): bool
  + GetPlantingWindow(year: int): DateRange
}

class YearSeason {
  + Id: Guid
  + SeasonId: Guid
  + ClusterId: Guid
  + Year: int
  + Status: SeasonStatus
  + ActualStartDate: DateTime?
  + ActualEndDate: DateTime?
  + PlannedArea: decimal
  + ActualArea: decimal
  + CreatedAt: DateTime
  + UpdatedAt: DateTime
  --
  + Season: Season
  + Cluster: Cluster
  + ProductionPlans: ICollection<ProductionPlan>
  + PlotCultivations: ICollection<PlotCultivation>
  --
  + IsActive(): bool
  + CanAcceptNewGroups(): bool
  + GetProgress(): decimal
}

enum SeasonStatus {
  Planning
  Active
  InProgress
  Harvesting
  Completed
  Cancelled
}

class ClusterHistory {
  + Id: Guid
  + ClusterId: Guid
  + YearSeasonId: Guid
  + TotalFarmers: int
  + TotalPlots: int
  + TotalArea: decimal
  + TotalGroups: int
  + AverageYield: decimal
  + CompletionRate: decimal
  + RecordedAt: DateTime
  --
  + Cluster: Cluster
  + YearSeason: YearSeason
}

Cluster "1" --> "0..*" YearSeason : has
Season "1" --> "0..*" YearSeason : instances
YearSeason --> SeasonStatus
Cluster "1" --> "0..*" ClusterHistory : tracked by
YearSeason "1" --> "0..*" ClusterHistory : recorded in

note right of YearSeason
  Represents a specific season
  in a specific year for a cluster
  Example: "Winter-Spring 2024"
  in "Cluster A"
end note

@enduml
```

---

## 3. Domain Model - Plot & Cultivation Management

```plantuml
@startuml PlotCultivationManagement
!theme plain
title Domain Model: Plot & Cultivation Management

class Plot {
  + Id: Guid
  + FarmerId: Guid
  + SoThua: string
  + SoTo: string
  + Area: decimal
  + Coordinate: Point?
  + Boundary: Polygon?
  + Address: string?
  + HasBoundary: bool
  + IsActive: bool
  + CreatedAt: DateTime
  + UpdatedAt: DateTime
  --
  + Farmer: Farmer
  + PlotCultivations: ICollection<PlotCultivation>
  + GroupPlots: ICollection<GroupPlot>
  --
  + CalculateArea(): decimal
  + IsInGroup(seasonId: Guid): bool
  + GetCurrentCultivation(): PlotCultivation?
}

class PlotCultivation {
  + Id: Guid
  + PlotId: Guid
  + SeasonId: Guid
  + YearSeasonId: Guid
  + RiceVarietyId: Guid
  + PlantingDate: DateTime
  + ExpectedHarvestDate: DateTime?
  + ActualHarvestDate: DateTime?
  + EstimatedYield: decimal?
  + ActualYield: decimal?
  + Status: CultivationStatus
  + CreatedAt: DateTime
  --
  + Plot: Plot
  + Season: Season
  + YearSeason: YearSeason
  + RiceVariety: RiceVariety
  + ProductionPlan: ProductionPlan?
  --
  + IsLate(): bool
  + GetProgress(): decimal
}

enum CultivationStatus {
  Planned
  Preparing
  Planting
  Growing
  Harvesting
  Completed
  Failed
}

class RiceVariety {
  + Id: Guid
  + VarietyName: string
  + VarietyCode: string
  + Description: string?
  + GrowthDuration: int
  + OptimalPlantingPeriod: string?
  + ExpectedYield: decimal
  + ResistanceLevel: string?
  + IsActive: bool
  --
  + PlotCultivations: ICollection<PlotCultivation>
  + Groups: ICollection<Group>
}

class Farmer {
  + Id: Guid
  + FullName: string
  + PhoneNumber: string
  + Email: string?
  + Address: string
  + FarmCode: string
  + NumberOfPlots: int
  + ClusterId: Guid?
  --
  + Plots: ICollection<Plot>
  + Cluster: Cluster?
  --
  + GetTotalArea(): decimal
  + GetActivePlots(): IEnumerable<Plot>
}

class PolygonAssignmentTask {
  + Id: Guid
  + PlotId: Guid
  + SupervisorId: Guid
  + Status: TaskStatus
  + AssignedAt: DateTime
  + CompletedAt: DateTime?
  + PolygonGeoJson: string?
  + Notes: string?
  --
  + Plot: Plot
  + Supervisor: Supervisor
}

enum TaskStatus {
  Pending
  InProgress
  Completed
  Cancelled
}

Farmer "1" --> "0..*" Plot : owns
Plot "1" --> "0..*" PlotCultivation : has
PlotCultivation --> CultivationStatus
PlotCultivation "1" --> "1" RiceVariety : uses
Plot "1" --> "0..*" PolygonAssignmentTask : has
PolygonAssignmentTask --> TaskStatus

note right of Plot
  Spatial data stored as:
  - Coordinate: Point (centroid)
  - Boundary: Polygon (actual shape)
  Uses PostGIS types
end note

note left of PlotCultivation
  Represents cultivation activity
  for a specific plot in a season
  Links to ProductionPlan for tasks
end note

@enduml
```

---

## 4. Domain Model - Group Formation & Management

```plantuml
@startuml GroupManagement
!theme plain
title Domain Model: Group Formation & Management

class Group {
  + Id: Guid
  + ClusterId: Guid
  + SupervisorId: Guid?
  + RiceVarietyId: Guid
  + SeasonId: Guid
  + YearSeasonId: Guid
  + Year: int
  + GroupName: string?
  + PlantingDate: DateTime
  + TotalArea: decimal
  + PlotCount: int
  + Boundary: Polygon?
  + Centroid: Point?
  + IsManuallyCreated: bool
  + IsException: bool
  + ExceptionReason: string?
  + Status: GroupStatus
  + CreatedAt: DateTime
  + CreatedBy: Guid
  --
  + Cluster: Cluster
  + Supervisor: Supervisor?
  + RiceVariety: RiceVariety
  + Season: Season
  + YearSeason: YearSeason
  + GroupPlots: ICollection<GroupPlot>
  + ProductionPlans: ICollection<ProductionPlan>
  --
  + AddPlot(plot: Plot): void
  + RemovePlot(plotId: Guid): void
  + CalculateCentroid(): Point
  + GetProgress(): decimal
}

class GroupPlot {
  + GroupId: Guid
  + PlotId: Guid
  + AddedAt: DateTime
  + AddedBy: Guid
  --
  + Group: Group
  + Plot: Plot
}

enum GroupStatus {
  Draft
  Active
  InProgress
  Completed
  Cancelled
}

class GroupFormationParameters {
  + ProximityThreshold: double
  + PlantingDateTolerance: int
  + MinGroupArea: decimal
  + MaxGroupArea: decimal
  + MinPlotsPerGroup: int
  + MaxPlotsPerGroup: int
  + BorderBuffer: double
  --
  + Validate(): bool
  + GetDefault(): GroupFormationParameters
}

class ProposedGroup {
  + GroupNumber: int
  + RiceVarietyId: Guid
  + PlantingWindowStart: DateTime
  + PlantingWindowEnd: DateTime
  + MedianPlantingDate: DateTime
  + PlotIds: List<Guid>
  + CultivationIds: List<Guid>
  + PlotCount: int
  + TotalArea: decimal
  + GroupBoundary: Polygon?
  + GroupCentroid: Point?
  + IsValid: bool
  + ValidationErrors: List<string>
  --
  + MeetsConstraints(params: GroupFormationParameters): bool
  + CalculateCoherence(): double
}

class UngroupedPlotInfo {
  + PlotId: Guid
  + CultivationId: Guid
  + RiceVarietyId: Guid
  + PlantingDate: DateTime
  + Area: decimal
  + Centroid: Point?
  + UngroupedReason: UngroupReason
  + ReasonDetails: string
  + NearestGroupId: Guid?
  + DistanceToNearestGroup: double?
  --
  + GetRecommendedActions(): List<string>
  + CanJoinGroup(groupId: Guid): bool
}

enum UngroupReason {
  IsolatedLocation
  TooSpreadOut
  PlantingDateTooFar
  NoValidGroup
  TooManyPlots
  TooFewPlots
  TooSmallArea
  TooLargeArea
  NoCoordinates
  AlreadyGrouped
  OtherReason
}

class Plot {
  + Id: Guid
  + Area: decimal
  + Coordinate: Point?
  + Boundary: Polygon?
}

class RiceVariety {
  + Id: Guid
  + VarietyName: string
}

Group "1" --> "0..*" GroupPlot : contains
Plot "1" --> "0..*" GroupPlot : assigned to
Group "1" --> "1" RiceVariety : cultivates
Group --> GroupStatus
ProposedGroup --> GroupFormationParameters : validated by
UngroupedPlotInfo --> UngroupReason
UngroupedPlotInfo "0..*" --> "1" Plot : references

note right of Group
  Groups are formed either:
  1. Automatically via algorithms
  2. Manually by Cluster Manager
  3. Using PostGIS spatial analysis
end note

note left of GroupFormationParameters
  Used by both:
  - Regular DBSCAN clustering
  - PostGIS ST_ClusterDBSCAN
end note

@enduml
```

---

## 5. Domain Model - Production Plan & Task Management

```plantuml
@startuml ProductionPlanManagement
!theme plain
title Domain Model: Production Plan & Task Management

class ProductionPlan {
  + Id: Guid
  + PlotCultivationId: Guid
  + YearSeasonId: Guid
  + GroupId: Guid?
  + StandardPlanId: Guid?
  + PlanName: string
  + StartDate: DateTime
  + EndDate: DateTime
  + EstimatedCost: decimal
  + ActualCost: decimal?
  + Status: PlanStatus
  + Progress: decimal
  + CreatedAt: DateTime
  + CreatedBy: Guid
  --
  + PlotCultivation: PlotCultivation
  + YearSeason: YearSeason
  + Group: Group?
  + StandardPlan: StandardPlan?
  + CultivationTasks: ICollection<CultivationTask>
  + MaterialUsages: ICollection<MaterialUsage>
  --
  + CalculateProgress(): decimal
  + GetLateTasks(): IEnumerable<CultivationTask>
  + IsOnSchedule(): bool
}

enum PlanStatus {
  Draft
  Approved
  InProgress
  OnHold
  Completed
  Cancelled
}

class CultivationTask {
  + Id: Guid
  + ProductionPlanId: Guid
  + TaskName: string
  + Description: string?
  + TaskType: TaskType
  + ScheduledStartDate: DateTime
  + ScheduledEndDate: DateTime
  + ActualStartDate: DateTime?
  + ActualCompletionDate: DateTime?
  + Status: TaskStatus
  + Priority: int
  + AssignedTo: Guid?
  + Notes: string?
  + CreatedAt: DateTime
  --
  + ProductionPlan: ProductionPlan
  + LateFarmerRecord: LateFarmerRecord?
  + MaterialUsages: ICollection<MaterialUsage>
  --
  + IsLate(): bool
  + GetDaysLate(): int
  + CanComplete(): bool
}

enum TaskType {
  LandPreparation
  Seeding
  Transplanting
  Fertilization
  Irrigation
  PestControl
  Weeding
  Monitoring
  Harvesting
  PostHarvest
}

enum TaskStatus {
  Pending
  InProgress
  Completed
  Late
  Skipped
  Cancelled
}

class LateFarmerRecord {
  + Id: Guid
  + FarmerId: Guid
  + PlotId: Guid
  + CultivationTaskId: Guid
  + YearSeasonId: Guid
  + DaysLate: int
  + ScheduledDate: DateTime
  + ActualCompletionDate: DateTime
  + Reason: string?
  + Severity: LateSeverity
  + CreatedAt: DateTime
  --
  + Farmer: Farmer
  + Plot: Plot
  + CultivationTask: CultivationTask
  + YearSeason: YearSeason
}

enum LateSeverity {
  Minor
  Moderate
  Severe
  Critical
}

class Material {
  + Id: Guid
  + MaterialName: string
  + MaterialType: MaterialType
  + Unit: string
  + Description: string?
  + UnitPrice: decimal
  + IsActive: bool
  --
  + MaterialUsages: ICollection<MaterialUsage>
}

enum MaterialType {
  Seed
  Fertilizer
  Pesticide
  Herbicide
  Equipment
  Other
}

class MaterialUsage {
  + Id: Guid
  + ProductionPlanId: Guid
  + CultivationTaskId: Guid?
  + MaterialId: Guid
  + PlannedQuantity: decimal
  + ActualQuantity: decimal?
  + UsageDate: DateTime?
  + Cost: decimal
  + Notes: string?
  --
  + ProductionPlan: ProductionPlan
  + CultivationTask: CultivationTask?
  + Material: Material
}

class StandardPlan {
  + Id: Guid
  + PlanName: string
  + RiceVarietyId: Guid
  + Description: string?
  + DurationDays: int
  + IsActive: bool
  --
  + RiceVariety: RiceVariety
  + StandardTasks: ICollection<StandardTask>
  + ProductionPlans: ICollection<ProductionPlan>
}

class StandardTask {
  + Id: Guid
  + StandardPlanId: Guid
  + TaskName: string
  + TaskType: TaskType
  + DayOffset: int
  + Duration: int
  + Description: string?
  --
  + StandardPlan: StandardPlan
}

ProductionPlan --> PlanStatus
ProductionPlan "1" --> "0..*" CultivationTask : contains
CultivationTask --> TaskType
CultivationTask --> TaskStatus
CultivationTask "1" --> "0..1" LateFarmerRecord : tracked by
LateFarmerRecord --> LateSeverity
ProductionPlan "1" --> "0..*" MaterialUsage : uses
CultivationTask "1" --> "0..*" MaterialUsage : requires
Material --> MaterialType
Material "1" --> "0..*" MaterialUsage : consumed in
StandardPlan "1" --> "0..*" StandardTask : defines
StandardPlan "1" --> "0..*" ProductionPlan : generates

note right of ProductionPlan
  Created from StandardPlan
  or customized by Expert
  Tracks progress and costs
end note

note left of LateFarmerRecord
  Automatically created when
  task completion is late
  Used for performance tracking
end note

@enduml
```

---

## 6. Application Layer - CQRS Commands (UC-CM01 to UC-CM09)

```plantuml
@startuml CQRSCommands
!theme plain
title Application Layer: CQRS Commands & Queries (UC-CM01 to UC-CM09)

package "Commands" {
  class CreateSupervisorCommand {
    + FullName: string
    + Email: string
    + PhoneNumber: string
    + ClusterId: Guid
    --
    + Validate(): ValidationResult
  }
  
  class CreateClusterCommand {
    + ClusterName: string
    + ClusterManagerId: Guid?
    + AgronomyExpertId: Guid?
    + SupervisorIds: List<Guid>
  }
}

package "Queries" {
  class GetClusterIdByManagerIdQuery {
    + ClusterManagerId: Guid
  }
  
  class GetAllSupervisorQuery {
    + SearchNameOrEmail: string?
    + SearchPhoneNumber: string?
    + CurrentPage: int
    + PageSize: int
  }
  
  class GetAllFarmerQuery {
    + PageNumber: int
    + PageSize: int
    + SearchTerm: string?
    + ClusterManagerId: Guid?
  }
  
  class GetFarmerDetailQuery {
    + FarmerId: Guid
  }
  
  class GetClusterAvailableSeasonsQuery {
    + ClusterId: Guid
    + IncludeEmpty: bool
    + Limit: int?
  }
  
  class GetClusterCurrentSeasonQuery {
    + ClusterId: Guid
  }
  
  class GetClusterHistoryQuery {
    + ClusterId: Guid
    + SeasonId: Guid?
    + Year: int?
    + Limit: int
  }
  
  class GetYearSeasonsByClusterQuery {
    + ClusterId: Guid
  }
}

package "Handlers" {
  class CreateSupervisorCommandHandler {
    - _userManager: UserManager
    - _unitOfWork: IUnitOfWork
    --
    + Handle(command: CreateSupervisorCommand): Task<Result<Guid>>
  }
  
  class GetClusterIdByManagerIdQueryHandler {
    - _unitOfWork: IUnitOfWork
    --
    + Handle(query: GetClusterIdByManagerIdQuery): Task<Result<Guid?>>
  }
  
  class GetAllSupervisorQueryHandler {
    - _unitOfWork: IUnitOfWork
    - _mapper: IMapper
    --
    + Handle(query: GetAllSupervisorQuery): Task<PagedResult<List<SupervisorResponse>>>
  }
  
  class GetFarmerDetailQueryHandler {
    - _unitOfWork: IUnitOfWork
    - _mapper: IMapper
    --
    + Handle(query: GetFarmerDetailQuery): Task<Result<FarmerDetailDTO>>
  }
  
  class GetClusterHistoryQueryHandler {
    - _unitOfWork: IUnitOfWork
    - _mapper: IMapper
    --
    + Handle(query: GetClusterHistoryQuery): Task<Result<ClusterHistoryResponse>>
  }
}

interface IRequestHandler<TRequest, TResponse> {
  + Handle(request: TRequest, cancellationToken: CancellationToken): Task<TResponse>
}

CreateSupervisorCommand ..> CreateSupervisorCommandHandler
GetClusterIdByManagerIdQuery ..> GetClusterIdByManagerIdQueryHandler
GetAllSupervisorQuery ..> GetAllSupervisorQueryHandler
GetFarmerDetailQuery ..> GetFarmerDetailQueryHandler
GetClusterHistoryQuery ..> GetClusterHistoryQueryHandler

CreateSupervisorCommandHandler ..|> IRequestHandler
GetClusterIdByManagerIdQueryHandler ..|> IRequestHandler
GetAllSupervisorQueryHandler ..|> IRequestHandler
GetFarmerDetailQueryHandler ..|> IRequestHandler
GetClusterHistoryQueryHandler ..|> IRequestHandler

note right of IRequestHandler
  MediatR pattern
  All commands and queries
  implement IRequest<TResponse>
end note

@enduml
```

---

## 7. Application Layer - Group Management Commands (UC-CM10 to UC-CM17)

```plantuml
@startuml GroupManagementCommands
!theme plain
title Application Layer: Group Management CQRS (UC-CM10 to UC-CM17)

package "Commands" {
  class FormGroupsCommand {
    + ClusterId: Guid
    + SeasonId: Guid
    + Year: int
    + Parameters: GroupFormationParameters?
    + AutoAssignSupervisors: bool
    + CreateGroupsImmediately: bool
  }
  
  class CreateGroupManuallyCommand {
    + ClusterId: Guid
    + SupervisorId: Guid
    + RiceVarietyId: Guid
    + SeasonId: Guid
    + Year: int
    + PlantingDate: DateTime
    + PlotIds: List<Guid>
    + IsException: bool
    + ExceptionReason: string?
  }
}

package "Queries" {
  class PreviewGroupsQuery {
    + ClusterId: Guid
    + SeasonId: Guid
    + Year: int
    + ProximityThreshold: double?
    + PlantingDateTolerance: int?
    + MinGroupArea: decimal?
    + MaxGroupArea: decimal?
    + MinPlotsPerGroup: int?
    + MaxPlotsPerGroup: int?
  }
  
  class GetAllGroupQuery {
  }
  
  class GetGroupsByClusterManagerIdQuery {
    + CurrentPage: int
    + PageSize: int
  }
  
  class GetGroupDetailQuery {
    + GroupId: Guid
  }
  
  class GetUngroupedPlotsQuery {
    + ClusterId: Guid
    + SeasonId: Guid
    + Year: int
  }
}

package "Handlers" {
  class FormGroupsCommandHandler {
    - _groupFormationService: IGroupFormationService
    - _postGISService: IPostGISGroupFormationService
    - _unitOfWork: IUnitOfWork
    --
    + Handle(command: FormGroupsCommand): Task<Result<FormGroupsResponse>>
    - AssignSupervisors(groups: List<Group>): Task
    - CreateGroupEntities(proposed: List<ProposedGroup>): Task
  }
  
  class FormGroupsPostGISCommandHandler {
    - _postGISService: IPostGISGroupFormationService
    - _unitOfWork: IUnitOfWork
    --
    + Handle(command: FormGroupsCommand): Task<Result<FormGroupsResponse>>
  }
  
  class CreateGroupManuallyCommandHandler {
    - _unitOfWork: IUnitOfWork
    - _mapper: IMapper
    --
    + Handle(command: CreateGroupManuallyCommand): Task<Result<Guid>>
    - ValidatePlots(plotIds: List<Guid>): Task<ValidationResult>
    - CalculateGroupMetrics(plots: List<Plot>): GroupMetrics
  }
  
  class PreviewGroupsQueryHandler {
    - _groupFormationService: IGroupFormationService
    --
    + Handle(query: PreviewGroupsQuery): Task<Result<PreviewGroupsResponse>>
  }
  
  class GetGroupDetailQueryHandler {
    - _unitOfWork: IUnitOfWork
    - _mapper: IMapper
    --
    + Handle(query: GetGroupDetailQuery): Task<Result<GroupDetailResponse>>
  }
  
  class GetUngroupedPlotsQueryHandler {
    - _unitOfWork: IUnitOfWork
    - _groupFormationService: IGroupFormationService
    --
    + Handle(query: GetUngroupedPlotsQuery): Task<Result<UngroupedPlotsResponse>>
    - AnalyzeUngroupedReasons(plots: List<Plot>): Task<List<UngroupedPlotInfo>>
  }
}

package "Services" {
  interface IGroupFormationService {
    + FormGroupsAsync(parameters: GroupFormationParameters, clusterId: Guid?, seasonId: Guid?): Task<GroupFormationResult>
    + AnalyzeUngroupedPlots(plots: List<Plot>, groups: List<Group>): Task<List<UngroupedPlotInfo>>
  }
  
  class GroupFormationService {
    - _unitOfWork: IUnitOfWork
    --
    + FormGroupsAsync(...): Task<GroupFormationResult>
    - SpatialClusteringDBSCAN(plots: List<Plot>, eps: double, minPoints: int): List<List<Plot>>
    - TemporalClustering(plots: List<Plot>, tolerance: int): List<List<Plot>>
    - ValidateGroup(plots: List<Plot>, params: GroupFormationParameters): bool
  }
  
  interface IPostGISGroupFormationService {
    + FormGroupsAsync(parameters: PostGISGroupingParameters, clusterId: Guid?, seasonId: Guid?): Task<PostGISGroupFormationResult>
  }
  
  class PostGISGroupFormationService {
    - _context: ApplicationDbContext
    --
    + FormGroupsAsync(...): Task<PostGISGroupFormationResult>
    - ExecutePostGISQuery(sql: string, parameters: Dictionary): Task<DataReader>
    - ParsePostGISResults(reader: DataReader): PostGISGroupFormationResult
  }
}

FormGroupsCommand ..> FormGroupsCommandHandler
FormGroupsCommand ..> FormGroupsPostGISCommandHandler
CreateGroupManuallyCommand ..> CreateGroupManuallyCommandHandler
PreviewGroupsQuery ..> PreviewGroupsQueryHandler
GetGroupDetailQuery ..> GetGroupDetailQueryHandler
GetUngroupedPlotsQuery ..> GetUngroupedPlotsQueryHandler

FormGroupsCommandHandler --> IGroupFormationService
FormGroupsCommandHandler --> IPostGISGroupFormationService
FormGroupsPostGISCommandHandler --> IPostGISGroupFormationService
PreviewGroupsQueryHandler --> IGroupFormationService
GetUngroupedPlotsQueryHandler --> IGroupFormationService

IGroupFormationService <|.. GroupFormationService
IPostGISGroupFormationService <|.. PostGISGroupFormationService

note right of FormGroupsCommandHandler
  Can use either:
  1. In-memory DBSCAN clustering
  2. PostGIS spatial clustering
  Based on configuration
end note

note left of PreviewGroupsQueryHandler
  Read-only operation
  No database changes
  Used for preview before creation
end note

@enduml
```

---

## 8. Application Layer - Plot & Performance Tracking (UC-CM18 to UC-CM20)

```plantuml
@startuml PlotPerformanceTracking
!theme plain
title Application Layer: Plot & Performance Tracking (UC-CM18 to UC-CM20)

package "Queries" {
  class GetAllPlotQuery {
    + PageNumber: int
    + PageSize: int
    + SearchTerm: string?
    + ClusterManagerId: Guid?
  }
  
  class GetLateFarmersInClusterQuery {
    + AgronomyExpertId: Guid?
    + SupervisorId: Guid?
    + PageNumber: int
    + PageSize: int
    + SearchTerm: string?
  }
  
  class GetLatePlotsInClusterQuery {
    + AgronomyExpertId: Guid?
    + SupervisorId: Guid?
    + PageNumber: int
    + PageSize: int
    + SearchTerm: string?
  }
  
  class GetLateCountByFarmerIdQuery {
    + FarmerId: Guid
  }
  
  class GetLateDetailByFarmerIdQuery {
    + FarmerId: Guid
  }
  
  class GetLateCountByPlotIdQuery {
    + PlotId: Guid
  }
}

package "Commands" {
  class CreateLateFarmerRecordCommand {
    + CultivationTaskId: Guid
    + Reason: string?
  }
}

package "Handlers" {
  class GetAllPlotQueryHandler {
    - _unitOfWork: IUnitOfWork
    - _mapper: IMapper
    --
    + Handle(query: GetAllPlotQuery): Task<PagedResult<List<PlotDTO>>>
  }
  
  class GetLateFarmersInClusterQueryHandler {
    - _unitOfWork: IUnitOfWork
    - _mapper: IMapper
    --
    + Handle(query: GetLateFarmersInClusterQuery): Task<PagedResult<List<FarmerWithLateCountDTO>>>
    - GetClusterIdFromExpertOrSupervisor(expertId: Guid?, supervisorId: Guid?): Task<Guid>
    - CalculateLateSeverity(lateCount: int, totalTasks: int): LateSeverity
  }
  
  class GetLatePlotsInClusterQueryHandler {
    - _unitOfWork: IUnitOfWork
    - _mapper: IMapper
    --
    + Handle(query: GetLatePlotsInClusterQuery): Task<PagedResult<List<PlotWithLateCountDTO>>>
    - GetLatePlotStatistics(plotId: Guid): Task<PlotLateStatistics>
    - GetRecommendations(plotStats: PlotLateStatistics): List<string>
  }
  
  class CreateLateFarmerRecordCommandHandler {
    - _unitOfWork: IUnitOfWork
    --
    + Handle(command: CreateLateFarmerRecordCommand): Task<Result<Guid>>
    - CalculateDaysLate(task: CultivationTask): int
    - DetermineSeverity(daysLate: int): LateSeverity
  }
}

package "DTOs" {
  class PlotDTO {
    + Id: Guid
    + SoThua: string
    + SoTo: string
    + Area: decimal
    + FarmerId: Guid
    + FarmerName: string
    + FarmerPhone: string?
    + ClusterId: Guid?
    + ClusterName: string?
    + HasBoundary: bool
    + Coordinate: CoordinateDto?
  }
  
  class FarmerWithLateCountDTO {
    + FarmerId: Guid
    + FullName: string
    + PhoneNumber: string
    + Email: string?
    + LateCount: int
    + TotalTasks: int
    + LatePercentage: decimal
    + LastLateDate: DateTime?
    + Severity: LateSeverity
    + ClusterId: Guid
    + ClusterName: string
  }
  
  class PlotWithLateCountDTO {
    + PlotId: Guid
    + SoThua: string
    + SoTo: string
    + Area: decimal
    + FarmerId: Guid
    + FarmerName: string
    + FarmerPhone: string
    + LateCount: int
    + TotalTasks: int
    + LatePercentage: decimal
    + LastLateDate: DateTime?
    + LateTasks: List<string>
    + Recommendations: List<string>
  }
  
  class FarmerLateCountDTO {
    + FarmerId: Guid
    + FullName: string
    + TotalLateRecords: int
    + LatestLateRecord: DateTime?
  }
  
  class PlotLateCountDTO {
    + PlotId: Guid
    + SoThua: string
    + SoTo: string
    + TotalLateRecords: int
  }
  
  class FarmerLateDetailDTO {
    + FarmerId: Guid
    + FullName: string
    + PhoneNumber: string
    + TotalLateRecords: int
    + LateRecords: List<LateFarmerRecordDTO>
  }
}

GetAllPlotQuery ..> GetAllPlotQueryHandler
GetLateFarmersInClusterQuery ..> GetLateFarmersInClusterQueryHandler
GetLatePlotsInClusterQuery ..> GetLatePlotsInClusterQueryHandler
CreateLateFarmerRecordCommand ..> CreateLateFarmerRecordCommandHandler

GetAllPlotQueryHandler ..> PlotDTO
GetLateFarmersInClusterQueryHandler ..> FarmerWithLateCountDTO
GetLatePlotsInClusterQueryHandler ..> PlotWithLateCountDTO

note right of GetLateFarmersInClusterQueryHandler
  Complex aggregation query
  Groups late records by farmer
  Calculates performance metrics
  Ranks by severity
end note

note left of PlotWithLateCountDTO
  Includes recommendations:
  - Increase monitoring
  - Provide additional support
  - Adjust planting schedule
  - Assign different supervisor
end note

@enduml
```

---

## 9. Infrastructure Layer - Repositories & Unit of Work

```plantuml
@startuml RepositoryPattern
!theme plain
title Infrastructure Layer: Repository Pattern & Unit of Work

interface IRepository<T> {
  + GetByIdAsync(id: Guid): Task<T?>
  + GetAllAsync(): Task<IEnumerable<T>>
  + FindAsync(predicate: Expression<Func<T, bool>>): Task<T?>
  + AddAsync(entity: T): Task
  + AddRangeAsync(entities: IEnumerable<T>): Task
  + UpdateAsync(entity: T): Task
  + DeleteAsync(id: Guid): Task
  + GenerateNewGuid(guid: Guid): Task<Guid>
}

class Repository<T> {
  # _context: ApplicationDbContext
  # _dbSet: DbSet<T>
  --
  + GetByIdAsync(id: Guid): Task<T?>
  + GetAllAsync(): Task<IEnumerable<T>>
  + FindAsync(predicate: Expression<Func<T, bool>>): Task<T?>
  + AddAsync(entity: T): Task
  + UpdateAsync(entity: T): Task
  + DeleteAsync(id: Guid): Task
}

interface IClusterManagerRepository {
  + GetClusterManagerByIdAsync(id: Guid): Task<ClusterManager?>
  + GetClusterIdByManagerId(managerId: Guid): Task<Guid?>
  + GetFreeClusterManagers(): Task<IEnumerable<ClusterManager>>
  + GetClusterManagersWithPaging(page: int, size: int, search: string?): Task<PagedResult<ClusterManager>>
}

class ClusterManagerRepository {
  - _context: ApplicationDbContext
  --
  + GetClusterManagerByIdAsync(id: Guid): Task<ClusterManager?>
  + GetClusterIdByManagerId(managerId: Guid): Task<Guid?>
}

interface IFarmerRepository {
  + GetFarmerByIdAsync(id: Guid): Task<Farmer?>
  + GetFarmerByPhoneNumber(phone: string): Task<Farmer?>
  + GetFarmersWithPaging(page: int, size: int, search: string?, managerId: Guid?): Task<PagedResult<Farmer>>
  + GetFarmerDetailAsync(id: Guid): Task<FarmerDetailDTO?>
}

interface IPlotRepository {
  + GetPlotsByFarmerId(farmerId: Guid): Task<IEnumerable<Plot>>
  + GetPlotsByClusterId(clusterId: Guid): Task<IEnumerable<Plot>>
  + GetPlotsWithBoundary(): Task<IEnumerable<Plot>>
  + GetPlotsNotInGroups(clusterId: Guid, seasonId: Guid, year: int): Task<IEnumerable<Plot>>
  + GetPlotsAwaitingPolygon(filters: PlotFilters): Task<PagedResult<Plot>>
}

interface IGroupRepository {
  + GetGroupByIdWithDetails(id: Guid): Task<Group?>
  + GetGroupsByClusterId(clusterId: Guid): Task<IEnumerable<Group>>
  + GetGroupsBySeasonId(seasonId: Guid): Task<IEnumerable<Group>>
  + GetNearbyGroups(coordinate: Point, riceVarietyId: Guid, limit: int): Task<IEnumerable<Group>>
}

interface ISupervisorRepository {
  + GetSupervisorsByClusterId(clusterId: Guid): Task<IEnumerable<Supervisor>>
  + GetAvailableSupervisors(clusterId: Guid, seasonId: Guid): Task<IEnumerable<Supervisor>>
  + GetSupervisorWorkload(supervisorId: Guid, seasonId: Guid): Task<SupervisorWorkload>
}

interface IYearSeasonRepository {
  + GetByClusterIdAsync(clusterId: Guid): Task<IEnumerable<YearSeason>>
  + GetCurrentSeasonForCluster(clusterId: Guid): Task<YearSeason?>
  + GetClusterHistory(clusterId: Guid, filters: HistoryFilters): Task<IEnumerable<YearSeason>>
}

interface ILateFarmerRecordRepository {
  + GetByFarmerIdAsync(farmerId: Guid): Task<IEnumerable<LateFarmerRecord>>
  + GetByPlotIdAsync(plotId: Guid): Task<IEnumerable<LateFarmerRecord>>
  + GetLateFarmersInCluster(clusterId: Guid, filters: LateFilters): Task<PagedResult<LateFarmerRecord>>
  + GetLatePlotsInCluster(clusterId: Guid, filters: LateFilters): Task<PagedResult<LateFarmerRecord>>
}

interface IUnitOfWork {
  + Repository<T>(): IRepository<T>
  + ClusterRepository: IRepository<Cluster>
  + ClusterManagerRepository: IClusterManagerRepository
  + FarmerRepository: IFarmerRepository
  + PlotRepository: IPlotRepository
  + GroupRepository: IGroupRepository
  + SupervisorRepository: ISupervisorRepository
  + YearSeasonRepository: IYearSeasonRepository
  + LateFarmerRecordRepository: ILateFarmerRecordRepository
  + ProductionPlanRepository: IRepository<ProductionPlan>
  + CultivationTaskRepository: IRepository<CultivationTask>
  --
  + SaveChangesAsync(cancellationToken: CancellationToken): Task<int>
  + BeginTransactionAsync(): Task<IDbContextTransaction>
  + CommitAsync(): Task
  + RollbackAsync(): Task
}

class UnitOfWork {
  - _context: ApplicationDbContext
  - _repositories: Dictionary<Type, object>
  - _transaction: IDbContextTransaction?
  --
  + Repository<T>(): IRepository<T>
  + SaveChangesAsync(cancellationToken: CancellationToken): Task<int>
  + BeginTransactionAsync(): Task<IDbContextTransaction>
  + Dispose(): void
}

IRepository <|.. Repository
IClusterManagerRepository <|.. ClusterManagerRepository
Repository <|-- ClusterManagerRepository

IUnitOfWork <|.. UnitOfWork
UnitOfWork --> IClusterManagerRepository
UnitOfWork --> IFarmerRepository
UnitOfWork --> IPlotRepository
UnitOfWork --> IGroupRepository
UnitOfWork --> ISupervisorRepository
UnitOfWork --> IYearSeasonRepository
UnitOfWork --> ILateFarmerRecordRepository

note right of IUnitOfWork
  Manages database transactions
  Provides access to all repositories
  Ensures consistency across operations
end note

note left of Repository
  Generic base repository
  Common CRUD operations
  Used by all specific repositories
end note

@enduml
```

---

## 10. Infrastructure Layer - Services & External Integrations

```plantuml
@startuml InfrastructureServices
!theme plain
title Infrastructure Layer: Services & External Integrations

package "Spatial Services" {
  interface IPostGISGroupFormationService {
    + FormGroupsAsync(parameters: PostGISGroupingParameters, clusterId: Guid?, seasonId: Guid?): Task<PostGISGroupFormationResult>
  }
  
  class PostGISGroupFormationService {
    - _context: ApplicationDbContext
    --
    + FormGroupsAsync(...): Task<PostGISGroupFormationResult>
    - ExecuteSpatialQuery(sql: string): Task<DataReader>
    - ParseSpatialResults(reader: DataReader): PostGISGroupFormationResult
  }
  
  interface ISpatialService {
    + CalculateDistance(point1: Point, point2: Point): double
    + CalculateCentroid(polygons: List<Polygon>): Point
    + UnionPolygons(polygons: List<Polygon>): Polygon
    + BufferPolygon(polygon: Polygon, distance: double): Polygon
    + IsWithinDistance(point1: Point, point2: Point, threshold: double): bool
  }
  
  class PostGISSpatialService {
    - _context: ApplicationDbContext
    --
    + CalculateDistance(point1: Point, point2: Point): double
    + CalculateCentroid(polygons: List<Polygon>): Point
    - ConvertToWKT(geometry: Geometry): string
    - ConvertFromWKT(wkt: string): Geometry
  }
}

package "Identity Services" {
  interface IIdentityService {
    + LoginAsync(emailOrPhone: string, password: string, isEmail: bool): Task<AuthenticationResult>
    + LogoutAsync(userId: Guid, refreshToken: string?): Task<Result>
    + CreateUserAsync(user: ApplicationUser, password: string, role: string): Task<Result<Guid>>
    + GetUserByIdAsync(userId: Guid): Task<ApplicationUser?>
    + RevokeAllUserTokensAsync(userId: Guid): Task<Result>
  }
  
  class IdentityService {
    - _userManager: UserManager<ApplicationUser>
    - _signInManager: SignInManager<ApplicationUser>
    - _tokenService: ITokenService
    - _context: ApplicationDbContext
    --
    + LoginAsync(...): Task<AuthenticationResult>
    + LogoutAsync(...): Task<Result>
    + CreateUserAsync(...): Task<Result<Guid>>
  }
  
  interface ITokenService {
    + GenerateAccessToken(userId: Guid, userName: string, email: string, roles: IList<string>): string
    + GenerateRefreshToken(): string
    + GetPrincipalFromExpiredToken(token: string): ClaimsPrincipal?
    + ValidateToken(token: string): bool
    + GetTokenExpiration(token: string): DateTime
  }
  
  class TokenService {
    - _jwtSecret: string
    - _issuer: string
    - _audience: string
    - _accessTokenExpirationMinutes: int
    --
    + GenerateAccessToken(...): string
    + GenerateRefreshToken(): string
    + ValidateToken(token: string): bool
  }
}

package "Notification Services" {
  interface IEmailService {
    + SendEmailAsync(request: SimpleEmailRequest, cancellationToken: CancellationToken): Task<Result<EmailResponse>>
    + SendBulkEmailAsync(requests: List<SimpleEmailRequest>, cancellationToken: CancellationToken): Task<Result<EmailBatchResult>>
  }
  
  class InfobipEmailService {
    - _httpClient: HttpClient
    - _apiKey: string
    - _context: ApplicationDbContext
    --
    + SendEmailAsync(...): Task<Result<EmailResponse>>
    + SendBulkEmailAsync(...): Task<Result<EmailBatchResult>>
    - TrackEmailInDatabase(email: EmailMessage): Task
  }
  
  interface ISmsService {
    + SendSmsAsync(phoneNumber: string, message: string): Task<Result<SmsResponse>>
    + SendBulkSmsAsync(requests: List<SmsRequest>): Task<Result<SmsBatchResult>>
  }
  
  class InfobipSmsService {
    - _httpClient: HttpClient
    - _apiKey: string
    --
    + SendSmsAsync(...): Task<Result<SmsResponse>>
    + SendBulkSmsAsync(...): Task<Result<SmsBatchResult>>
  }
}

package "Excel Services" {
  interface IFarmerExcel {
    + ImportFarmerFromExcelAsync(file: IFormFile, clusterManagerId: Guid?, cancellationToken: CancellationToken): Task<ImportFarmerResult>
    + ExportFarmersToExcelAsync(farmers: List<Farmer>): Task<byte[]>
  }
  
  class FarmerExcelImplement {
    - _userManager: UserManager<ApplicationUser>
    - _context: ApplicationDbContext
    --
    + ImportFarmerFromExcelAsync(...): Task<ImportFarmerResult>
    - ValidateExcelRow(row: FarmerDTO): ValidationResult
    - CreateFarmerAccount(dto: FarmerDTO, clusterId: Guid): Task<Farmer>
  }
  
  interface IPlotExcel {
    + ImportPlotFromExcelAsync(file: IFormFile, cancellationToken: CancellationToken): Task<ImportPlotResult>
    + ExportPlotsToExcelAsync(plots: List<Plot>): Task<byte[]>
  }
}

IPostGISGroupFormationService <|.. PostGISGroupFormationService
ISpatialService <|.. PostGISSpatialService
IIdentityService <|.. IdentityService
ITokenService <|.. TokenService
IEmailService <|.. InfobipEmailService
ISmsService <|.. InfobipSmsService
IFarmerExcel <|.. FarmerExcelImplement

IdentityService --> ITokenService
InfobipEmailService --> IEmailService
FarmerExcelImplement --> IIdentityService

note right of PostGISGroupFormationService
  Uses native PostGIS functions:
  - ST_ClusterDBSCAN
  - ST_Distance
  - ST_Union
  - ST_Centroid
  - ST_Buffer
end note

note left of InfobipEmailService
  External API integration
  Tracks emails in database
  Handles batch sending
  Error handling & retry logic
end note

@enduml
```

---

## 11. API Layer - Controllers & DTOs

```plantuml
@startuml APILayer
!theme plain
title API Layer: Controllers, Requests & Responses

package "Controllers" {
  class ClusterManagerController {
    - _mediator: IMediator
    - _logger: ILogger
    --
    + GetClusterManagersPagingAndSearch(request: ClusterManagerListRequest): Task<ActionResult>
    + CreateClusterManager(command: CreateClusterManagerCommand): Task<IActionResult>
    + GetClusterManagerById(clusterManagerId: Guid): Task<IActionResult>
    + GetClusterIdByManagerId(clusterManagerId: Guid): Task<IActionResult>
  }
  
  class SupervisorController {
    - _mediator: IMediator
    - _logger: ILogger
    --
    + GetAllSupervisorOfAClusterPaging(request: SupervisorListRequest): Task<ActionResult>
    + CreateSupervisorCommand(command: CreateSupervisorCommand): Task<IActionResult>
    + GetMyPolygonTasks(status: string?): Task<ActionResult>
    + GetGroupBySeason(seasonId: Guid?, year: int?): Task<ActionResult>
  }
  
  class FarmerController {
    - _mediator: IMediator
    - _logger: ILogger
    - _currentUser: IUser
    --
    + GetFarmerById(id: Guid): Task<ActionResult>
    + GetFarmerDetailById(id: Guid): Task<ActionResult>
    + GetAllFarmers(pageNumber: int, pageSize: int, searchTerm: string?, clusterManagerId: Guid?): Task<ActionResult>
    + ImportFarmers(requestModel: FileUploadRequest): Task<IActionResult>
    + CreateFarmer(command: CreateFarmersCommand): Task<IActionResult>
  }
  
  class GroupController {
    - _mediator: IMediator
    - _currentUser: IUser
    --
    + GetGroupsByClusterIdPaging(request: GroupListRequest): Task<ActionResult>
    + GetGroupDetail(id: Guid): Task<IActionResult>
    + GetAllGroups(): Task<IActionResult>
    + PreviewGroups(clusterId: Guid, seasonId: Guid, year: int, parameters...): Task<IActionResult>
    + FormGroups(request: FormGroupsRequest): Task<IActionResult>
    + CreateGroupManually(request: CreateGroupManuallyRequest): Task<IActionResult>
  }
  
  class PlotController {
    - _mediator: IMediator
    - _logger: ILogger
    - _currentUser: IUser
    --
    + GetPlotById(id: Guid): Task<ActionResult>
    + GetAllPlots(pageNumber: int, pageSize: int, searchTerm: string?, clusterManagerId: Guid?): Task<ActionResult>
    + CreatePlot(command: CreatePlotCommand): Task<IActionResult>
    + ImportPlotsFromExcel(excelFile: IFormFile, importDate: DateTime?): Task<IActionResult>
    + GetPlotsAwaitingPolygon(filters...): Task<IActionResult>
  }
  
  class LateFarmerRecordController {
    - _mediator: IMediator
    - _logger: ILogger
    --
    + CreateLateFarmerRecord(command: CreateLateFarmerRecordCommand): Task<ActionResult>
    + GetLateCountByFarmerId(farmerId: Guid): Task<ActionResult>
    + GetLateDetailByFarmerId(farmerId: Guid): Task<ActionResult>
    + GetLateCountByPlotId(plotId: Guid): Task<ActionResult>
    + GetLateFarmersInCluster(agronomyExpertId: Guid?, supervisorId: Guid?, filters...): Task<ActionResult>
    + GetLatePlotsInCluster(agronomyExpertId: Guid?, supervisorId: Guid?, filters...): Task<ActionResult>
  }
  
  class ClusterController {
    - _mediator: IMediator
    - _logger: ILogger
    --
    + CreateCluster(command: CreateClusterCommand): Task<IActionResult>
    + GetAllClusters(request: ClusterListRequest): Task<ActionResult>
    + GetClusterHistory(clusterId: Guid, seasonId: Guid?, year: int?, limit: int?): Task<ActionResult>
    + GetClusterCurrentSeason(clusterId: Guid): Task<ActionResult>
    + GetClusterAvailableSeasons(clusterId: Guid, includeEmpty: bool, limit: int?): Task<ActionResult>
  }
}

package "Request DTOs" {
  class ClusterManagerListRequest {
    + CurrentPage: int
    + PageSize: int
    + Search: string?
    + PhoneNumber: string?
    + FreeOrAssigned: bool?
  }
  
  class SupervisorListRequest {
    + SearchNameOrEmail: string?
    + SearchPhoneNumber: string?
    + CurrentPage: int
    + PageSize: int
  }
  
  class GroupListRequest {
    + CurrentPage: int
    + PageSize: int
  }
  
  class FormGroupsRequest {
    + ClusterId: Guid
    + SeasonId: Guid
    + Year: int
    + Parameters: GroupFormationParametersDto?
    + AutoAssignSupervisors: bool
    + CreateGroupsImmediately: bool
  }
  
  class CreateGroupManuallyRequest {
    + ClusterId: Guid
    + SupervisorId: Guid
    + RiceVarietyId: Guid
    + SeasonId: Guid
    + Year: int
    + PlantingDate: DateTime
    + PlotIds: List<Guid>
    + IsException: bool
    + ExceptionReason: string?
  }
}

package "Response DTOs" {
  class SupervisorResponse {
    + Id: Guid
    + FullName: string
    + Email: string
    + PhoneNumber: string
    + ClusterId: Guid?
    + ClusterName: string?
    + IsActive: bool
    + CreatedAt: DateTime
  }
  
  class FarmerDTO {
    + Id: Guid
    + FullName: string
    + PhoneNumber: string
    + Email: string?
    + Address: string
    + FarmCode: string
    + ClusterId: Guid?
    + ClusterName: string?
    + NumberOfPlots: int
    + IsActive: bool
  }
  
  class FarmerDetailDTO {
    + Id: Guid
    + FullName: string
    + PhoneNumber: string
    + Email: string?
    + Address: string
    + FarmCode: string
    + ClusterId: Guid?
    + ClusterName: string?
    + Plots: List<PlotDTO>
    + CultivationHistory: List<CultivationHistoryDTO>
    + PerformanceMetrics: PerformanceMetricsDTO
  }
  
  class GroupResponse {
    + Id: Guid
    + ClusterId: Guid
    + ClusterName: string
    + SupervisorId: Guid?
    + SupervisorName: string?
    + RiceVarietyId: Guid
    + RiceVarietyName: string
    + SeasonName: string
    + Year: int
    + PlantingDate: DateTime
    + PlotCount: int
    + TotalArea: decimal
    + Status: string
    + CreatedAt: DateTime
  }
  
  class GroupDetailResponse {
    + Id: Guid
    + GroupName: string?
    + ClusterInfo: ClusterInfoDTO
    + SupervisorInfo: SupervisorInfoDTO?
    + RiceVarietyInfo: RiceVarietyInfoDTO
    + SeasonInfo: SeasonInfoDTO
    + Plots: List<PlotWithFarmerDTO>
    + ProductionPlans: List<ProductionPlanSummaryDTO>
    + Statistics: GroupStatisticsDTO
    + BoundaryGeoJson: string?
  }
  
  class FormGroupsResponse {
    + TotalGroupsCreated: int
    + TotalPlotsGrouped: int
    + TotalUngroupedPlots: int
    + TotalArea: decimal
    + Groups: List<GroupSummaryDTO>
    + UngroupedPlots: List<UngroupedPlotSummaryDTO>
    + Statistics: FormationStatisticsDTO
  }
  
  class PreviewGroupsResponse {
    + ProposedGroups: List<ProposedGroupDTO>
    + UngroupedPlots: List<UngroupedPlotDTO>
    + Statistics: PreviewStatisticsDTO
    + Recommendations: List<string>
  }
}

ClusterManagerController --> ClusterManagerListRequest
SupervisorController --> SupervisorListRequest
SupervisorController --> SupervisorResponse
GroupController --> GroupListRequest
GroupController --> FormGroupsRequest
GroupController --> GroupResponse
GroupController --> GroupDetailResponse
GroupController --> FormGroupsResponse
GroupController --> PreviewGroupsResponse
FarmerController --> FarmerDTO
FarmerController --> FarmerDetailDTO

note right of ClusterManagerController
  All controllers use MediatR
  Send commands/queries to handlers
  Return standardized Result<T>
end note

note left of FormGroupsResponse
  Contains complete information:
  - Created groups
  - Ungrouped plots with reasons
  - Statistics and metrics
  - Recommendations
end note

@enduml
```

---

## 12. Complete System Architecture Overview

```plantuml
@startuml SystemArchitecture
!theme plain
title Complete System Architecture - Clean Architecture with CQRS

package "Presentation Layer (API)" {
  [Controllers] as Controllers
  [Middleware] as Middleware
  [Filters] as Filters
}

package "Application Layer" {
  [Commands] as Commands
  [Queries] as Queries
  [Command Handlers] as CHandlers
  [Query Handlers] as QHandlers
  [DTOs] as DTOs
  [Validators] as Validators
  [Mappers] as Mappers
  [Domain Events] as Events
}

package "Domain Layer" {
  [Entities] as Entities
  [Value Objects] as ValueObjects
  [Domain Services] as DomainServices
  [Specifications] as Specs
  [Interfaces] as DomainInterfaces
}

package "Infrastructure Layer" {
  [Repositories] as Repos
  [DbContext] as DbContext
  [Identity Services] as Identity
  [Email Services] as Email
  [SMS Services] as SMS
  [Excel Services] as Excel
  [PostGIS Services] as PostGIS
  [External APIs] as APIs
}

database "PostgreSQL\n+ PostGIS" as DB

cloud "External Services" {
  [Infobip API] as Infobip
  [Email Provider] as EmailProvider
}

Controllers --> Middleware
Controllers --> Commands
Controllers --> Queries

Commands --> CHandlers
Queries --> QHandlers

CHandlers --> DomainServices
CHandlers --> Repos
CHandlers --> Identity
CHandlers --> Events

QHandlers --> Repos
QHandlers --> Mappers

Repos --> DbContext
Identity --> DbContext
PostGIS --> DbContext

DbContext --> DB

Email --> Infobip
SMS --> Infobib
Excel --> Repos

CHandlers --> DTOs
QHandlers --> DTOs

Entities ..> DomainInterfaces
CHandlers ..> Entities
Repos ..> Entities

note right of Controllers
  RESTful API endpoints
  JWT Authentication
  Request validation
  Response formatting
end note

note right of Commands
  CQRS Pattern
  Commands: Mutate state
  Queries: Read data
  Handlers implement logic
end note

note left of Repos
  Repository Pattern
  Unit of Work
  Generic & Specialized repos
end note

note bottom of DB
  PostgreSQL with PostGIS
  Spatial indexing
  Advanced geospatial queries
  DBSCAN clustering support
end note

@enduml
```

---

## Summary

This document provides comprehensive class diagrams covering all aspects of the SRPW-AI-BE system that support the 20 Cluster Manager use cases:

### Domain Models (Diagrams 1-5):
1. **User Management & Authentication** - Identity, roles, tokens
2. **Cluster & Season Management** - Cluster organization, season tracking
3. **Plot & Cultivation** - Plot management, cultivation tracking
4. **Group Formation** - Automatic & manual grouping, spatial clustering
5. **Production & Tasks** - Plans, tasks, late tracking

### Application Layer (Diagrams 6-8):
6. **Basic CQRS Commands** - UC-CM01 to UC-CM09
7. **Group Management CQRS** - UC-CM10 to UC-CM17  
8. **Plot & Performance CQRS** - UC-CM18 to UC-CM20

### Infrastructure Layer (Diagrams 9-10):
9. **Repository Pattern** - Data access abstraction
10. **Services** - PostGIS, Identity, Email, SMS, Excel

### API Layer (Diagram 11):
11. **Controllers & DTOs** - Request/Response models

### Complete Overview (Diagram 12):
12. **System Architecture** - Clean Architecture visualization

Each diagram shows:
- Class properties and methods
- Relationships (inheritance, composition, association)
- Key interfaces and implementations
- Enumerations
- Design patterns applied
- Notes explaining important concepts

---

*Generated for SRPW-AI-BE Project*  
*Class Diagrams for Cluster Manager Use Cases*  
*Date: December 14, 2025*

