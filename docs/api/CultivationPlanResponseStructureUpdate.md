# Cultivation Plan Response Structure Update

## Overview
Updated the `CurrentPlotCultivationDetailResponse` to use a nested hierarchical structure where stages contain their tasks, instead of a flat list of tasks.

## Changes Made

### 1. Response Structure (`CurrentPlotCultivationDetailResponse.cs`)

**Before:**
```json
{
  "stages": null,
  "tasks": [
    {
      "taskId": "guid",
      "taskName": "Task 1",
      "stageName": "Preparation",
      "orderIndex": 1
    },
    {
      "taskId": "guid",
      "taskName": "Task 2",
      "stageName": "Preparation",
      "orderIndex": 2
    },
    {
      "taskId": "guid",
      "taskName": "Task 3",
      "stageName": "Planting",
      "orderIndex": 1
    }
  ]
}
```

**After:**
```json
{
  "stages": [
    {
      "stageId": "guid",
      "stageName": "Preparation",
      "sequenceOrder": 1,
      "description": "Land preparation stage",
      "typicalDurationDays": 10,
      "tasks": [
        {
          "taskId": "guid",
          "taskName": "Task 1",
          "orderIndex": 1,
          "materials": [...]
        },
        {
          "taskId": "guid",
          "taskName": "Task 2",
          "orderIndex": 2,
          "materials": [...]
        }
      ]
    },
    {
      "stageId": "guid",
      "stageName": "Planting",
      "sequenceOrder": 2,
      "description": "Sowing and planting stage",
      "typicalDurationDays": 5,
      "tasks": [
        {
          "taskId": "guid",
          "taskName": "Task 3",
          "orderIndex": 1,
          "materials": [...]
        }
      ]
    }
  ],
  "progress": {...}
}
```

### 2. New Model Classes

Added `CultivationStageSummary` class:
```csharp
public class CultivationStageSummary
{
    public Guid? StageId { get; set; }
    public string StageName { get; set; } = string.Empty;
    public int SequenceOrder { get; set; }
    public string? Description { get; set; }
    public int? TypicalDurationDays { get; set; }
    public List<CultivationTaskSummary> Tasks { get; set; } = new List<CultivationTaskSummary>();
}
```

Updated `CultivationTaskSummary`:
- Removed `StageName` property (now part of parent stage)
- Kept all other properties (materials, dates, status, etc.)

### 3. Handler Updates

Both handlers now:
1. **Group tasks by stage** using LINQ `GroupBy` with stage properties
2. **Order stages** by `SequenceOrder` (from `ProductionStage.SequenceOrder`)
3. **Order tasks within each stage** by `ExecutionOrder`
4. **Create nested structure** with stages containing their tasks

#### Key Code Pattern:
```csharp
var stagesGroup = tasks
    .GroupBy(task => new
    {
        StageId = task.ProductionPlanTask?.ProductionStage?.Id,
        StageName = task.ProductionPlanTask?.ProductionStage?.StageName ?? "N/A",
        SequenceOrder = task.ProductionPlanTask?.ProductionStage?.SequenceOrder ?? int.MaxValue,
        Description = task.ProductionPlanTask?.ProductionStage?.Description,
        TypicalDurationDays = task.ProductionPlanTask?.ProductionStage?.TypicalDurationDays
    })
    .OrderBy(g => g.Key.SequenceOrder)
    .Select(stageGroup => new CultivationStageSummary
    {
        StageId = stageGroup.Key.StageId,
        StageName = stageGroup.Key.StageName,
        SequenceOrder = stageGroup.Key.SequenceOrder,
        Tasks = stageGroup
            .OrderBy(task => task.ExecutionOrder ?? 0)
            .Select(task => new CultivationTaskSummary { ... })
            .ToList()
    })
    .ToList();
```

## Affected Endpoints

1. **GET `/api/cultivation-plan/current/{plotId}`**
   - Returns cultivation for a specific plot
   - Now with nested stage/task structure

2. **POST `/api/cultivation-plan/by-group-plot`**
   - Returns cultivation for a plot in a specific group
   - Body: `{ "plotId": "guid", "groupId": "guid" }`
   - Now with nested stage/task structure

## Benefits

1. **Better Organization**: Tasks are grouped logically by their production stages
2. **Clearer Hierarchy**: UI can easily render stages with expandable task lists
3. **Stage Metadata**: Access to stage-level information (description, typical duration)
4. **Proper Ordering**: Stages ordered by sequence, tasks ordered within each stage
5. **No Duplication**: Stage name and details don't repeat for each task

## Migration Notes

- **Breaking Change**: Response structure has changed
- Frontend applications need to update their data parsing logic
- The `tasks` array has been replaced with `stages` array containing nested `tasks`
- Progress calculation remains unchanged

## Testing Recommendations

1. Test with multiple stages
2. Test with stages containing multiple tasks
3. Test with tasks that have no associated stage (should appear in "N/A" stage)
4. Verify ordering is correct (stages by sequence, tasks by execution order)
5. Test progress calculations with new structure
