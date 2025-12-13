namespace RiceProduction.Application.Common.Constants;

public static class ErrorCodes
{
    // Authentication & Authorization
    public const string AuthenticationRequired = "AuthenticationRequired";
    public const string Unauthorized = "Unauthorized";

    // General
    public const string NotFound = "NotFound";
    public const string ValidationFailed = "ValidationFailed";
    public const string OperationFailed = "OperationFailed";
    public const string InvalidRequest = "InvalidRequest";

    // Status
    public const string InvalidStatus = "InvalidStatus";
    public const string InvalidEnumType = "InvalidEnumType";

    // Production Plan
    public const string PlanNotFound = "PlanNotFound";
    public const string ApprovalRejectedFailed = "ApprovalRejectedFailed";

    // UAV
    public const string OrderNotFound = "OrderNotFound";
    public const string PlotNotAssigned = "PlotNotAssigned";
    public const string AssignmentCompleted = "AssignmentCompleted";
    public const string GetReadyPlotsFailed = "GetReadyPlotsFailed";
    public const string ReportCompletionFailed = "ReportCompletionFailed";

    // Group
    public const string GroupNotFound = "GroupNotFound";

    // Database
    public const string DatabaseError = "DatabaseError";
    public const string ConcurrencyError = "ConcurrencyError";

    // File Operations
    public const string FileUploadFailed = "FileUploadFailed";
    public const string InvalidFileFormat = "InvalidFileFormat";

    // Business Logic
    public const string AlreadyExists = "AlreadyExists";
    public const string CannotDelete = "CannotDelete";
    public const string AlreadyAssigned = "AlreadyAssigned";
    public const string NotAssigned = "NotAssigned";
}

