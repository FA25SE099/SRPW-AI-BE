# `IApplicationDbContext` Full Adoption Plan

**Current Progress:** **100% Complete** — All QueryHandlers, CommandHandlers, EventHandlers, Helpers, Unit Tests, Controllers, and Infrastructure Services migrated to `IApplicationDbContext` / `TestApplicationDbContext`; `IUnitOfWork`, `IGenericRepository<T>`, and all 10 custom repositories retired.  
**Additional Unlisted Files Migrated:** `CultivationFeature/Event/ProductionPlanApprovalEventHandler.cs`, `RiceProduction.API/Controllers/FarmerCultivationController.cs`, `RiceProduction.Infrastructure/Implementation/Email/EmailService.cs`, `RiceProduction.Infrastructure/Services/NotificationService.cs`.

---

## 1. Current State & Target Architecture

| Layer / Category | Migrated | Remaining | Notes |
|---|---:|---:|---|
| Read-Only QueryHandlers (`*QueryHandler*` / `*QueriesHandler*`) | 87 | **46** | Includes 3 with Moq unit tests, 1 dead commented file (`PreviewGroupsPostGISQueryHandler`), and 1 mutating query (`GetPlotsAwaitingPolygonQueryHandler`) |
| Shared Application Helpers | — | **2** | `MaterialCostHelper.cs`, `PlotImportHelper.cs` |
| Domain / MediatR Event Handlers | — | **2** | `MaterialDistributionCreationEventHandler.cs`, `PlotImportedEventHandler.cs` |
| Command Handlers (no Moq test) | 0 | **59** | Straightforward vertical-slice migrations |
| Command Handlers (with Moq test) | 0 | **15** | Migrate alongside converting their tests to `TestApplicationDbContext` |
| Unit Test Files using `Mock<IUnitOfWork>` | 0 | **18** | 3 Query tests + 15 Command tests |
| Infrastructure & Interface Files to Retire | 0 | **21** | 11 Infrastructure classes + 10 Application interfaces (retire last) |

---

## 2. Standard Migration Pattern

When migrating any handler from `IUnitOfWork` → `IApplicationDbContext`:

1. **Constructor**: Replace `IUnitOfWork _unitOfWork` with `IApplicationDbContext _context`.
2. **Read Queries**:
   - Replace `_unitOfWork.Repository<T>().GetQueryable()` / `ListAsync(...)` / `FindAsync(...)` with `_context.<DbSet>.AsNoTracking()...`
   - Add `.AsSplitQuery()` whenever `.Include(...)` loads **2+ collection navigations**.
   - Prefer `.Select(...)`, `.AnyAsync(...)`, `.CountAsync(...)`, `.ToDictionaryAsync(...)` over loading full entities (especially avoiding PostGIS `Polygon`/`Point` columns when unused).
   - Propagate `cancellationToken` to every EF Core async method.
3. **Write Commands**:
   - Replace `_unitOfWork.Repository<T>().AddAsync(...)` / `AddRangeAsync(...)` / `Update(...)` / `Delete(...)` with `_context.<DbSet>.Add(...)` / `AddRange(...)` / `Update(...)` / `Remove(...)`.
   - Replace `_unitOfWork.SaveChangesAsync(cancellationToken)` / `CompleteAsync()` with `await _context.SaveChangesAsync(cancellationToken)`.
   - Replace `GenerateNewGuid(...)` calls with `Guid.NewGuid()` (or let EF Core `ValueGeneratedOnAdd` generate IDs).
4. **Custom Repositories**: Replace custom repository methods (`FarmerRepository`, `PlotRepository`, `SupervisorRepository`, `ClusterRepository`, `ClusterManagerRepository`, `AgronomyExpertRepository`, `UavVendorRepository`, `LateFarmerRecordRepository`) with direct `_context.<DbSet>` LINQ queries (or extract small reusable `IQueryable<T>` extension methods if a filter/projection is shared by 3+ handlers).
5. **Unit Tests**: Replace `Mock<IUnitOfWork>` + `Mock<IGenericRepository<T>>` with `await using var context = TestApplicationDbContext.Create();` (see `RiceProduction.Tests/Unit/Application/YearSeasonFeature/YearSeasonDbContextQueryHandlerTests.cs`).

---

## 3. Phased Checklist of All Remaining Items

### Phase 1 — Finish All Remaining QueryHandlers (46 files) + `MaterialCostHelper`

#### 1A. QueryHandlers without Moq Unit Tests (42 files)
**AgronomyExpertFeature (3)**
- [x] `RiceProduction.Application/AgronomyExpertFeature/Queries/GetAgronomyExpertById/GetAgronomyExpertByIdQueryHandler.cs`
- [x] `RiceProduction.Application/AgronomyExpertFeature/Queries/GetAgronomyExpertList/GetAgronomyExpertsQueryHandler.cs`
- [x] `RiceProduction.Application/AgronomyExpertFeature/Queries/GetCurrentAgronomyExpert/GetCurrentAgronomyExpertQueryHandler.cs`

**ClusterFeature & ClusterManagerFeature (4)**
- [x] `RiceProduction.Application/ClusterFeature/Queries/GetAllClustersPaging/GetAllClustersQueryHandler.cs`
- [x] `RiceProduction.Application/ClusterManagerFeature/Queries/GetClusterIdByManagerId/GetClusterIdByManagerIdQueryHandler.cs`
- [x] `RiceProduction.Application/ClusterManagerFeature/Queries/GetClusterManagerById/GetClusterManagerByIdQueryHandler.cs`
- [x] `RiceProduction.Application/ClusterManagerFeature/Queries/GetClusterManagerList/GetClusterManagersQueryHandler.cs`

**FarmerFeature (7)**
- [x] `RiceProduction.Application/FarmerFeature/Queries/ExportFarmerExcel/DownloadAllFarmerExcelQueryHandler.cs`
- [x] `RiceProduction.Application/FarmerFeature/Queries/GetFarmerCultivationSelections/GetFarmerCultivationSelectionsQueryHandler.cs`
- [x] `RiceProduction.Application/FarmerFeature/Queries/GetFarmersForAdmin/GetFarmersForAdminQueryHandler.cs`
- [x] `RiceProduction.Application/FarmerFeature/Queries/ValidateCultivationPreferences/ValidateCultivationPreferencesQueryHandler.cs`
- [x] `RiceProduction.Application/FarmerFeature/Queries/GetFarmer/GetAll/GetAllFarmerQueriesHandler.cs`
- [x] `RiceProduction.Application/FarmerFeature/Queries/GetFarmer/GetById/GetFarmerByIdQueriesHandler.cs`
- [x] `RiceProduction.Application/FarmerFeature/Queries/GetFarmer/GetDetailById/GetFarmerDetailQueriesHandler.cs`

**GroupFeature (2)**
- [x] `RiceProduction.Application/GroupFeature/Queries/GetGroupsByClusterId/GetGroupsByClusterManagerIdQueryHandler.cs`
- [x] `RiceProduction.Application/GroupFeature/Queries/PreviewGroups/PreviewGroupsPostGISQueryHandler.cs` *(100% commented out — delete or revive)*

**LateFarmerRecordFeature (5)**
- [x] `RiceProduction.Application/LateFarmerRecordFeature/Queries/GetLateCountByFarmerId/GetLateCountByFarmerIdQueryHandler.cs`
- [x] `RiceProduction.Application/LateFarmerRecordFeature/Queries/GetLateCountByPlotId/GetLateCountByPlotIdQueryHandler.cs`
- [x] `RiceProduction.Application/LateFarmerRecordFeature/Queries/GetLateDetailByFarmerId/GetLateDetailByFarmerIdQueryHandler.cs`
- [x] `RiceProduction.Application/LateFarmerRecordFeature/Queries/GetLateFarmersInCluster/GetLateFarmersInClusterQueryHandler.cs`
- [x] `RiceProduction.Application/LateFarmerRecordFeature/Queries/GetLatePlotsInCluster/GetLatePlotsInClusterQueryHandler.cs`

**MaterialFeature (6 + helper)**
- [x] `RiceProduction.Application/Common/Helpers/MaterialCostHelper.cs` *(migrate `GetCurrentPricesAsync` from `IUnitOfWork` → `IApplicationDbContext`)*
- [x] `RiceProduction.Application/MaterialFeature/Queries/CalculateGroupMaterialCost/CalculateGroupMaterialCostQueryHandler.cs`
- [x] `RiceProduction.Application/MaterialFeature/Queries/CalculateMaterialsCostByPlotId/CalculateMaterialsCostByPlotIdQueryHandler.cs`
- [x] `RiceProduction.Application/MaterialFeature/Queries/CalculatePrice/CalculateMaterialPriceQueryHandler.cs`
- [x] `RiceProduction.Application/MaterialFeature/Queries/CalculateStandardPlanMaterialCost/CalculateStandardPlanMaterialCostQueryHandler.cs`
- [x] `RiceProduction.Application/MaterialFeature/Queries/CalculateStandardPlanProfitAnalysis/CalculateStandardPlanProfitAnalysisQueryHandler.cs`
- [x] `RiceProduction.Application/MaterialFeature/Queries/DownloadAllMaterialExcel/DownloadAllMaterialExcelQueryHandler.cs`

**PlotFeature (6)**
- [x] `RiceProduction.Application/PlotFeature/Queries/GetAll/GetAllPlotQueriesHandler.cs`
- [x] `RiceProduction.Application/PlotFeature/Queries/GetByFarmerId/GetByFarmerIdQueryHandle.cs`
- [x] `RiceProduction.Application/PlotFeature/Queries/GetById/GetPlotByIDQueriesHandler.cs`
- [x] `RiceProduction.Application/PlotFeature/Queries/GetDetail/GetPlotDetailQueriesHandler.cs`
- [x] `RiceProduction.Application/PlotFeature/Queries/GetOutOfSeason/GetPlotOutSeasonQueriesHandler.cs`
- [x] `RiceProduction.Application/PlotFeature/Queries/GetPlotsAwaitingPolygon/GetPlotsAwaitingPolygonQueryHandler.cs` *(note: mutates overdue tasks & calls `SaveChangesAsync` — keep tracking on the overdue-task update query!)*

**ProductionPlanFeature (1)**
- [x] `RiceProduction.Application/ProductionPlanFeature/Queries/GetApproved/GetAppprovedQueriesHandler.cs`

**SupervisorFeature (6)**
- [x] `RiceProduction.Application/SupervisorFeature/Queries/GetAllSupervisor/GetAllSupervisorQueryHandlers.cs`
- [x] `RiceProduction.Application/SupervisorFeature/Queries/GetAllSupervisorForAdmin/GetAllSupervisorForAdminQueryHandler.cs`
- [x] `RiceProduction.Application/SupervisorFeature/Queries/GetAllSupervisorForClusterManager/GetAllSupervisorQueryHandler.cs`
- [x] `RiceProduction.Application/SupervisorFeature/Queries/GetFarmersBySupervisor/GetFarmersBySupervisorQueryHandler.cs`
- [x] `RiceProduction.Application/SupervisorFeature/Queries/GetSupervisorByClusterId/GetSupervisorByClusterIdQueryHandler.cs`
- [x] `RiceProduction.Application/SupervisorFeature/Queries/GetSupervisorProfile/GetSupervisorProfileQueryHandler.cs`

**UavVendorFeature (3)**
- [x] `RiceProduction.Application/UavVendorFeature/Queries/GetAllUavVendor/GetAllUavVendorQueryHandler.cs`
- [x] `RiceProduction.Application/UavVendorFeature/Queries/GetUavVendorById/GetUavVendorByIdQueryHandler.cs`
- [x] `RiceProduction.Application/UavVendorFeature/Queries/GetUavVendorList/GetUavVendorQueryHandler.cs`

#### 1B. QueryHandlers with Moq Unit Tests (3 handlers + 3 test files)
- [x] `RiceProduction.Application/MaterialFeature/Queries/CalculateMaterialsCostByArea/CalculateMaterialsCostByAreaQueryHandler.cs`
  - Test: `RiceProduction.Tests/Unit/Application/MaterialFeature/CalculateMaterialsCostByAreaQueryHandlerTests.cs`
- [x] `RiceProduction.Application/SupervisorFeature/Queries/ValidatePolygonArea/ValidatePolygonAreaQueryHandler.cs`
  - Test: `RiceProduction.Tests/Unit/Application/SupervisorFeature/ValidatePolygonAreaQueryHandlerTests.cs`
- [x] `RiceProduction.Application/UAVFeature/Queries/GetPlotsReadyForUav/GetPlotsReadyForUavQueryHandler.cs`
  - Test: `RiceProduction.Tests/Unit/Application/UAVFeature/GetPlotsReadyForUavQueryHandlerTests.cs`

---

### Phase 2 — Event Handlers & Shared Import Helper (3 files)
- [x] `RiceProduction.Application/Common/Helpers/PlotImportHelper.cs` *(shared by `ImportPlotByExcelCommandHandler` & `ImportPlotsFromDataCommandHandler`)*
- [x] `RiceProduction.Application/MaterialDistributionFeature/Events/MaterialDistributionCreationEventHandler.cs`
- [x] `RiceProduction.Application/PlotFeature/Events/PlotImportedEventHandler.cs` *(uses `SupervisorRepository`, `ClusterManagerRepository`)*

---

### Phase 3 — Command Handlers without Moq Unit Tests (59 files)

**AgronomyExpertFeature, ClusterFeature & ClusterManagerFeature (4)**
- [x] `RiceProduction.Application/AgronomyExpertFeature/Commands/CreateAgronomyExpert/CreateAgronomyExpertCommandHandler.cs`
- [x] `RiceProduction.Application/ClusterFeature/Commands/CreateCluster/CreateClusterCommandHandler.cs` *(uses `SupervisorRepository`, `ClusterManagerRepository`, `AgronomyExpertRepository`)*
- [x] `RiceProduction.Application/ClusterFeature/Commands/UpdateCluster/UpdateClusterCommandHandler.cs` *(uses `SupervisorRepository`, `ClusterManagerRepository`, `AgronomyExpertRepository`, `ClusterRepository`)*
- [x] `RiceProduction.Application/ClusterManagerFeature/Commands/CreateClusterManager/CreateClusterManagerCommandHandler.cs`

**EmailFeature, NotificationFeature, SmsFeature & SystemSettingFeature (4)**
- [x] `RiceProduction.Application/EmailFeature/Command/SendEmailCommandHandler.cs`
- [x] `RiceProduction.Application/NotificationFeature/Command/PushNotification/SendPushNotificationCommandHandler.cs`
- [x] `RiceProduction.Application/SmsFeature/Commands/ProcessSmsDeliveryWebhook/ProcessSmsDeliveryWebhookCommandHandler.cs`
- [x] `RiceProduction.Application/SystemSettingFeature/Commands/UpdateSystemSetting/UpdateSystemSettingCommandHandler.cs`

**EmergencyProtocolFeature, PestProtocolFeature & WeatherProtocolFeature (5)**
- [x] `RiceProduction.Application/EmergencyProtocolFeature/Commands/CreateEmergencyProtocol/CreateEmergencyProtocolCommandHandler.cs` *(uses `AgronomyExpertRepository`)*
- [x] `RiceProduction.Application/EmergencyProtocolFeature/Commands/UpdateEmergencyProtocol/UpdateEmergencyProtocolCommandHandler.cs`
- [x] `RiceProduction.Application/PestProtocolFeature/Commands/UpdatePestProtocol/UpdatePestProtocolCommandHandler.cs`
- [x] `RiceProduction.Application/WeatherProtocolFeature/Commands/CreateWeatherProtocol/CreateWeatherProtocolCommandHandler.cs`
- [x] `RiceProduction.Application/WeatherProtocolFeature/Commands/UpdateWeatherProtocol/UpdateWeatherProtocolCommandHandler.cs`

**FarmerFeature, FarmerCultivationTaskFeature & LateFarmerRecordFeature (6)**
- [x] `RiceProduction.Application/FarmerFeature/Command/CreateFarmer/CreateFarmersCommandHandler.cs` *(uses `ClusterManagerRepository`, `FarmerRepository`)*
- [x] `RiceProduction.Application/FarmerFeature/Command/UpdateFarmer/UpdateFarmerCommandHandler.cs` *(uses `FarmerRepository`)*
- [x] `RiceProduction.Application/FarmerFeature/Commands/ChangeFarmerStatus/ChangeFarmerStatusCommandHandler.cs` *(uses `FarmerRepository`)*
- [x] `RiceProduction.Application/FarmerFeature/Commands/SelectCultivationPreferences/SelectCultivationPreferencesCommandHandler.cs`
- [x] `RiceProduction.Application/FarmerCultivationTaskFeature/Commands/StartCultivationTask/StartCultivationTaskCommandHandler.cs`
- [x] `RiceProduction.Application/LateFarmerRecordFeature/Commands/CreateLateFarmerRecord/CreateLateFarmerRecordCommandHandler.cs` *(uses `LateFarmerRecordRepository`)*

**GroupFeature (3)**
- [x] `RiceProduction.Application/GroupFeature/Commands/CreateGroupManually/CreateGroupManuallyCommandHandler.cs` *(uses `SupervisorRepository`, `PlotRepository`)*
- [x] `RiceProduction.Application/GroupFeature/Commands/FormGroups/FormGroupsPostGISCommandHandler.cs` *(uses `SupervisorRepository`)*
- [x] `RiceProduction.Application/GroupFeature/Commands/FormGroupsFromPreview/FormGroupsFromPreviewCommandHandler.cs` *(uses `SupervisorRepository`, `PlotRepository`)*

**MaterialDistributionFeature & MaterialFeature (10)**
- [x] `RiceProduction.Application/MaterialDistributionFeature/Commands/BulkConfirmMaterialDistribution/BulkConfirmMaterialDistributionCommandHandler.cs`
- [x] `RiceProduction.Application/MaterialDistributionFeature/Commands/BulkConfirmMaterialReceipt/BulkConfirmMaterialReceiptCommandHandler.cs` *(uses `FarmerRepository`)*
- [x] `RiceProduction.Application/MaterialDistributionFeature/Commands/ConfirmMaterialDistribution/ConfirmMaterialDistributionCommandHandler.cs`
- [x] `RiceProduction.Application/MaterialDistributionFeature/Commands/ConfirmMaterialReceipt/ConfirmMaterialReceiptCommandHandler.cs` *(uses `FarmerRepository`)*
- [x] `RiceProduction.Application/MaterialDistributionFeature/Commands/InitiateMaterialDistribution/InitiateMaterialDistributionCommandHandler.cs` *(uses `FarmerRepository`)*
- [x] `RiceProduction.Application/MaterialFeature/Commands/CreateMaterial/CreateMaterialCommandHandler.cs`
- [x] `RiceProduction.Application/MaterialFeature/Commands/ImportCreateAllMaterialExcel/ImportCreateAllMaterialExcelCommandHandler.cs`
- [x] `RiceProduction.Application/MaterialFeature/Commands/ImportUpdateAllMaterialExcel/ImportUpdateAllMaterialExcelCommandHandler.cs`
- [x] `RiceProduction.Application/MaterialFeature/Commands/ImportUpsertMaterialExcel/ImportUpsertMaterialExcelCommandHandler.cs`
- [x] `RiceProduction.Application/MaterialFeature/Commands/UpdateMaterial/UpdateMaterialCommandHandler.cs`

**PlotFeature (5)**
- [x] `RiceProduction.Application/PlotFeature/Commands/CreatePlot/CreatePlotCommandHandler.cs` *(uses `FarmerRepository`)*
- [x] `RiceProduction.Application/PlotFeature/Commands/EditPlot/EditPlotCommandHandler.cs` *(uses `FarmerRepository`, `PlotRepository`)*
- [x] `RiceProduction.Application/PlotFeature/Commands/ImportFromData/ImportPlotsFromDataCommandHandler.cs` *(via `PlotImportHelper`)*
- [x] `RiceProduction.Application/PlotFeature/Commands/UpdateBoundaryExcel/UpdatePlotBoundaryCommandHandler.cs` *(uses `FarmerRepository`)*
- [x] `RiceProduction.Application/PlotFeature/Commands/UpdateCoordinate/UpdateCoordinateCommandHandler.cs`

**ProductionPlanFeature & ReportFeature (5)**
- [x] `RiceProduction.Application/ProductionPlanFeature/Commands/EditPlan/EditPlanCommandHandler.cs`
- [x] `RiceProduction.Application/ProductionPlanFeature/Commands/ResolveEmergencyPlan/ResolveEmergencyPlanCommandHandler.cs`
- [x] `RiceProduction.Application/ProductionPlanFeature/Commands/SubmitPlan/SubmitPlanCommandHandler.cs`
- [x] `RiceProduction.Application/ReportFeature/Command/CreateEmergencyPlanForPlotCommandHandler.cs`
- [x] `RiceProduction.Application/ReportFeature/Command/ResolveReportCommandHandler.cs`

**RiceVarietyFeature, RiceVarietySeasonFeature & SeasonFeature (8)**
- [x] `RiceProduction.Application/RiceVarietyFeature/Commands/ChangeRiceSeason/ChangeRiceSeasonCommandHandler.cs`
- [x] `RiceProduction.Application/RiceVarietyFeature/Commands/CreateRiceVariety/CreateRiceVarietyCommandHandler.cs`
- [x] `RiceProduction.Application/RiceVarietyFeature/Commands/UpdateRiceVariety/UpdateRiceVarietyCommandHandler.cs`
- [x] `RiceProduction.Application/RiceVarietySeasonFeature/Commands/CreateRiceVarietySeason/CreateRiceVarietySeasonCommandHandler.cs`
- [x] `RiceProduction.Application/RiceVarietySeasonFeature/Commands/DeleteRiceVarietySeason/DeleteRiceVarietySeasonCommandHandler.cs`
- [x] `RiceProduction.Application/RiceVarietySeasonFeature/Commands/UpdateRiceVarietySeason/UpdateRiceVarietySeasonCommandHandler.cs`
- [x] `RiceProduction.Application/SeasonFeature/Commands/DeleteSeason/DeleteSeasonCommandHandler.cs`
- [x] `RiceProduction.Application/SeasonFeature/Commands/UpdateSeason/UpdateSeasonCommandHandler.cs`

**SupervisorFeature, UAVFeature, UavVendorFeature & YearSeasonFeature (9)**
- [x] `RiceProduction.Application/SupervisorFeature/Commands/CompletePolygonAssignment/CompletePolygonAssignmentCommandHandler.cs`
- [x] `RiceProduction.Application/SupervisorFeature/Commands/CreateSupervisor/CreateSupervisorCommandHandler.cs`
- [x] `RiceProduction.Application/UAVFeature/Commands/ReportServiceOrder/ReportServiceOrderCompletionCommandHandler.cs`
- [x] `RiceProduction.Application/UavVendorFeature/Commands/CreateUavVendor/CreateUavVendorCommandHandler.cs`
- [x] `RiceProduction.Application/UavVendorFeature/Commands/UpdateUavVendor/UpdateUavVendorCommandHandler.cs` *(uses `UavVendorRepository`)*
- [x] `RiceProduction.Application/YearSeasonFeature/Commands/CreateYearSeason/CreateYearSeasonCommandHandler.cs` *(uses `AgronomyExpertRepository`)*
- [x] `RiceProduction.Application/YearSeasonFeature/Commands/DeleteYearSeason/DeleteYearSeasonCommandHandler.cs`
- [x] `RiceProduction.Application/YearSeasonFeature/Commands/UpdateYearSeason/UpdateYearSeasonCommandHandler.cs`
- [x] `RiceProduction.Application/YearSeasonFeature/Commands/UpdateYearSeasonStatus/UpdateYearSeasonStatusCommandHandler.cs`

---

### Phase 4 — Command Handlers with Moq Unit Tests (15 handlers + 15 test files)

Migrate each handler + convert its unit test suite from `Mock<IUnitOfWork>` to `TestApplicationDbContext` (EF Core InMemory):

- [x] `FarmerFeature/Command/CreateFarmer/CreateFarmerCommandHandler.cs` + `CreateFarmerCommandHandlerTests.cs`
- [x] `FarmLogFeature/Commands/CreateFarmLog/CreateFarmLogCommandHandler.cs` + `CreateFarmLogCommandHandlerTests.cs`
- [x] `GroupFeature/Commands/FormGroups/FormGroupsCommandHandler.cs` + `FormGroupsCommandHandlerTests.cs`
- [x] `MaterialFeature/Commands/DeleteMaterial/DeleteMaterialCommandHandler.cs` + `DeleteMaterialCommandHandlerTests.cs`
- [x] `PestProtocolFeature/Commands/CreatePestProtocol/CreatePestProtocolCommandHandler.cs` + `CreatePestProtocolCommandHandlerTests.cs`
- [x] `PlotFeature/Commands/CreatePlots/CreatePlotsCommandHandler.cs` + `CreatePlotsCommandHandlerTests.cs`
- [x] `PlotFeature/Commands/ImportExcel/ImportPlotByExcelCommandHandler.cs` + `ImportPlotByExcelCommandHandlerTests.cs`
- [x] `ProductionPlanFeature/Commands/ApproveRejectPlan/ApproveRejectPlanCommandHandler.cs` + `ApproveRejectPlanCommandHandlerTests.cs`
- [x] `ProductionPlanFeature/Commands/CreateProductionPlan/CreateProductionPlanCommandHandler.cs` + `CreateProductionPlanCommandHandlerTests.cs`
- [x] `ReportFeature/Command/CreateEmergencyReportCommandHandler.cs` + `CreateEmergencyReportCommandHandlerTests.cs`
- [x] `RiceVarietyFeature/Commands/DeleteRiceVariety/DeleteRiceVarietyCommandHandler.cs` + `DeleteRiceVarietyCommandHandlerTests.cs`
- [x] `SeasonFeature/Commands/CreateSeason/CreateSeasonCommandHandler.cs` + `CreateSeasonCommandHandlerTests.cs`
- [x] `StandardPlanFeature/Commands/CreateStandardPlan/CreateStandardPlanCommandHandler.cs` + `CreateStandardPlanCommandHandlerTests.cs`
- [x] `StandardPlanFeature/Commands/UpdateStandardPlan/UpdateStandardPlanCommandHandler.cs` + `UpdateStandardPlanCommandHandlerTests.cs`
- [x] `UAVFeature/Commands/CreateUavOrder/CreateUavOrderCommandHandler.cs` + `CreateUavOrderCommandHandlerTests.cs`

*(Note: for database-error tests like `Handle_DatabaseError_ReturnsFailure` that currently throw from `Mock<IUnitOfWork>.SaveChangesAsync`, either use a tiny `FailingSaveChangesDbContext` subclass of `TestApplicationDbContext` or a `Mock<IApplicationDbContext>` for that specific failure test.)*

---

### Phase 5 — Retire `IUnitOfWork`, `IGenericRepository<T>` & Custom Repositories (21 files + DI)

Only execute once workspace-wide search for `IUnitOfWork` and `IGenericRepository` returns **0** matches in `Application` and `Tests`:

- [x] Remove DI registrations in `RiceProduction.Infrastructure/DependencyInjection.cs`:
  - `AddScoped<IUnitOfWork, UnitOfWork.UnitOfWork>()`
  - `AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>))`
  - `AddScoped<IFarmerRepository, FarmerRepository>()`
- [x] Delete 10 Application interfaces (`RiceProduction.Application/Common/Interfaces/`):
  - `IUnitOfWork.cs`, `IGenericRepository.cs`, `IAgronomyExpertRepository.cs`, `IClusterManagerRepository.cs`, `IClusterRepository.cs`, `IFarmerRepository.cs`, `ILateFarmerRecordRepository.cs`, `IPlotRepository.cs`, `ISupervisorRepository.cs`, `IUavVendorRepository.cs`
- [x] Delete 11 Infrastructure implementations:
  - `RiceProduction.Infrastructure/UnitOfWork/UnitOfWork.cs`
  - `RiceProduction.Infrastructure/Repository/GenericRepository.cs`
  - `RiceProduction.Infrastructure/Repository/DecoratorGenericRepository.cs`
  - `RiceProduction.Infrastructure/Repository/AgronomyExpertRepository.cs`
  - `RiceProduction.Infrastructure/Repository/ClusterManagerRepository.cs`
  - `RiceProduction.Infrastructure/Repository/ClusterRepository.cs`
  - `RiceProduction.Infrastructure/Repository/FarmerRepository.cs`
  - `RiceProduction.Infrastructure/Repository/LateFarmerRecordRepository.cs`
  - `RiceProduction.Infrastructure/Repository/PlotRepository.cs`
  - `RiceProduction.Infrastructure/Repository/SupervisorRepository.cs`
  - `RiceProduction.Infrastructure/Repository/UavVendorRepository.cs`

---

## 4. Suggested Execution Order & Risks to Watch

| Order | Phase | Risk | Why |
|---:|---|---|---|
| 1 | **Phase 1A** (42 Read QueryHandlers) | Low | Read-only, no Moq tests; finishes 93% of CQRS read side |
| 2 | **Phase 2** (2 EventHandlers + `PlotImportHelper`) | Low–Medium | Unblocks `ImportPlot*` commands |
| 3 | **Phase 3** (59 CommandHandlers without Moq tests) | Medium | Group by Feature (1–2 features per PR); verify `SaveChangesAsync` & change tracking |
| 4 | **Phase 1B + Phase 4** (18 handlers + 18 unit test suites) | Medium | Replace fragile Moq setups with `TestApplicationDbContext` (note: spatial tests like `ValidatePolygonArea` need geometry properties not ignored or a helper mock) |
| 5 | **Phase 5** (Delete `UnitOfWork` + 10 Repositories) | Low | Clean deletion once 0 references remain; verify `dotnet build` & `dotnet test` |
