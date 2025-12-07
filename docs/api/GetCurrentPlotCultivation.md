# Get Current Plot Cultivation Detail API

## Overview
New endpoint to retrieve the current active cultivation plan details for a specific plot, including all tasks, materials, and progress information.

## Endpoint
```
GET /api/cultivation-plan/current/{plotId}
```

## Response Structure
```json
{
  "succeeded": true,
  "data": {
    "plotCultivationId": "guid",
    "plotId": "guid",
    "plotName": "Thửa 123, Tờ 45",
    "plotArea": 2.5,
    
    "seasonId": "guid",
    "seasonName": "Đông Xuân",
    "seasonStartDate": "2025-12-01T00:00:00Z",
    "seasonEndDate": "2026-04-30T00:00:00Z",
    
    "riceVarietyId": "guid",
    "riceVarietyName": "OM5451",
    "riceVarietyDescription": "Popular high-yield rice variety",
    
    "plantingDate": "2025-12-15T00:00:00Z",
    "expectedYield": 15.5,
    "actualYield": null,
    "cultivationArea": 2.5,
    "status": "InProgress",
    
    "productionPlanId": "guid",
    "productionPlanName": "Mùa Đông Xuân 2025",
    "productionPlanDescription": "Production plan description",
    
    "activeVersionId": "guid",
    "activeVersionName": "Original",
    
    "tasks": [
      {
        "taskId": "guid",
        "taskName": "Land Preparation",
        "taskDescription": "Plow and level the field",
        "taskType": "Plowing",
        "status": "Completed",
        "priority": "High",
        "plannedStartDate": "2025-12-01T00:00:00Z",
        "plannedEndDate": "2025-12-05T00:00:00Z",
        "actualStartDate": "2025-12-01T00:00:00Z",
        "actualEndDate": "2025-12-04T00:00:00Z",
        "orderIndex": 1,
        "stageName": "Preparation",
        "materials": [
          {
            "materialId": "guid",
            "materialName": "Fertilizer",
            "plannedQuantity": 50.0,
            "actualQuantity": 48.5,
            "unit": "kg"
          }
        ]
      }
    ],
    
    "progress": {
      "totalTasks": 10,
      "completedTasks": 3,
      "inProgressTasks": 2,
      "pendingTasks": 5,
      "completionPercentage": 30.0,
      "daysElapsed": 45,
      "estimatedDaysRemaining": 85
    }
  },
  "message": "Successfully retrieved current cultivation plan."
}
```

## Features
- Returns the current **active** cultivation (InProgress or latest Planned)
- Includes complete task list with materials
- Provides progress metrics (completion %, days elapsed/remaining)
- Loads production plan information
- Includes season and rice variety details

## Usage Example
```bash
curl -X GET "https://your-api.com/api/cultivation-plan/current/{plotId}" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

## Error Responses
- **404**: Plot not found
- **404**: No active cultivation found for plot
- **500**: Server error

## Related Endpoints
- `GET /api/cultivation-plan/by-plot/{plotId}` - Get cultivation history (paginated list)
- `GET /api/cultivation-plan/plan-view/{plotCultivationId}` - Get farmer plan view by cultivation ID

## Notes
- Returns only the **current** cultivation (not historical)
- "Current" means: status is InProgress OR latest Planned
- Progress calculation based on task statuses
- Estimated days remaining calculated from last task's planned end date
