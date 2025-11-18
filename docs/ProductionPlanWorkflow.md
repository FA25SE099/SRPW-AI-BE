# Production Plan Workflow Guide

This guide demonstrates the complete production plan lifecycle from template creation to execution monitoring.

---

## 📋 Overview

```
StandardPlan (Template) 
    ↓ [Expert creates template]
ProductionPlan (Draft)
    ↓ [Supervisor edits if needed]
ProductionPlan (Pending)
    ↓ [Expert reviews]
ProductionPlan (Approved)
    ↓ [System auto-creates tasks]
CultivationTasks (Distributed to each Plot)
    ↓ [Farmers execute]
Monitoring & Tracking
```

---

## 🎯 Phase 1: Create Standard Plan (Template)

**Role:** AgronomyExpert

### 1.1 Create Standard Plan Template

```http
POST /api/StandardPlan
Content-Type: application/json
Authorization: Bearer {expert_token}
```

**Request Body:**

```json
{
  "categoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "planName": "Winter-Spring Rice 2025 - Long Duration",
  "description": "Standard cultivation plan for long-duration rice varieties (140-150 days)",
  "estimatedDurationDays": 145,
  "stages": [
    {
      "stageName": "Land Preparation",
      "sequenceOrder": 1,
      "expectedDurationDays": 7,
      "isMandatory": true,
      "notes": "Prepare soil before planting",
      "tasks": [
        {
          "taskName": "Apply Organic Fertilizer",
          "description": "Apply composted organic matter to improve soil quality",
          "daysAfter": -3,
          "durationDays": 1,
          "taskType": "Fertilizing",
          "priority": "High",
          "sequenceOrder": 1,
          "materials": [
            {
              "materialId": "mat-001-guid",
              "quantityPerHa": 500.0
            }
          ]
        },
        {
          "taskName": "Plowing and Harrowing",
          "description": "Deep plow and harrow to prepare seedbed",
          "daysAfter": -2,
          "durationDays": 1,
          "taskType": "Cultivation",
          "priority": "High",
          "sequenceOrder": 2,
          "materials": []
        }
      ]
    },
    {
      "stageName": "Planting",
      "sequenceOrder": 2,
      "expectedDurationDays": 1,
      "isMandatory": true,
      "tasks": [
        {
          "taskName": "Sowing Seeds",
          "description": "Broadcast or transplant rice seedlings",
          "daysAfter": 0,
          "durationDays": 1,
          "taskType": "Planting",
          "priority": "Critical",
          "sequenceOrder": 1,
          "materials": [
            {
              "materialId": "seed-001-guid",
              "quantityPerHa": 150.0
            }
          ]
        }
      ]
    },
    {
      "stageName": "Growth & Maintenance",
      "sequenceOrder": 3,
      "expectedDurationDays": 90,
      "isMandatory": true,
      "tasks": [
        {
          "taskName": "First Fertilization (Urea)",
          "description": "Apply nitrogen fertilizer for vegetative growth",
          "daysAfter": 7,
          "durationDays": 1,
          "taskType": "Fertilizing",
          "priority": "High",
          "sequenceOrder": 1,
          "materials": [
            {
              "materialId": "urea-001-guid",
              "quantityPerHa": 100.0
            }
          ]
        },
        {
          "taskName": "Pest Control Spray",
          "description": "Apply pesticide to prevent stem borer",
          "daysAfter": 30,
          "durationDays": 1,
          "taskType": "PestControl",
          "priority": "Normal",
          "sequenceOrder": 2,
          "materials": [
            {
              "materialId": "pesticide-001-guid",
              "quantityPerHa": 2.5
            }
          ]
        }
      ]
    },
    {
      "stageName": "Harvest",
      "sequenceOrder": 4,
      "expectedDurationDays": 3,
      "isMandatory": true,
      "tasks": [
        {
          "taskName": "Harvesting",
          "description": "Cut and collect mature rice",
          "daysAfter": 140,
          "durationDays": 2,
          "taskType": "Harvesting",
          "priority": "Critical",
          "sequenceOrder": 1,
          "materials": []
        },
        {
          "taskName": "Drying and Storing",
          "description": "Dry harvested rice to proper moisture content",
          "daysAfter": 142,
          "durationDays": 1,
          "taskType": "PostHarvest",
          "priority": "High",
          "sequenceOrder": 2,
          "materials": []
        }
      ]
    }
  ]
}
```

**Response:**

```json
{
  "succeeded": true,
  "data": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "message": "Standard Plan created successfully.",
  "errors": null
}
```

**Key Points:**
- `daysAfter`: Relative to planting date (Day 0)
  - Negative values = before planting
  - Zero = planting day
  - Positive = after planting
- `quantityPerHa`: Material quantity per hectare (system calculates total based on group area)
- **No prices stored** in StandardPlan - prices are fetched dynamically when creating ProductionPlan

---

### 1.2 Review Standard Plan (Optional)

```http
GET /api/StandardPlan/{standardPlanId}/review
Authorization: Bearer {expert_token}
```

**Response:**

```json
{
  "succeeded": true,
  "data": {
    "planName": "Winter-Spring Rice 2025 - Long Duration",
    "estimatedDurationDays": 145,
    "totalStages": 4,
    "totalTasks": 7,
    "stages": [
      {
        "stageName": "Land Preparation",
        "tasks": [
          {
            "taskName": "Apply Organic Fertilizer",
            "daysAfter": -3,
            "materials": [
              {
                "materialName": "Organic Compost",
                "quantityPerHa": 500.0,
                "unit": "kg"
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

## 🎯 Phase 2: Create Production Plan from Template

**Role:** Supervisor

### 2.1 Generate Plan Draft (Preview)

Before creating the actual plan, preview what it will look like with current prices:

```http
GET /api/production-plans/draft
  ?standardPlanId=3fa85f64-5717-4562-b3fc-2c963f66afa6
  &groupId=group-001-guid
  &basePlantingDate=2025-02-01
Authorization: Bearer {supervisor_token}
```

**Response:**

```json
{
  "succeeded": true,
  "data": {
    "standardPlanId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "groupId": "group-001-guid",
    "planName": "Winter-Spring Rice 2025 - Long Duration - abc12345",
    "totalArea": 15.5,
    "basePlantingDate": "2025-02-01T00:00:00Z",
    "estimatedTotalPlanCost": 45750000,
    "hasPriceWarnings": true,
    "priceWarnings": [
      "Material 'Urea Fertilizer' has no valid price for date 2025-02-08. Using latest available price from 2024-12-01 (70 days old).",
      "Material 'Pesticide XYZ' has no valid price for date 2025-03-03. Using latest available price from 2024-11-15 (108 days old)."
    ],
    "stages": [
      {
        "stageName": "Land Preparation",
        "sequenceOrder": 1,
        "tasks": [
          {
            "taskName": "Apply Organic Fertilizer",
            "scheduledEndDate": "2025-01-29T00:00:00Z",
            "materials": [
              {
                "materialName": "Organic Compost",
                "quantityPerHa": 500.0,
                "estimatedAmount": 3875000,
                "hasPriceWarning": false,
                "priceValidFrom": "2025-01-15T00:00:00Z"
              }
            ]
          }
        ]
      }
    ]
  },
  "message": "Successfully generated production plan draft with 2 price warning(s)."
}
```

**⚠️ Important:**
- Review `priceWarnings` - outdated prices may affect budget accuracy
- `estimatedAmount` = `quantityPerHa` × `totalArea` × `pricePerMaterial`
- This is a **preview only** - nothing is saved yet

---

### 2.2 Create Production Plan

If draft looks good, create the actual plan:

```http
POST /api/production-plans
Content-Type: application/json
Authorization: Bearer {supervisor_token}
```

**Request Body:**

```json
{
  "standardPlanId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "groupId": "group-001-guid",
  "basePlantingDate": "2025-02-01",
  "planName": "Winter-Spring Rice 2025 - Group A",
  "stages": []
}
```

**Response:**

```json
{
  "succeeded": true,
  "data": "plan-001-guid",
  "message": "Production Plan created successfully with status: Draft."
}
```

**Status:** Draft (can still be edited)

---

### 2.3 Edit Production Plan (Optional)

Supervisor can modify the plan before submission:

```http
PUT /api/production-plans/{planId}
Content-Type: application/json
Authorization: Bearer {supervisor_token}
```

**Request Body:**

```json
{
  "planId": "plan-001-guid",
  "planName": "Winter-Spring Rice 2025 - Group A (Updated)",
  "basePlantingDate": "2025-02-05",
  "stages": [
    {
      "stageId": "stage-001-guid",
      "stageName": "Land Preparation (Modified)",
      "sequenceOrder": 1,
      "tasks": [
        {
          "taskId": "task-001-guid",
          "taskName": "Apply Organic Fertilizer (Increased)",
          "daysAfter": -3,
          "taskType": "Fertilizing",
          "materials": [
            {
              "materialId": "mat-001-guid",
              "estimatedAmount": 4500000
            }
          ]
        }
      ]
    }
  ]
}
```

**Response:**

```json
{
  "succeeded": true,
  "data": "plan-001-guid",
  "message": "Production Plan updated successfully."
}
```

---

### 2.4 Submit Plan for Approval

```http
POST /api/production-plans/{planId}/submit
Content-Type: application/json
Authorization: Bearer {supervisor_token}
```

**Request Body:**

```json
{
  "planId": "plan-001-guid",
  "supervisorId": "supervisor-001-guid"
}
```

**Response:**

```json
{
  "succeeded": true,
  "data": "plan-001-guid",
  "message": "Production Plan submitted for approval successfully."
}
```

**Status:** Pending (awaiting expert approval)

---

## 🎯 Phase 3: Expert Review & Approval

**Role:** AgronomyExpert

### 3.1 Get Pending Approvals

```http
GET /api/production-plans/pending-approvals
  ?groupId={optional}
  &seasonId={optional}
Authorization: Bearer {expert_token}
```

**Response:**

```json
{
  "succeeded": true,
  "data": [
    {
      "planId": "plan-001-guid",
      "planName": "Winter-Spring Rice 2025 - Group A",
      "groupName": "Group A - District 1",
      "supervisorName": "Nguyen Van A",
      "submittedAt": "2025-01-20T10:30:00Z",
      "estimatedCost": 45750000,
      "totalArea": 15.5,
      "plotCount": 12
    }
  ]
}
```

---

### 3.2 Get Plan Details

```http
GET /api/production-plans/{planId}
Authorization: Bearer {expert_token}
```

**Response:**

```json
{
  "succeeded": true,
  "data": {
    "planId": "plan-001-guid",
    "planName": "Winter-Spring Rice 2025 - Group A",
    "status": "Pending",
    "basePlantingDate": "2025-02-01T00:00:00Z",
    "totalArea": 15.5,
    "estimatedTotalCost": 45750000,
    "groupName": "Group A - District 1",
    "supervisorName": "Nguyen Van A",
    "stages": [
      {
        "stageName": "Land Preparation",
        "tasks": [
          {
            "taskName": "Apply Organic Fertilizer",
            "scheduledEndDate": "2025-01-29T00:00:00Z",
            "estimatedCost": 3875000
          }
        ]
      }
    ]
  }
}
```

---

### 3.3 Approve or Reject Plan

```http
POST /api/production-plans/{planId}/approve-reject
Content-Type: application/json
Authorization: Bearer {expert_token}
```

**To Approve:**

```json
{
  "planId": "plan-001-guid",
  "isApproved": true,
  "comments": "Plan looks good. Budget is reasonable for the area."
}
```

**To Reject:**

```json
{
  "planId": "plan-001-guid",
  "isApproved": false,
  "comments": "Please reduce pesticide usage and add more organic alternatives."
}
```

**Response (Approved):**

```json
{
  "succeeded": true,
  "data": "plan-001-guid",
  "message": "Production Plan approved successfully."
}
```

**Status:** Approved

---

## 🎯 Phase 4: Automatic Task Distribution

**System automatically executes after approval**

When expert approves a plan, the system:

1. Gets all plots in the group
2. For each plot, creates `PlotCultivation` record
3. For each task in the plan, creates `CultivationTask` for each plot
4. Calculates actual quantities based on plot area
5. Assigns tasks to farmers

**Example Calculation:**

```
StandardPlan Material: 100 kg/ha Urea
Group Total Area: 15.5 ha
ProductionPlan Estimated: 100 × 15.5 = 1,550 kg

Plot 1 Area: 1.2 ha
CultivationTask for Plot 1: 100 × 1.2 = 120 kg Urea

Plot 2 Area: 0.8 ha
CultivationTask for Plot 2: 100 × 0.8 = 80 kg Urea
```

---

## 🎯 Phase 5: Monitor Plan Execution

**Role:** AgronomyExpert / Supervisor

### 5.1 Get Execution Summary

```http
GET /api/production-plans/{planId}/execution-summary
Authorization: Bearer {expert_token}
```

**Response:**

```json
{
  "succeeded": true,
  "data": {
    "planId": "plan-001-guid",
    "planName": "Winter-Spring Rice 2025 - Group A",
    "approvedAt": "2025-01-20T14:00:00Z",
    "approvedByExpert": "Dr. Tran Van B",
    
    "groupId": "group-001-guid",
    "groupName": "Group A - District 1",
    "seasonName": "Winter-Spring 2025",
    "totalArea": 15.5,
    "plotCount": 12,
    "farmerCount": 12,
    
    "totalTasksCreated": 84,
    "tasksCompleted": 36,
    "tasksInProgress": 12,
    "tasksPending": 36,
    "completionPercentage": 42.86,
    
    "estimatedCost": 45750000,
    "actualCost": 18250000,
    
    "firstTaskStarted": "2025-01-29T08:00:00Z",
    "lastTaskCompleted": "2025-02-15T16:30:00Z",
    
    "plotSummaries": [
      {
        "plotId": "plot-001-guid",
        "plotName": "123/45",
        "farmerName": "Nguyen Van C",
        "plotArea": 1.2,
        "taskCount": 7,
        "completedTasks": 3,
        "completionRate": 42.86
      },
      {
        "plotId": "plot-002-guid",
        "plotName": "123/46",
        "farmerName": "Tran Thi D",
        "plotArea": 0.8,
        "taskCount": 7,
        "completedTasks": 4,
        "completionRate": 57.14
      }
    ]
  }
}
```

**Use this to:**
- Track overall plan progress
- Compare estimated vs actual costs
- Identify slow plots
- Monitor farmer participation

---

### 5.2 Get All Cultivation Tasks for Plan

```http
GET /api/production-plans/{planId}/cultivation-tasks
  ?status=InProgress
  &plotId={optional}
Authorization: Bearer {expert_token}
```

**Query Parameters:**
- `status`: Draft, PendingApproval, InProgress, Completed, Cancelled
- `plotId`: Filter by specific plot

**Response:**

```json
{
  "succeeded": true,
  "data": [
    {
      "taskId": "task-001-guid",
      "taskName": "Apply Organic Fertilizer",
      "description": "Apply composted organic matter",
      "taskType": "Fertilizing",
      "status": "InProgress",
      
      "scheduledEndDate": "2025-01-29T00:00:00Z",
      "actualStartDate": "2025-01-29T08:00:00Z",
      "actualEndDate": null,
      
      "plotId": "plot-001-guid",
      "plotName": "123/45",
      "farmerId": "farmer-001-guid",
      "farmerName": "Nguyen Van C",
      
      "actualMaterialCost": 312500,
      "actualServiceCost": 0,
      
      "isContingency": false,
      "contingencyReason": null
    }
  ]
}
```

**Use this to:**
- See all tasks across all plots
- Filter by status to find delayed tasks
- Track task execution timeline

---

### 5.3 Get Plot-Specific Implementation

```http
GET /api/production-plans/plot-implementation
  ?plotId=plot-001-guid
  &productionPlanId=plan-001-guid
Authorization: Bearer {expert_token}
```

**Response:**

```json
{
  "succeeded": true,
  "data": {
    "plotId": "plot-001-guid",
    "plotName": "123/45",
    "soThua": "123",
    "soTo": "45",
    "plotArea": 1.2,
    
    "farmerId": "farmer-001-guid",
    "farmerName": "Nguyen Van C",
    
    "productionPlanId": "plan-001-guid",
    "productionPlanName": "Winter-Spring Rice 2025 - Group A",
    
    "seasonName": "Winter-Spring 2025",
    "riceVarietyName": "IR50404",
    "plantingDate": "2025-02-01T00:00:00Z",
    
    "totalTasks": 7,
    "completedTasks": 3,
    "inProgressTasks": 1,
    "pendingTasks": 3,
    "completionPercentage": 42.86,
    
    "tasks": [
      {
        "taskId": "task-001-guid",
        "taskName": "Apply Organic Fertilizer",
        "description": "Apply composted organic matter",
        "taskType": "Fertilizing",
        "status": "Completed",
        "executionOrder": 1,
        
        "scheduledEndDate": "2025-01-29T00:00:00Z",
        "actualStartDate": "2025-01-29T08:00:00Z",
        "actualEndDate": "2025-01-29T16:00:00Z",
        
        "actualMaterialCost": 312500,
        "materials": [
          {
            "materialId": "mat-001-guid",
            "materialName": "Organic Compost",
            "plannedQuantity": 600.0,
            "actualQuantity": 620.0,
            "actualCost": 312500,
            "unit": "kg"
          }
        ]
      },
      {
        "taskId": "task-002-guid",
        "taskName": "Plowing and Harrowing",
        "status": "InProgress",
        "executionOrder": 2,
        "actualStartDate": "2025-01-30T07:00:00Z",
        "materials": []
      }
    ]
  }
}
```

**Use this to:**
- Show farmer exactly what they need to do
- Compare planned vs actual material usage
- Track specific plot progress
- Generate plot-level reports

---

## 🎯 Phase 6: View All Approved Plans

**Role:** AgronomyExpert / Supervisor

### 6.1 Get All Approved Plans

```http
GET /api/production-plans/approved
  ?groupId={optional}
  &supervisorId={optional}
  &fromDate=2025-01-01
  &toDate=2025-12-31
Authorization: Bearer {expert_token}
```

**Response:**

```json
{
  "succeeded": true,
  "data": [
    {
      "planId": "plan-001-guid",
      "planName": "Winter-Spring Rice 2025 - Group A",
      "groupName": "Group A - District 1",
      "seasonName": "Winter-Spring 2025",
      "approvedAt": "2025-01-20T14:00:00Z",
      "approvedBy": "Dr. Tran Van B",
      "estimatedCost": 45750000,
      "totalArea": 15.5,
      "plotCount": 12,
      "completionPercentage": 42.86
    }
  ]
}
```

---

## 📊 Complete Demo Flow Example

### Demo Script: "From Template to Harvest"

**Scenario:** Expert creates template, Supervisor creates plan for Winter-Spring 2025 season

#### Step 1: Expert Creates Standard Plan (1-2 min)
```bash
POST /api/StandardPlan
# Show the template structure with stages and tasks
# Emphasize: relative dates (daysAfter), no prices stored
```

#### Step 2: Supervisor Generates Draft (30 sec)
```bash
GET /api/production-plans/draft?standardPlanId=X&groupId=Y&basePlantingDate=2025-02-01
# Show: estimated costs, price warnings, calculated quantities
# Point out: dates are now absolute, prices fetched dynamically
```

#### Step 3: Supervisor Creates Plan (30 sec)
```bash
POST /api/production-plans
# Status: Draft
```

#### Step 4: Supervisor Edits (Optional) (1 min)
```bash
PUT /api/production-plans/{id}
# Show: can modify tasks, dates, materials before submission
```

#### Step 5: Supervisor Submits (30 sec)
```bash
POST /api/production-plans/{id}/submit
# Status: Pending
```

#### Step 6: Expert Reviews (1 min)
```bash
GET /api/production-plans/pending-approvals
GET /api/production-plans/{id}
# Review plan details, costs, timeline
```

#### Step 7: Expert Approves (30 sec)
```bash
POST /api/production-plans/{id}/approve-reject
# isApproved: true
# Status: Approved
# System auto-creates cultivation tasks for all plots!
```

#### Step 8: View Execution Summary (2 min)
```bash
GET /api/production-plans/{id}/execution-summary
# Show: 12 plots, 84 tasks created, progress tracking
```

#### Step 9: View Tasks by Plan (1 min)
```bash
GET /api/production-plans/{id}/cultivation-tasks?status=InProgress
# Show: which tasks are currently being executed, by which farmers
```

#### Step 10: View Specific Plot (1 min)
```bash
GET /api/production-plans/plot-implementation?plotId=X&productionPlanId=Y
# Show: farmer's view - their tasks, materials needed, progress
```

---

## 🔑 Key Concepts Summary

### StandardPlan vs ProductionPlan

| Aspect | StandardPlan | ProductionPlan |
|--------|--------------|----------------|
| **Purpose** | Reusable template | Specific season execution |
| **Dates** | Relative (`daysAfter`) | Absolute (calculated from `basePlantingDate`) |
| **Prices** | Not stored | Fetched at creation time |
| **Area** | Generic (per hectare) | Specific group area |
| **Status** | N/A | Draft → Pending → Approved |
| **Editability** | Can update anytime | Only editable in Draft status |

### Price Validation

- StandardPlan defines `quantityPerHa`
- ProductionPlan fetches prices valid for `basePlantingDate`
- If no price found for that date, uses latest available (with warning)
- Warnings help supervisor decide if plan needs price updates

### Task Distribution

- 1 ProductionPlan → N Plots → N × M CultivationTasks
- Each plot gets full set of tasks
- Quantities scaled by plot area
- Tasks assigned to plot owner (farmer)

---

## 🎓 Best Practices

1. **Always use Draft workflow** - generate draft → review warnings → create plan
2. **Update material prices regularly** - avoid outdated price warnings
3. **Review before approval** - check estimated costs match budget
4. **Monitor execution** - use summary endpoint to track progress
5. **Handle rejections** - provide clear feedback to supervisors

---

## 🚨 Common Issues

### Issue: Price Warning

**Problem:** Material price not found for planting date

**Solution:**
- Update material prices before creating plan
- Or accept older price if reasonable

### Issue: Cannot Edit Plan

**Problem:** Plan status is "Pending" or "Approved"

**Solution:**
- Draft status only: can edit
- Pending: must reject back to Draft
- Approved: create new plan

### Issue: No Tasks Created After Approval

**Problem:** Group has no plots

**Solution:**
- Ensure group has plots assigned
- Check plot cultivation records

---

## 📝 Notes for Reviewer Demo

**Highlight These Points:**

1. **Template Reusability** - One StandardPlan used for multiple seasons/groups
2. **Dynamic Pricing** - Prices fetched at creation time, not stored in template
3. **Price Validation** - System warns about outdated prices
4. **Flexible Editing** - Supervisor can modify before submission
5. **Automatic Distribution** - System creates tasks for all plots on approval
6. **Complete Tracking** - Monitor from plan level → group level → plot level
7. **Area-Based Calculation** - All quantities auto-scaled by plot area
8. **Status Workflow** - Clear progression: Draft → Pending → Approved
9. **Role Separation** - Expert creates templates, Supervisor creates plans
10. **Real-World Dates** - Relative template dates become absolute execution dates

