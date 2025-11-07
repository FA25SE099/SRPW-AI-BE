# ViewGroupBySeason API Usage Guide

## Overview

The new unified `ViewGroupBySeason` API provides a streamlined way to view both current and historical groups for supervisors. The API intelligently determines what information to display based on the group's state and season.

## Architecture

### Three New Endpoints

1. **`GET /api/supervisor/group-by-season`** - Lightweight overview (fast loading)
2. **`GET /api/supervisor/plan/{planId}/details`** - Detailed plan information (heavy data)
3. **`GET /api/supervisor/available-seasons`** - List of season/year options for dropdown

---

## 1. Group Overview API

### Endpoint
```http
GET /api/supervisor/group-by-season
```

### Purpose
Get a lightweight overview of a group for a specific season and year. Returns different information based on the group's state:
- **PrePlanning**: Shows group readiness and plot details
- **Planning**: Shows basic plan info (draft/pending)
- **InProduction**: Shows plan progress overview
- **Completed**: Shows plan progress + economic summary
- **Archived**: Shows historical data with economics

### Authentication
- **Required**: Yes
- **Role**: `Supervisor`
- **Header**: `Authorization: Bearer {token}`

### Query Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `seasonId` | Guid | No | Current season | The ID of the season |
| `year` | Integer | No | Current year | The year of the season cycle |

### Response Model

```json
{
  "succeeded": true,
  "data": {
    "groupId": "guid",
    "groupName": "string",
    "status": "Active | Completed | Draft",
    "plantingDate": "2024-05-10T00:00:00Z",
    "totalArea": 25.5,
    "areaGeoJson": "{...}",
    "riceVarietyId": "guid",
    "riceVarietyName": "ST25",
    "season": {
      "seasonId": "guid",
      "seasonName": "Hè Thu",
      "seasonType": "Summer-Autumn",
      "startDate": "05/01",
      "endDate": "09/30",
      "isActive": false
    },
    "seasonYear": 2024,
    "isCurrentSeason": false,
    "isPastSeason": true,
    "clusterId": "guid",
    "clusterName": "DongThap1",
    "currentState": "Completed",
    "plots": [...],
    "readiness": null,
    "planProgressOverview": {...},
    "economicsOverview": {...}
  }
}
```

### Group States and What They Show

#### State: PrePlanning
**When**: No production plan exists yet
**Shows**:
- ✅ Group readiness information
- ✅ Plot readiness details (which plots are ready/blocked)
- ✅ Blocking issues and warnings
- ❌ No plan progress
- ❌ No economics

```json
{
  "currentState": "PrePlanning",
  "readiness": {
    "isReady": false,
    "readinessScore": 75,
    "readinessLevel": "Almost Ready",
    "hasRiceVariety": true,
    "hasTotalArea": true,
    "hasPlots": true,
    "allPlotsHavePolygons": false,
    "blockingIssues": [
      "2 plot(s) missing polygon boundaries"
    ],
    "warnings": [
      "No planting date set"
    ],
    "totalPlots": 5,
    "plotsWithPolygon": 3,
    "plotsWithoutPolygon": 2
  },
  "plots": [
    {
      "plotId": "guid",
      "area": 5.5,
      "farmerName": "Nguyen Van A",
      "readiness": {
        "isReady": false,
        "blockingIssues": ["Missing polygon boundary"],
        "warnings": ["Soil type not specified"],
        "readinessLevel": "Blocked"
      }
    }
  ]
}
```

#### State: Planning
**When**: Plan exists but not approved (Draft/Pending status)
**Shows**:
- ❌ No readiness (plan already created)
- ✅ Basic plan information
- ✅ Plan progress overview (0% progress)
- ❌ No economics yet

#### State: InProduction
**When**: Plan is approved and in progress
**Shows**:
- ✅ Plan progress overview
- ✅ Stage and task completion
- ✅ Cost tracking (estimated vs actual)
- ✅ Schedule status
- ❌ No economics yet (incomplete)

```json
{
  "currentState": "InProduction",
  "planProgressOverview": {
    "productionPlanId": "guid",
    "planName": "Kế hoạch Hè Thu 2025",
    "status": "InProgress",
    "basePlantingDate": "2025-05-15T00:00:00Z",
    "totalStages": 5,
    "completedStages": 2,
    "totalTasks": 45,
    "completedTasks": 18,
    "overallProgressPercentage": 40.0,
    "daysElapsed": 35,
    "estimatedDaysRemaining": 75,
    "isOnSchedule": true,
    "daysBehindSchedule": 0,
    "estimatedTotalCost": 25000000,
    "actualCostToDate": 9500000,
    "contingencyTasksCount": 2,
    "tasksWithInterruptions": 1
  }
}
```

#### State: Completed
**When**: All tasks in the plan are completed
**Shows**:
- ✅ Plan progress overview (100%)
- ✅ Full economic summary
- ✅ Performance metrics
- ✅ Yield data

```json
{
  "currentState": "Completed",
  "planProgressOverview": {
    "overallProgressPercentage": 100.0,
    "completedStages": 5,
    "completedTasks": 45,
    ...
  },
  "economicsOverview": {
    "totalEstimatedCost": 25000000,
    "totalActualCost": 24500000,
    "costVariancePercentage": -2.0,
    "expectedYield": 180.0,
    "actualYield": 184.8,
    "yieldVariancePercentage": 2.67,
    "profitMargin": 35.5,
    "performanceRating": "Excellent"
  }
}
```

#### State: Archived
**When**: Past season (season end date has passed)
**Shows**: Same as Completed

---

## 2. Detailed Plan API

### Endpoint
```http
GET /api/supervisor/plan/{planId}/details
```

### Purpose
Get comprehensive production plan details including all stages, tasks, materials, plot-level progress, and full economic breakdown. This is a **heavy endpoint** - use it only when user wants to view detailed information.

### Authentication
- **Required**: Yes
- **Role**: `Supervisor`

### Path Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `planId` | Guid | Yes | The ID of the production plan |

### Response Model

```json
{
  "succeeded": true,
  "data": {
    "productionPlanId": "guid",
    "planName": "Kế hoạch Hè Thu 2024",
    "status": "Completed",
    "basePlantingDate": "2024-05-10T00:00:00Z",
    "submittedAt": "2024-04-15T00:00:00Z",
    "approvedAt": "2024-04-20T00:00:00Z",
    "groupId": "guid",
    "groupName": "Group 67b40a3c",
    "seasonName": "Hè Thu",
    "seasonYear": 2024,
    "totalArea": 25.5,
    "riceVarietyName": "OM5451",
    
    "totalStages": 5,
    "completedStages": 5,
    "totalTasks": 45,
    "completedTasks": 45,
    "overallProgressPercentage": 100.0,
    
    "daysElapsed": 143,
    "estimatedDaysRemaining": 0,
    "isOnSchedule": true,
    
    "estimatedTotalPlanCost": 25000000,
    "actualCostToDate": 24500000,
    "costVariance": -500000,
    "costVariancePercentage": -2.0,
    
    "stages": [
      {
        "stageId": "guid",
        "stageName": "Làm đất",
        "sequenceOrder": 1,
        "totalTasks": 10,
        "completedTasks": 10,
        "progressPercentage": 100.0,
        "status": "Completed",
        "estimatedStageCost": 5000000,
        "actualStageCost": 4800000,
        "tasks": [
          {
            "taskId": "guid",
            "taskName": "Cày bừa",
            "taskType": "LandPreparation",
            "status": "Completed",
            "scheduledDate": "2024-05-03T00:00:00Z",
            "actualStartDate": "2024-05-03T00:00:00Z",
            "actualEndDate": "2024-05-05T00:00:00Z",
            "estimatedCost": 500000,
            "actualMaterialCost": 450000,
            "actualServiceCost": 200000,
            "totalActualCost": 650000,
            "materials": [
              {
                "materialId": "guid",
                "materialName": "Phân hữu cơ vi sinh",
                "unit": "kg",
                "quantityPerHa": 300,
                "estimatedUnitPrice": 15000,
                "actualQuantityUsed": 7650,
                "actualUnitPrice": 14500
              }
            ]
          }
        ]
      }
    ],
    
    "plotProgress": [
      {
        "plotId": "guid",
        "plotIdentifier": "SoThua 15, SoTo 1",
        "area": 5.5,
        "farmerName": "Nguyen Van A",
        "status": "Active",
        "totalTasks": 10,
        "completedTasks": 10,
        "progressPercentage": 100.0,
        "estimatedCost": 5000000,
        "actualCost": 4800000,
        "hasActiveIssues": false,
        "latestCompletedTask": "Thu hoạch",
        "latestCompletedAt": "2024-09-13T00:00:00Z"
      }
    ],
    
    "economicsDetail": {
      "totalEstimatedCost": 25000000,
      "totalActualCost": 24500000,
      "costVariance": -500000,
      "costVariancePercentage": -2.0,
      "actualMaterialCost": 18000000,
      "actualServiceCost": 6500000,
      "expectedYield": 180.0,
      "actualYield": 184.8,
      "yieldVariance": 4.8,
      "yieldVariancePercentage": 2.67,
      "yieldPerHectare": 7.25,
      "totalRevenue": 92400000,
      "grossProfit": 67900000,
      "profitMargin": 73.48,
      "returnOnInvestment": 277.14,
      "costPerKg": 132.58,
      "performanceRating": "Excellent",
      "performanceScore": 95,
      "plotBreakdown": [...]
    }
  }
}
```

---

## 3. Available Seasons API

### Endpoint
```http
GET /api/supervisor/available-seasons
```

### Purpose
Get a list of all season/year combinations where the supervisor has a group. Use this to populate a dropdown selector in the frontend.

### Authentication
- **Required**: Yes
- **Role**: `Supervisor`

### Response Model

```json
{
  "succeeded": true,
  "data": [
    {
      "seasonId": "guid",
      "seasonName": "Hè Thu",
      "seasonType": "Summer-Autumn",
      "year": 2025,
      "displayName": "Hè Thu 2025",
      "isCurrent": true,
      "isPast": false,
      "hasGroup": true
    },
    {
      "seasonId": "guid",
      "seasonName": "Hè Thu",
      "seasonType": "Summer-Autumn",
      "year": 2024,
      "displayName": "Hè Thu 2024",
      "isCurrent": false,
      "isPast": true,
      "hasGroup": true
    },
    {
      "seasonId": "guid",
      "seasonName": "Đông Xuân",
      "seasonType": "Winter-Spring",
      "year": 2023,
      "displayName": "Đông Xuân 2023",
      "isCurrent": false,
      "isPast": true,
      "hasGroup": true
    }
  ]
}
```

---

## Usage Examples

### Example 1: View Current Season Group

**Request:**
```http
GET /api/supervisor/group-by-season
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

**Use Case**: Load supervisor's dashboard showing current group status

**Response**: Returns group for current season and year automatically

---

### Example 2: View Past Completed Group (2024)

**Request:**
```http
GET /api/supervisor/group-by-season?seasonId=a1b2c3d4-e5f6-7890-abcd-ef1234567890&year=2024
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

**Use Case**: View historical group from Summer-Autumn 2024

**Response**: Returns completed group with economic summary showing:
- Total actual cost: 24,500,000 VND
- Actual yield: 184.8 tons (7.2 tons/hectare)
- Performance rating: "Good"

---

### Example 3: View Past Completed Group (2023)

**Request:**
```http
GET /api/supervisor/group-by-season?seasonId=b2c3d4e5-f6a7-8901-bcde-f12345678901&year=2023
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

**Use Case**: View historical group from Winter-Spring 2023

**Response**: Returns archived group with full economic data showing:
- Actual yield: 135 tons (7.5 tons/hectare for 18ha)
- Complete cost breakdown
- Historical performance metrics

---

### Example 4: Get Detailed Plan Information

**Workflow:**
1. First, call `group-by-season` to get overview
2. Extract `planProgressOverview.productionPlanId` from response
3. Call plan details endpoint with that ID

**Request:**
```http
GET /api/supervisor/plan/e9c0a252-10b9-4190-96ac-4f1e19617cf5/details
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

**Use Case**: User clicks "View Detailed Progress" button

**Response**: Returns comprehensive plan data with:
- All 5 stages with individual progress
- All 45 tasks with dates, costs, materials
- Plot-by-plot breakdown
- Full economic analysis with cost per kg, ROI, etc.

---

### Example 5: Build Season Selector Dropdown

**Request:**
```http
GET /api/supervisor/available-seasons
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

**Use Case**: Load dropdown options when page loads

**Frontend Implementation:**
```javascript
// Fetch available seasons
const response = await fetch('/api/supervisor/available-seasons', {
  headers: { 'Authorization': `Bearer ${token}` }
});
const result = await response.json();

// Populate dropdown
const dropdown = document.getElementById('seasonSelector');
result.data.forEach(season => {
  const option = document.createElement('option');
  option.value = JSON.stringify({ seasonId: season.seasonId, year: season.year });
  option.text = season.displayName + (season.isCurrent ? ' (Current)' : '');
  dropdown.appendChild(option);
});

// When user selects a season
dropdown.addEventListener('change', async (e) => {
  const { seasonId, year } = JSON.parse(e.target.value);
  const groupResponse = await fetch(
    `/api/supervisor/group-by-season?seasonId=${seasonId}&year=${year}`,
    { headers: { 'Authorization': `Bearer ${token}` }}
  );
  const groupData = await groupResponse.json();
  displayGroupOverview(groupData.data);
});
```

---

## Frontend UI Flow

### Recommended User Experience

```
┌─────────────────────────────────────────┐
│  Supervisor Dashboard                    │
│                                         │
│  Season Selector: [Hè Thu 2025 ▼]      │
│                                         │
│  ┌───────────────────────────────────┐ │
│  │ Group Overview (Fast Load)        │ │
│  │                                   │ │
│  │ • Group: DongThap1 - 25.5 ha     │ │
│  │ • Rice Variety: ST25              │ │
│  │ • Status: In Production           │ │
│  │                                   │ │
│  │ Progress: 40% [=========>     ]   │ │
│  │ Completed: 18 / 45 tasks          │ │
│  │                                   │ │
│  │ Estimated Cost: 25,000,000 VND    │ │
│  │ Actual Cost: 9,500,000 VND        │ │
│  │                                   │ │
│  │ [View Detailed Progress]          │ │
│  └───────────────────────────────────┘ │
└─────────────────────────────────────────┘
          ↓ (User clicks button)
┌─────────────────────────────────────────┐
│  Detailed Plan View (Heavy Load)        │
│                                         │
│  Stages (5):                            │
│  ✓ Làm đất (100%)                       │
│  ✓ Sạ giống (100%)                      │
│  → Bón phân đợt 1 (50%)                 │
│  ⏳ Bón phân đợt 2 (0%)                 │
│  ⏳ Thu hoạch (0%)                      │
│                                         │
│  Plot Progress:                         │
│  • SoThua 15: 40% (18/45 tasks)        │
│  • SoThua 18: 42% (19/45 tasks)        │
│                                         │
│  [View Task Details]                    │
│  [View Materials]                       │
│  [View Economics]                       │
└─────────────────────────────────────────┘
```

---

## Performance Considerations

### Fast Endpoint (Overview)
- ⚡ **Response Time**: ~100-300ms
- 📦 **Data Size**: ~50-150 KB
- 🎯 **Use For**: Initial page load, dashboard, quick previews

### Heavy Endpoint (Details)
- 🐢 **Response Time**: ~500-2000ms
- 📦 **Data Size**: ~500KB - 5MB (depending on plan size)
- 🎯 **Use For**: On-demand detailed views, reports, analytics

### Best Practices

1. **Always load overview first**
   - Fast initial load
   - User sees something immediately
   
2. **Load details on-demand**
   - Only when user explicitly requests it
   - Show loading indicator
   
3. **Cache season list**
   - Available seasons rarely change
   - Fetch once on app load
   
4. **Progressive loading**
   ```javascript
   // Good: Progressive approach
   loadOverview()      // Fast
     .then(showUI)
     .then(() => {
       if (userClicksDetails) {
         loadDetails() // Slow, but only when needed
       }
     });
   
   // Bad: Load everything upfront
   Promise.all([
     loadOverview(),
     loadDetails()    // Unnecessary if user doesn't view details
   ]);
   ```

---

## Error Handling

### Common Error Responses

#### No Group Found
```json
{
  "succeeded": false,
  "errors": ["No group assigned to this supervisor for season 'Hè Thu 2024'"]
}
```

**HTTP Status**: 404 Not Found

**Reason**: Supervisor has no group for that season/year combination

**Fix**: Check available seasons using `/available-seasons` endpoint

---

#### Unauthorized
```json
{
  "succeeded": false,
  "errors": ["User not authenticated"]
}
```

**HTTP Status**: 401 Unauthorized

**Reason**: Missing or invalid JWT token

**Fix**: Ensure Authorization header is present with valid token

---

#### Plan Not Found
```json
{
  "succeeded": false,
  "errors": ["Production plan not found or not accessible by this supervisor"]
}
```

**HTTP Status**: 404 Not Found

**Reason**: Invalid planId or plan belongs to different supervisor

**Fix**: Verify planId from the group overview response

---

## Migration from Old Endpoints

### Deprecated Endpoints

❌ **OLD**: `GET /api/supervisor/my-group`
✅ **NEW**: `GET /api/supervisor/group-by-season` (no parameters for current)

❌ **OLD**: `GET /api/supervisor/group-history`
✅ **NEW**: `GET /api/supervisor/group-by-season?seasonId={id}&year={year}`

### Migration Guide

**Before:**
```javascript
// Get current group
const current = await fetch('/api/supervisor/my-group');

// Get history
const history = await fetch('/api/supervisor/group-history?year=2024');
```

**After:**
```javascript
// Get current group (same result, better performance)
const current = await fetch('/api/supervisor/group-by-season');

// Get specific historical group
const past2024 = await fetch('/api/supervisor/group-by-season?seasonId={id}&year=2024');
const past2023 = await fetch('/api/supervisor/group-by-season?seasonId={id}&year=2023');

// Get all available options for dropdown
const seasons = await fetch('/api/supervisor/available-seasons');
```

---

## Summary

### When to Use Each Endpoint

| Scenario | Endpoint | Why |
|----------|----------|-----|
| Dashboard/Home page | `group-by-season` (no params) | Fast, shows current group |
| View past season | `group-by-season?seasonId=X&year=Y` | Fast overview of historical data |
| User clicks "View Details" | `plan/{id}/details` | Full data for deep analysis |
| Build season dropdown | `available-seasons` | Get all options |
| Check group readiness | `group-by-season` (current, PrePlanning) | Shows what's blocking plan creation |
| Track ongoing progress | `group-by-season` (current, InProduction) | Real-time progress tracking |
| View completed economics | `group-by-season` (past, Completed) | Cost analysis and yield results |

---

## Contact & Support

For questions or issues with these APIs, please contact the backend development team or refer to the full API documentation at `/swagger`.

