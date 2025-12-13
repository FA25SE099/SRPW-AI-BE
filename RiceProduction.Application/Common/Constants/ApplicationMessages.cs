namespace RiceProduction.Application.Common.Constants;

public static class ApplicationMessages
{
    public static class Authentication
    {
        public const string UserAuthenticatedSuccessfully = "User authenticated successfully";
        public const string TokenRefreshedSuccessfully = "Token refreshed successfully";
        public const string UserLoggedOutSuccessfully = "User logged out successfully";
        public const string CurrentUserIdNotFound = "Current user ID not found.";
        public const string CurrentExpertUserIdNotFound = "Current expert user ID not found.";
        public const string UnauthorizedAccess = "Unauthorized access";
        public const string ClusterManagerIdRequired = "Cluster Manager ID is required.";
        public const string SupervisorIdRequired = "Supervisor ID is required.";
        public const string UavVendorIdRequired = "UAV Vendor ID is required.";
    }

    public static class YearSeason
    {
        public const string CreatedSuccessfully = "YearSeason created successfully";
        public const string UpdatedSuccessfully = "YearSeason updated successfully";
        public const string DeletedSuccessfully = "YearSeason deleted successfully";
        public const string StatusUpdatedSuccessfully = "YearSeason status updated to {0}";
        public const string NotFound = "YearSeason not found";
        public const string SeasonNotFound = "Season not found";
        public const string ClusterNotFound = "Cluster not found";
        public const string RiceVarietyNotFound = "Rice variety not found";
        public const string AlreadyExists = "YearSeason already exists for {0} {1} in this cluster";
        public const string CannotDeleteWithGroups = "Cannot delete YearSeason that has associated groups";
        public const string FailedToCreate = "Failed to create YearSeason";
        public const string FailedToUpdate = "Failed to update YearSeason";
        public const string FailedToDelete = "Failed to delete YearSeason";
        public const string FailedToUpdateStatus = "Failed to update YearSeason status";
        public const string FailedToRetrieve = "Failed to retrieve YearSeasons";
        public const string FailedToRetrieveDetail = "Failed to retrieve YearSeason detail";
    }

    public static class LateFarmerRecord
    {
        public const string CreatedSuccessfully = "Late record created successfully";
        public const string DetailRetrievedSuccessfully = "Late detail retrieved successfully";
        public const string CountRetrievedSuccessfully = "Late count retrieved successfully";
        public const string AgronomyExpertNotFound = "Agronomy Expert not found";
        public const string AgronomyExpertNotAssigned = "Agronomy Expert is not assigned to any cluster";
        public const string SupervisorNotFound = "Supervisor not found";
        public const string SupervisorNotAssigned = "Supervisor is not assigned to any cluster";
        public const string UserIdRequired = "Either AgronomyExpertId or SupervisorId must be provided";
        public const string ClusterIdNotFound = "Cluster ID not found for the specified user";
        public const string FarmerNotFound = "Farmer with ID {0} not found";
        public const string PlotNotFound = "Plot with ID {0} not found";
        public const string CultivationTaskNotFound = "Cultivation task with ID {0} not found";
        public const string PlotCultivationNotFound = "Plot cultivation not found for cultivation task {0}";
        public const string NoActiveGroup = "No active group found for plot {0}";
        public const string ProcessingError = "An error occurred while processing your request";
    }

    public static class Group
    {
        public const string FormedSuccessfully = "Groups formed successfully with {0} groups";
        public const string PreviewGenerated = "Groups preview generated successfully";
        public const string ClusterNotFound = "Cluster {0} not found";
        public const string SeasonNotFound = "Season {0} not found";
        public const string YearSeasonNotFound = "YearSeason not found for this cluster and season";
        public const string NoPlotsFound = "No plots found for grouping in this cluster and season";
        public const string FormationError = "Error forming groups: {0}";
        public const string PreviewError = "Error previewing groups: {0}";
        public const string NotFound = "Group not found";
        public const string NotFoundWithId = "Group not found.";
    }

    public static class ProductionPlan
    {
        public const string ApprovedSuccessfully = "Production Plan '{0}' successfully {1}";
        public const string CreatedSuccessfully = "Production plan created successfully";
        public const string UpdatedSuccessfully = "Production plan updated successfully";
        public const string DeletedSuccessfully = "Production plan deleted successfully";
        public const string StatusUpdated = "Plan status updated successfully";
        public const string NotFound = "Production Plan with ID {0} not found.";
        public const string InvalidStatus = "Plan is currently in status '{0}'. Only PendingApproval plans can be approved or rejected.";
        public const string ApprovalFailed = "An error occurred during approval/rejection process.";
        public const string GroupNotFound = "Group not found";
        public const string ExpertNotFound = "Expert not found";
        public const string InvalidStageConfiguration = "Invalid stage configuration";
        public const string CannotDelete = "Cannot delete production plan that is already in use";
    }

    public static class Uav
    {
        public const string OrderCreatedSuccessfully = "UAV order created successfully";
        public const string OrderUpdatedSuccessfully = "UAV order updated successfully";
        public const string ReportSubmittedSuccessfully = "Report for Plot {0} submitted successfully";
        public const string PlotsRetrievedSuccessfully = "Successfully retrieved {0} plots from group";
        public const string ServiceCompletedSuccessfully = "Service order completed successfully";
        public const string GroupNotFound = "Group not found.";
        public const string NoPlotsInGroup = "No plots found in Group {0}";
        public const string OrderNotFound = "UAV Service Order not found or unauthorized.";
        public const string PlotNotAssigned = "Plot {0} is not assigned to Order {1}.";
        public const string AssignmentCompleted = "This plot assignment is already completed.";
        public const string ReportSubmissionFailed = "Failed to submit service report.";
        public const string FailedToRetrievePlots = "Failed to retrieve ready plots.";
        public const string FailedToCreateOrder = "Failed to create UAV order";
        public const string FailedToUpdateOrder = "Failed to update UAV order";
        public const string VendorNotFound = "UAV Vendor not found";
    }

    public static class UavVendor
    {
        public const string RetrievedSuccessfully = "UAV vendors retrieved successfully";
    }

    public static class Plot
    {
        public const string CreatedSuccessfully = "Plot created successfully";
        public const string UpdatedSuccessfully = "Plot updated successfully";
        public const string DeletedSuccessfully = "Plot deleted successfully";
        public const string ImportedSuccessfully = "Plots imported successfully from Excel";
        public const string ImportSummary = "{0} plots imported successfully, {1} errors";
        public const string NotFound = "Plot not found";
        public const string NotFoundWithId = "Plot with ID {0} not found";
        public const string CodeAlreadyExists = "Plot with code {0} already exists";
        public const string FarmerNotFound = "Farmer not found";
        public const string InvalidExcelFormat = "Invalid Excel file format";
        public const string ExcelFileEmpty = "Excel file is empty";
        public const string MissingColumns = "Missing required columns: {0}";
        public const string InvalidRowData = "Invalid data in row {0}: {1}";
        public const string CannotDeleteActive = "Cannot delete plot that has active cultivation";
    }

    public static class Farmer
    {
        public const string CreatedSuccessfully = "Farmer created successfully";
        public const string UpdatedSuccessfully = "Farmer updated successfully";
        public const string DeletedSuccessfully = "Farmer deleted successfully";
        public const string ImportedSuccessfully = "Farmers imported successfully";
        public const string ProfileRetrievedSuccessfully = "Farmer profile retrieved successfully";
        public const string NotFound = "Farmer not found";
        public const string NotFoundWithId = "Farmer with ID {0} not found";
        public const string PhoneAlreadyExists = "Farmer with phone {0} already exists";
        public const string SupervisorNotFound = "Supervisor not found";
        public const string CannotDeleteWithPlots = "Cannot delete farmer with active plots";
        public const string InvalidData = "Invalid farmer data";
    }

    public static class Cluster
    {
        public const string CreatedSuccessfully = "Cluster created successfully";
        public const string UpdatedSuccessfully = "Cluster updated successfully";
        public const string DeletedSuccessfully = "Cluster deleted successfully";
        public const string DetailsRetrievedSuccessfully = "Cluster details retrieved successfully";
        public const string NotFound = "Cluster not found";
        public const string NameAlreadyExists = "Cluster name already exists";
        public const string CannotDeleteWithAssignments = "Cannot delete cluster with assigned managers or farmers";
    }

    public static class ClusterManager
    {
        public const string CreatedSuccessfully = "Cluster Manager created successfully";
        public const string UpdatedSuccessfully = "Cluster Manager updated successfully";
        public const string DeletedSuccessfully = "Cluster Manager deleted successfully";
        public const string AssignedSuccessfully = "Cluster Manager assigned to cluster successfully";
        public const string NotFound = "Cluster Manager not found";
        public const string AlreadyAssigned = "Cluster Manager already assigned to a cluster";
        public const string CannotDeleteWithAssignments = "Cannot delete Cluster Manager with active assignments";
    }

    public static class AgronomyExpert
    {
        public const string CreatedSuccessfully = "Agronomy Expert created successfully";
        public const string UpdatedSuccessfully = "Agronomy Expert updated successfully";
        public const string DeletedSuccessfully = "Agronomy Expert deleted successfully";
        public const string NotFound = "Agronomy Expert not found";
        public const string AlreadyAssigned = "Agronomy Expert already assigned to a cluster";
    }

    public static class Supervisor
    {
        public const string CreatedSuccessfully = "Supervisor created successfully";
        public const string UpdatedSuccessfully = "Supervisor updated successfully";
        public const string DeletedSuccessfully = "Supervisor deleted successfully";
        public const string FarmersAssignedSuccessfully = "Farmers assigned to supervisor successfully";
        public const string NotFound = "Supervisor not found";
        public const string CannotDeleteWithFarmers = "Cannot delete supervisor with assigned farmers";
        public const string NotAssignedToCluster = "Supervisor is not assigned to any cluster";
    }

    public static class Cultivation
    {
        public const string StartedSuccessfully = "Cultivation started successfully";
        public const string UpdatedSuccessfully = "Cultivation updated successfully";
        public const string TaskCompletedSuccessfully = "Cultivation task completed successfully";
        public const string PlotCultivationNotFound = "Plot cultivation not found";
        public const string PlotAlreadyUnderCultivation = "Plot is already under cultivation";
        public const string TaskNotFound = "Task not found";
        public const string TaskNotAssigned = "Task is not assigned to this farmer";
        public const string CannotUpdateCompletedTask = "Cannot update completed task";
    }

    public static class Material
    {
        public const string CreatedSuccessfully = "Material created successfully";
        public const string UpdatedSuccessfully = "Material updated successfully";
        public const string DeletedSuccessfully = "Material deleted successfully";
        public const string NotFound = "Material not found";
        public const string NameAlreadyExists = "Material with name {0} already exists";
        public const string CannotDeleteUsedInPlans = "Cannot delete material that is used in plans";
    }

    public static class RiceVariety
    {
        public const string CreatedSuccessfully = "Rice variety created successfully";
        public const string UpdatedSuccessfully = "Rice variety updated successfully";
        public const string DeletedSuccessfully = "Rice variety deleted successfully";
        public const string NotFound = "Rice variety not found";
        public const string NameAlreadyExists = "Rice variety with name {0} already exists";
        public const string CannotDeleteUsedInSeasons = "Cannot delete rice variety that is used in seasons";
    }

    public static class Season
    {
        public const string CreatedSuccessfully = "Season created successfully";
        public const string UpdatedSuccessfully = "Season updated successfully";
        public const string DeletedSuccessfully = "Season deleted successfully";
        public const string NotFound = "Season not found";
        public const string NameAlreadyExists = "Season with name {0} already exists";
        public const string CannotDeleteWithYearSeasons = "Cannot delete season with active year-seasons";
    }

    public static class Report
    {
        public const string EmergencyReportCreatedSuccessfully = "Emergency report created successfully";
        public const string EmergencyPlanCreatedSuccessfully = "Emergency plan created successfully for plot";
        public const string StatusUpdatedSuccessfully = "Report status updated successfully";
        public const string PlotCultivationNotFound = "Plot cultivation not found";
        public const string GroupNotFound = "Group not found";
        public const string ClusterNotFound = "Cluster not found";
        public const string AlertTypeRequired = "Alert type is required";
        public const string FailedToUploadImages = "Failed to upload images";
        public const string EmergencyProtocolNotFound = "Emergency protocol not found";
    }

    public static class StandardPlan
    {
        public const string CreatedSuccessfully = "Standard plan created successfully";
        public const string UpdatedSuccessfully = "Standard plan updated successfully";
        public const string DeletedSuccessfully = "Standard plan deleted successfully";
        public const string NotFound = "Standard plan not found";
        public const string CannotDeleteInUse = "Cannot delete standard plan that is being used";
        public const string InvalidStageConfiguration = "Invalid stage configuration";
    }

    public static class Notification
    {
        public const string SentSuccessfully = "Notification sent successfully";
        public const string MarkedAsRead = "Notifications marked as read";
        public const string FailedToSend = "Failed to send notification";
        public const string NotFound = "Notification not found";
    }

    public static class Protocol
    {
        public const string WeatherProtocolCreatedSuccessfully = "Weather protocol created successfully";
        public const string PestProtocolCreatedSuccessfully = "Pest protocol created successfully";
        public const string ProtocolUpdatedSuccessfully = "Protocol updated successfully";
        public const string WeatherProtocolNotFound = "Weather protocol not found";
        public const string PestProtocolNotFound = "Pest protocol not found";
        public const string ProtocolAlreadyExists = "Protocol already exists for this condition";
    }

    public static class Filter
    {
        public const string EnumValuesRetrievedSuccessfully = "Enum values retrieved successfully";
        public const string UnknownEnumType = "Unknown enum type: {0}. Supported types: PlotStatus, TaskStatus, TaskType, TaskPriority, AlertStatus, AlertSeverity, MaterialType, CultivationStatus";
    }

    public static class General
    {
        public const string ProcessingError = "An error occurred while processing your request";
        public const string InvalidRequestParameters = "Invalid request parameters";
        public const string ValidationFailed = "Validation failed";
        public const string DatabaseOperationFailed = "Database operation failed";
        public const string FileUploadFailed = "File upload failed";
        public const string InvalidFileFormat = "Invalid file format";
        public const string OperationNotPermitted = "Operation not permitted";
        public const string SuccessfullyRetrieved = "Successfully retrieved {0}";
        public const string FailedToRetrieve = "Failed to retrieve {0}";
    }
}

