# Reports API Specification

## 1. POST /api/reports

Create a new report (for farmers/supervisors).

**Request Body:**
```json
{
  "plotCultivationId": "7b8c9d0e-1f2g-3h4i-5j6k-7l8m9n0o1p2q",
  "groupId": null,
  "clusterId": null,
  "alertType": "Pest",
  "title": "Brown planthopper infestation",
  "description": "Severe infestation observed in the field",
  "severity": "High",
  "imageUrls": ["https://example.com/image1.jpg"]
}
```

**Example Response:**
```json
{
  "succeeded": true,
  "data": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "message": "Emergency report created successfully."
}
```

**Notes:**
- `alertType`: Pest | Weather | Disease | Other
- `severity`: Low | Medium | High | Critical
- At least one of `plotCultivationId`, `groupId`, or `clusterId` must be provided
- User role (Farmer/Supervisor) is automatically determined from authentication

---

## 2. GET /api/reports

Get paginated list of reports with filtering.

**Query Parameters:**
- `currentPage` (int, default: 1)
- `pageSize` (int, default: 10)
- `searchTerm` (string, optional)
- `status` (string, optional): Pending | UnderReview | Resolved | Rejected
- `severity` (string, optional): Low | Medium | High | Critical
- `reportType` (string, optional): Pest | Weather | Disease | Other

**Example Request:**
```
GET /api/reports?currentPage=1&pageSize=10&status=Pending&severity=High
```

**Example Response:**
```json
{
  "succeeded": true,
  "data": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "plotId": "1a2b3c4d-5e6f-7g8h-9i0j-1k2l3m4n5o6p",
      "plotName": "123/45",
      "plotArea": 2.5,
      "cultivationPlanId": "7b8c9d0e-1f2g-3h4i-5j6k-7l8m9n0o1p2q",
      "cultivationPlanName": "Plan 2024-01-15",
      "reportType": "Pest",
      "severity": "High",
      "title": "Brown planthopper infestation",
      "description": "Severe infestation observed in plot 123/45",
      "reportedBy": "Nguyen Van A",
      "reportedByRole": "Farmer",
      "reportedAt": "2024-11-26T10:30:00Z",
      "status": "Pending",
      "images": ["https://example.com/image1.jpg"],
      "coordinates": "10.762622,106.660172",
      "resolvedBy": null,
      "resolvedAt": null,
      "resolutionNotes": null,
      "farmerName": "Nguyen Van A",
      "clusterName": "Cluster A"
    }
  ],
  "currentPage": 1,
  "pageSize": 10,
  "totalCount": 45,
  "totalPages": 5
}
```

---

## 3. GET /api/reports/my-reports

Get paginated list of reports created by the authenticated user (Farmer/Supervisor).

**Query Parameters:**
- `currentPage` (int, default: 1)
- `pageSize` (int, default: 10)
- `searchTerm` (string, optional)
- `status` (string, optional): Pending | UnderReview | Resolved | Rejected
- `severity` (string, optional): Low | Medium | High | Critical
- `reportType` (string, optional): Pest | Weather | Disease | Other

**Authentication:** Required (Farmer or Supervisor)

**Example Request:**
```
GET /api/reports/my-reports?currentPage=1&pageSize=10&status=Pending
Authorization: Bearer {your-token}
```

**Example Response:**
```json
{
  "succeeded": true,
  "data": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "plotId": "1a2b3c4d-5e6f-7g8h-9i0j-1k2l3m4n5o6p",
      "plotName": "123/45",
      "plotArea": 2.5,
      "cultivationPlanId": "7b8c9d0e-1f2g-3h4i-5j6k-7l8m9n0o1p2q",
      "cultivationPlanName": "Plan 2024-01-15",
      "reportType": "Pest",
      "severity": "High",
      "title": "Brown planthopper infestation",
      "description": "Severe infestation observed in plot 123/45",
      "reportedBy": "Nguyen Van A",
      "reportedByRole": "Farmer",
      "reportedAt": "2024-11-26T10:30:00Z",
      "status": "Pending",
      "images": ["https://example.com/image1.jpg"],
      "coordinates": "10.762622,106.660172",
      "resolvedBy": null,
      "resolvedAt": null,
      "resolutionNotes": null,
      "farmerName": "Nguyen Van A",
      "clusterName": "Cluster A"
    }
  ],
  "currentPage": 1,
  "pageSize": 10,
  "totalCount": 5,
  "totalPages": 1
}
```

---

## 4. GET /api/reports/{reportId}

Get single report details.

**Path Parameters:**
- `reportId` (guid, required)

**Example Request:**
```
GET /api/reports/3fa85f64-5717-4562-b3fc-2c963f66afa6
```

**Example Response:**
```json
{
  "succeeded": true,
  "data": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "plotId": "1a2b3c4d-5e6f-7g8h-9i0j-1k2l3m4n5o6p",
    "plotName": "123/45",
    "plotArea": 2.5,
    "cultivationPlanId": "7b8c9d0e-1f2g-3h4i-5j6k-7l8m9n0o1p2q",
    "cultivationPlanName": "Plan 2024-01-15",
    "reportType": "Pest",
    "severity": "High",
    "title": "Brown planthopper infestation",
    "description": "Severe infestation observed in plot 123/45",
    "reportedBy": "Nguyen Van A",
    "reportedByRole": "Farmer",
    "reportedAt": "2024-11-26T10:30:00Z",
    "status": "Pending",
    "images": ["https://example.com/image1.jpg"],
    "coordinates": "10.762622,106.660172",
    "resolvedBy": null,
    "resolvedAt": null,
    "resolutionNotes": null,
    "farmerName": "Nguyen Van A",
    "clusterName": "Cluster A"
  }
}
```

---

## 5. GET /api/cultivation-plan/{planId}

Get cultivation plan details with stages, tasks, and materials (newest version).

**Path Parameters:**
- `planId` (guid, required) - PlotCultivationId

**Example Request:**
```
GET /api/cultivation-plan/7b8c9d0e-1f2g-3h4i-5j6k-7l8m9n0o1p2q
```

**Example Response:**
```json
{
  "succeeded": true,
  "data": {
    "id": "7b8c9d0e-1f2g-3h4i-5j6k-7l8m9n0o1p2q",
    "plotId": "1a2b3c4d-5e6f-7g8h-9i0j-1k2l3m4n5o6p",
    "plotName": "123/45",
    "planName": "Plan 2024-01-15",
    "riceVarietyId": "9c8d7e6f-5a4b-3c2d-1e0f-1a2b3c4d5e6f",
    "riceVarietyName": "IR64",
    "basePlantingDate": "2024-01-15T00:00:00Z",
    "totalArea": 2.5,
    "status": "InProgress",
    "estimatedTotalCost": 0,
    "farmerName": "Nguyen Van A",
    "clusterName": "Cluster A",
    "stages": [
      {
        "id": "a1b2c3d4-e5f6-7g8h-9i0j-1k2l3m4n5o6p",
        "stageName": "Land Preparation",
        "sequenceOrder": 1,
        "expectedDurationDays": 7,
        "tasks": [
          {
            "id": "b2c3d4e5-f6g7-8h9i-0j1k-2l3m4n5o6p7q",
            "taskName": "Plowing",
            "description": "Deep plowing to prepare soil",
            "taskType": "LandPreparation",
            "scheduledDate": "2024-01-15T00:00:00Z",
            "scheduledEndDate": "2024-01-17T00:00:00Z",
            "priority": "High",
            "sequenceOrder": 1,
            "materials": [
              {
                "materialId": "c3d4e5f6-g7h8-9i0j-1k2l-3m4n5o6p7q8r",
                "materialName": "Diesel Fuel",
                "quantityPerHa": 15.5,
                "unit": "liters"
              }
            ]
          }
        ]
      }
    ]
  }
}
```

---

## 6. POST /api/reports/{reportId}/resolve

Resolve a report by creating emergency cultivation tasks.

**Path Parameters:**
- `reportId` (guid, required)

**Request Body:**
```json
{
  "reportId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "cultivationPlanId": "7b8c9d0e-1f2g-3h4i-5j6k-7l8m9n0o1p2q",
  "newVersionName": "Emergency v2",
  "resolutionReason": "Pesticide application required",
  "expertId": "d4e5f6g7-h8i9-0j1k-2l3m-4n5o6p7q8r9s",
  "cultivationStageId": "a1b2c3d4-e5f6-7g8h-9i0j-1k2l3m4n5o6p",
  "baseCultivationTasks": [
    {
      "cultivationPlanTaskId": null,
      "taskName": "Emergency Pesticide Application",
      "description": "Apply pesticide for brown planthopper",
      "taskType": "PestControl",
      "scheduledEndDate": "2024-11-28T00:00:00Z",
      "status": "Draft",
      "executionOrder": 1,
      "isContingency": true,
      "contingencyReason": "Brown planthopper infestation",
      "defaultAssignedToUserId": null,
      "defaultAssignedToVendorId": null,
      "materialsPerHectare": [
        {
          "materialId": "e5f6g7h8-i9j0-1k2l-3m4n-5o6p7q8r9s0t",
          "quantityPerHa": 2.5,
          "notes": "Apply in evening"
        }
      ]
    }
  ]
}
```

**Example Response:**
```json
{
  "succeeded": true,
  "data": "f6g7h8i9-j0k1-2l3m-4n5o-6p7q8r9s0t1u",
  "message": "Emergency plan created successfully. New version 'Emergency v2' with 1 tasks."
}
```

**Notes:**
- `cultivationPlanTaskId`: If null, creates new task. If provided, updates existing task.
- `materialsPerHectare`: Materials are specified per hectare and will be scaled by plot area automatically.
- `expertId`: Auto-set from authenticated user, can be omitted in request.

