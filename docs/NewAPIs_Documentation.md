# New APIs Documentation

This document contains all 7 new/updated APIs created as per requirements.

**ALL ENDPOINTS USE POST WITH REQUEST BODY** for cleaner URLs and better filtering support.

---

## 1. Get Farm Logs by Production Plan Task ID
**Endpoint:** `POST /api/farmlog/farm-logs/by-production-plan-task`

**Description:** Retrieves all farm logs for a specific production plan task, sorted by logged date (newest first). This handles retrieving logs across all cultivation task versions for the same production plan task.

**Request Body:**
```json
{
  "productionPlanTaskId": "123e4567-e89b-12d3-a456-426614174000",
  "currentPage": 1,
  "pageSize": 20
}
```

**Request Body Parameters:**
- `ProductionPlanTaskId` (Guid, required): The production plan task ID
- `CurrentPage` (int, optional, default: 1): Page number
- `PageSize` (int, optional, default: 20): Items per page (max 100)

**Response:** `PagedResult<List<FarmLogDetailResponse>>`

**Example Request:**
```http
POST /api/farmlog/farm-logs/by-production-plan-task
Content-Type: application/json

{
  "productionPlanTaskId": "123e4567-e89b-12d3-a456-426614174000",
  "currentPage": 1,
  "pageSize": 20
}
```

**Example Response:**
```json
{
  "succeeded": true,
  "data": [
    {
      "farmLogId": "789...",
      "cultivationTaskName": "Fertilizer Application",
      "plotName": "Th?a 5, T? 3",
      "loggedDate": "2024-01-15T10:30:00Z",
      "workDescription": "Applied NPK fertilizer",
      "completionPercentage": 100,
      "actualAreaCovered": 2.5,
      "serviceCost": 500000,
      "serviceNotes": "Used drone service",
      "photoUrls": ["url1", "url2"],
      "weatherConditions": "Sunny, 28°C",
      "interruptionReason": null,
      "materialsUsed": [
        {
          "materialName": "NPK 16-16-8",
          "actualQuantityUsed": 50,
          "actualCost": 1200000,
          "notes": "Applied evenly"
        }
      ]
    }
  ],
  "currentPage": 1,
  "pageSize": 20,
  "totalCount": 45,
  "totalPages": 3,
  "message": "Successfully retrieved farm logs by production plan task."
}
```

**Notes:**
- This API retrieves logs across ALL versions of cultivation tasks for the same production plan task
- Useful when resolve report creates new versions and you need full history
- Logs are grouped by ProductionPlanTask to show task continuity

---

## 2. Get Farmers by Supervisor
**Endpoint:** `POST /api/supervisor/farmers`

**Description:** Retrieves farmers managed by a supervisor. Can return all farmers in the supervisor's cluster or only assigned farmers.

**Authorization:** Requires `Supervisor` role

**Request Body:**
```json
{
  "onlyAssigned": true,
  "currentPage": 1,
  "pageSize": 20,
  "searchTerm": "Nguyen"
}
```

**Request Body Parameters:**
- `OnlyAssigned` (bool, optional, default: false): 
  - `false` = all farmers in cluster
  - `true` = only assigned farmers
- `CurrentPage` (int, optional, default: 1)
- `PageSize` (int, optional, default: 20, max: 100)
- `SearchTerm` (string, optional): Search by name or phone number

**Response:** `PagedResult<List<FarmerDTO>>`

**Example Request:**
```http
POST /api/supervisor/farmers
Authorization: Bearer {token}
Content-Type: application/json

{
  "onlyAssigned": true,
  "currentPage": 1,
  "pageSize": 20,
  "searchTerm": "Nguyen"
}
```

**Example Response:**
```json
{
  "succeeded": true,
  "data": [
    {
      "farmerId": "456...",
      "fullName": "Nguyen Van A",
      "address": "123 Main St",
      "phoneNumber": "0901234567",
      "isActive": true,
      "isVerified": true,
      "lastActivityAt": "2024-01-15T10:00:00Z",
      "farmCode": "FA001",
      "plotCount": 3
    }
  ],
  "currentPage": 1,
  "pageSize": 20,
  "totalCount": 15,
  "message": "Successfully retrieved assigned farmers."
}
```

**Use Cases:**
- Supervisor wants to see all farmers they can potentially manage (onlyAssigned=false)
- Supervisor wants to see only their assigned farmers (onlyAssigned=true)

---

## 3. Get Plots by Farmer
**Endpoint:** `POST /api/farmer/{farmerId}/plots`

**Description:** Retrieves all plots owned by a specific farmer with optional filters.

**Path Parameters:**
- `farmerId` (Guid, required): The farmer's ID

**Request Body:**
```json
{
  "currentPage": 1,
  "pageSize": 20,
  "status": "Active",
  "isUnassigned": false
}
```

**Request Body Parameters:**
- `CurrentPage` (int, optional, default: 1)
- `PageSize` (int, optional, default: 20, max: 100)
- `Status` (PlotStatus enum, optional): Filter by plot status (Active, PendingPolygon)
- `IsUnassigned` (bool, optional): 
  - `true` = only plots not in any group
  - `false` = only plots in a group
  - `null` = all plots

**Response:** `PagedResult<List<PlotListResponse>>`

**Example Request:**
```http
POST /api/farmer/123e4567-e89b-12d3-a456-426614174000/plots
Content-Type: application/json

{
  "currentPage": 1,
  "pageSize": 20,
  "status": "Active",
  "isUnassigned": false
}
```

**Example Response:**
```json
{
  "succeeded": true,
  "data": [
    {
      "plotId": "789...",
      "area": 2.5,
      "soThua": 5,
      "soTo": 3,
      "status": "Active",
      "groupId": "456...",
      "boundary": "POLYGON((...))",
      "coordinate": "POINT(10.123 106.456)",
      "groupName": "Group A - Season 1",
      "activeCultivations": 1,
      "activeAlerts": 0
    }
  ],
  "currentPage": 1,
  "pageSize": 20,
  "totalCount": 5,
  "message": "Successfully retrieved plots."
}
```

**Use Cases:**
- View all plots for a farmer
- Find unassigned plots for group formation
- Check plot status and group assignment

---

## 4. Get Reports by Farmer
**Endpoint:** `POST /api/farmer/{farmerId}/reports`

**Description:** Retrieves all emergency reports for a farmer's plots, sorted by newest first.

**Path Parameters:**
- `farmerId` (Guid, required): The farmer's ID

**Request Body:**
```json
{
  "currentPage": 1,
  "pageSize": 20,
  "searchTerm": "pest",
  "status": "Pending",
  "severity": "High",
  "reportType": "Pest"
}
```

**Request Body Parameters:**
- `CurrentPage` (int, optional, default: 1)
- `PageSize` (int, optional, default: 20, max: 100)
- `SearchTerm` (string, optional): Search in title/description
- `Status` (string, optional): Filter by status (Pending, Resolved)
- `Severity` (string, optional): Filter by severity (Low, Medium, High, Critical)
- `ReportType` (string, optional): Filter by type (Pest, Weather, Disease, Other)

**Response:** `PagedResult<List<ReportItemResponse>>`

**Example Request:**
```http
POST /api/farmer/123e4567-e89b-12d3-a456-426614174000/reports
Content-Type: application/json

{
  "currentPage": 1,
  "pageSize": 20,
  "status": "Pending",
  "severity": "High"
}
```

**Example Response:**
```json
{
  "succeeded": true,
  "data": [
    {
      "id": "report-123",
      "plotId": "plot-456",
      "plotName": "5/3",
      "plotArea": 2.5,
      "cultivationPlanId": "plan-789",
      "cultivationPlanName": "Plan 2024-01-15",
      "reportType": "Pest",
      "severity": "High",
      "title": "Brown planthopper infestation",
      "description": "Severe infestation detected...",
      "reportedBy": "Nguyen Van A",
      "reportedByRole": "Farmer",
      "reportedAt": "2024-01-15T14:30:00Z",
      "status": "Pending",
      "images": ["url1", "url2"],
      "coordinates": "10.123,106.456",
      "resolvedBy": null,
      "resolvedAt": null,
      "resolutionNotes": null,
      "farmerName": "Nguyen Van A",
      "clusterName": "Cluster A"
    }
  ],
  "currentPage": 1,
  "pageSize": 20,
  "totalCount": 8,
  "message": "Successfully retrieved farmer reports."
}
```

---

## 5. Get Reports by Supervisor
**Endpoint:** `POST /api/supervisor/reports`

**Description:** Retrieves all emergency reports for farmers managed by the supervisor, sorted by newest first.

**Authorization:** Requires `Supervisor` role

**Request Body:**
```json
{
  "currentPage": 1,
  "pageSize": 20,
  "searchTerm": "disease",
  "status": "Pending",
  "severity": "Critical",
  "reportType": "Disease"
}
```

**Request Body Parameters:**
- `CurrentPage` (int, optional, default: 1)
- `PageSize` (int, optional, default: 20, max: 100)
- `SearchTerm` (string, optional): Search in title/description/farmer name
- `Status` (string, optional): Filter by status (Pending, Resolved)
- `Severity` (string, optional): Filter by severity (Low, Medium, High, Critical)
- `ReportType` (string, optional): Filter by type (Pest, Weather, Disease, Other)

**Response:** `PagedResult<List<ReportItemResponse>>`

**Example Request:**
```http
POST /api/supervisor/reports
Authorization: Bearer {token}
Content-Type: application/json

{
  "currentPage": 1,
  "pageSize": 20,
  "status": "Pending",
  "severity": "Critical"
}
```

**Example Response:**
```json
{
  "succeeded": true,
  "data": [
    {
      "id": "report-123",
      "plotId": "plot-456",
      "plotName": "5/3",
      "reportType": "Disease",
      "severity": "Critical",
      "title": "Rice blast disease outbreak",
      "description": "Multiple plots affected...",
      "reportedBy": "Nguyen Van A",
      "reportedByRole": "Farmer",
      "reportedAt": "2024-01-16T08:00:00Z",
      "status": "Pending",
      "farmerName": "Nguyen Van A",
      "clusterName": "Cluster A"
    }
  ],
  "currentPage": 1,
  "pageSize": 20,
  "totalCount": 12,
  "message": "Successfully retrieved reports for supervised farmers."
}
```

**Use Cases:**
- Monitor all reports from assigned farmers
- Prioritize urgent issues (Critical/High severity)
- Track resolution progress

---

## 6. Change Farmer Status
**Endpoint:** `PUT /api/farmer/{farmerId}/status`

**Description:** Changes a farmer's status (Admin only). Automatically deactivates supervisor assignments when status changes to NotAllowed or Resigned.

**Path Parameters:**
- `farmerId` (Guid, required): The farmer's ID

**Request Body:**
```json
{
  "farmerId": "123e4567-e89b-12d3-a456-426614174000",
  "newStatus": "Warned",
  "reason": "Late submission of farm logs for 3 consecutive tasks"
}
```

**Request Body Parameters:**
- `FarmerId` (Guid, required): Must match path parameter
- `NewStatus` (FarmerStatus enum, required): Normal, Warned, NotAllowed, Resigned
- `Reason` (string, required for Warned/NotAllowed/Resigned): Max 500 characters

**Response:** `Result<Guid>`

**Example Request:**
```http
PUT /api/farmer/123e4567-e89b-12d3-a456-426614174000/status
Content-Type: application/json

{
  "farmerId": "123e4567-e89b-12d3-a456-426614174000",
  "newStatus": "NotAllowed",
  "reason": "Violated group cooperation policies multiple times"
}
```

**Example Response:**
```json
{
  "succeeded": true,
  "data": "123e4567-e89b-12d3-a456-426614174000",
  "message": "Farmer status successfully changed from Normal to NotAllowed. Reason: Violated group cooperation policies multiple times"
}
```

**Status Enum Values:**
- `Normal`: Farmer is normal and can be grouped
- `Warned`: Farmer has been warned due to lateness or other issues
- `NotAllowed`: Farmer is temporarily not allowed to be grouped
- `Resigned`: Farmer has resigned but has a chance to keep cooperating after making a promise

**Side Effects:**
- When changing to `NotAllowed` or `Resigned`: All active SupervisorFarmerAssignments are deactivated
- When changing back to `Normal`: Supervisor may need to manually reassign

---

## 7. Get Farm Logs by Cultivation (FIXED)
**Endpoint:** `POST /api/farmlog/farm-logs/by-cultivation`

**Description:** Retrieves all farm logs for a plot cultivation across ALL versions. Fixed to handle resolve report scenario where new cultivation tasks are created with different IDs but same ProductionPlanTask.

**Request Body:**
```json
{
  "plotCultivationId": "123e4567-e89b-12d3-a456-426614174000",
  "currentPage": 1,
  "pageSize": 10
}
```

**Request Body Parameters:**
- `PlotCultivationId` (Guid, required): The plot cultivation ID
- `CurrentPage` (int, optional, default: 1)
- `PageSize` (int, optional, default: 10)

**Response:** `PagedResult<List<FarmLogDetailResponse>>`

**Example Request:**
```http
POST /api/farmlog/farm-logs/by-cultivation
Content-Type: application/json

{
  "plotCultivationId": "123e4567-e89b-12d3-a456-426614174000",
  "currentPage": 1,
  "pageSize": 10
}
```

**What Was Fixed:**
- **BEFORE**: Query used `CultivationTaskId` directly, which failed to retrieve logs from old versions after resolve report created new cultivation tasks
- **AFTER**: Query now:
  1. Gets all CultivationTasks for the PlotCultivationId (across all versions)
  2. Retrieves all FarmLogs for those CultivationTasks
  3. Groups by ProductionPlanTask to show task continuity

**Example Response:**
```json
{
  "succeeded": true,
  "data": [
    {
      "farmLogId": "log-new",
      "cultivationTaskName": "Pesticide Application",
      "plotName": "Th?a 5, T? 3",
      "loggedDate": "2024-01-20T14:00:00Z",
      "workDescription": "Emergency pesticide treatment (Version 2)",
      "completionPercentage": 100,
      "materialsUsed": [...]
    },
    {
      "farmLogId": "log-old",
      "cultivationTaskName": "Pesticide Application",
      "plotName": "Th?a 5, T? 3",
      "loggedDate": "2024-01-10T10:00:00Z",
      "workDescription": "Regular pesticide application (Version 1)",
      "completionPercentage": 100,
      "materialsUsed": [...]
    }
  ],
  "currentPage": 1,
  "pageSize": 10,
  "totalCount": 2,
  "message": "Successfully retrieved farm log history."
}
```

**Key Improvement:**
Now correctly retrieves farm logs from both old and new cultivation task versions, sorted by logged date (newest first), providing complete historical tracking even after emergency report resolution creates new versions.

---

## Common Response Models

### FarmLogDetailResponse
```typescript
{
  farmLogId: Guid,
  cultivationTaskName: string,
  plotName: string,
  loggedDate: DateTime,
  workDescription?: string,
  completionPercentage: number,
  actualAreaCovered?: decimal,
  serviceCost?: decimal,
  serviceNotes?: string,
  photoUrls?: string[],
  weatherConditions?: string,
  interruptionReason?: string,
  materialsUsed: FarmLogMaterialRecord[]
}
```

### FarmLogMaterialRecord
```typescript
{
  materialName: string,
  actualQuantityUsed: decimal,
  actualCost: decimal,
  notes?: string
}
```

### FarmerDTO
```typescript
{
  farmerId: Guid,
  fullName?: string,
  address?: string,
  phoneNumber?: string,
  isActive: boolean,
  isVerified: boolean,
  lastActivityAt?: DateTime,
  farmCode?: string,
  plotCount: number
}
```

### PlotListResponse
```typescript
{
  plotId: Guid,
  area: decimal,
  soThua?: number,
  soTo?: number,
  status: PlotStatus,
  groupId?: Guid,
  boundary?: string,
  coordinate?: string,
  groupName?: string,
  activeCultivations: number,
  activeAlerts: number
}
```

### ReportItemResponse
```typescript
{
  id: Guid,
  plotId?: Guid,
  plotName?: string,
  plotArea?: decimal,
  cultivationPlanId?: Guid,
  cultivationPlanName?: string,
  reportType: string,
  severity: string,
  title: string,
  description: string,
  reportedBy: string,
  reportedByRole?: string,
  reportedAt: DateTime,
  status: string,
  images?: string[],
  coordinates?: string,
  resolvedBy?: string,
  resolvedAt?: DateTime,
  resolutionNotes?: string,
  farmerName?: string,
  clusterName?: string
}
```

---

## Error Responses

All APIs return standard error responses:

### Not Found (404)
```json
{
  "succeeded": false,
  "data": null,
  "message": "Resource not found.",
  "errors": ["Farmer not found.", "NotFound"]
}
```

### Bad Request (400)
```json
{
  "succeeded": false,
  "data": null,
  "message": "Validation failed.",
  "errors": [
    "Farmer ID is required.",
    "Page size must be between 1 and 100."
  ]
}
```

### Unauthorized (401)
```json
{
  "succeeded": false,
  "data": null,
  "message": "User not authenticated.",
  "errors": ["User not authenticated.", "Unauthorized"]
}
```

### Internal Server Error (500)
```json
{
  "succeeded": false,
  "data": null,
  "message": "An error occurred while processing your request",
  "errors": []
}
```

---

## Summary

### New APIs Created (All POST):
1. ? **POST** /api/farmlog/farm-logs/by-production-plan-task - Get farm logs by production plan task ID
2. ? **POST** /api/supervisor/farmers - Get farmers by supervisor (all or only assigned)
3. ? **POST** /api/farmer/{farmerId}/plots - Get plots by farmer (with filters)
4. ? **POST** /api/farmer/{farmerId}/reports - Get reports by farmer (sorted by newest)
5. ? **POST** /api/supervisor/reports - Get reports by supervisor (sorted by newest)
6. ? **PUT** /api/farmer/{farmerId}/status - Change farmer status (Admin only)

### Fixed API (Changed to POST):
7. ? **POST** /api/farmlog/farm-logs/by-cultivation - Get farm logs by cultivation (handles multiple versions)

## ? Benefits of POST with Request Body

1. **Cleaner URLs** - No long query strings
2. **Better for Complex Filters** - Request body can handle complex filter objects easily
3. **No URL Length Limitations** - Query strings have browser/server limits (2048 chars)
4. **More Secure** - Request bodies are not logged in server access logs
5. **Better Type Safety** - Strongly typed request models with validation
6. **Easier to Test** - Simpler to construct request bodies in Postman/Swagger
7. **Future-Proof** - Easy to add new filter properties without breaking URLs

All APIs include:
- ? Proper validation with FluentValidation
- ? Pagination support
- ? Comprehensive filtering/search capabilities
- ? Structured error handling
- ? Proper authorization where needed
- ? Detailed logging
- ? **Clean request bodies instead of query parameters**
