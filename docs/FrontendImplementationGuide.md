# Frontend Implementation Guide - Production Plan Workflow

## 📋 Overview for Frontend Team

This document outlines the UI screens and API integrations needed to implement the complete Production Plan workflow. 

**IMPORTANT:** All APIs are already implemented and deployed. You should integrate directly with the real API endpoints - **DO NOT use mock data**.

**API Reference:** See `docs/ProductionPlanWorkflow.md` for complete API documentation with request/response examples.

---

## 🎯 Required User Roles

Your app needs to handle 3 user roles:

1. **AgronomyExpert** - Creates templates, approves plans
2. **Supervisor** - Creates plans from templates, submits for approval
3. **Farmer** - Views and executes tasks (not covered in this workflow)

---

## 📱 Required Screens & Features

### **1. Standard Plan Management (AgronomyExpert)**

#### Screen: Standard Plan List
- **Route:** `/standard-plans`
- **API:** `GET /api/StandardPlan`
- **Display:**
  - Table with columns: Plan Name, Category, Duration (days), Created Date, Actions
  - Filter by category dropdown
  - Search by name input
- **Actions:**
  - "Create New Template" button → Create screen
  - "View" icon → Detail screen
  - "Edit" icon → Edit screen
  - "Review" icon → Preview screen

#### Screen: Create Standard Plan
- **Route:** `/standard-plans/create`
- **API:** `POST /api/StandardPlan`
- **Form Structure:**
  ```
  Plan Information Section:
  ├── Category (dropdown) *required*
  ├── Plan Name (text input) *required*
  ├── Description (textarea)
  └── Estimated Duration Days (number input) *required*
  
  Stages Section (Repeatable):
  ├── Stage Name *required*
  ├── Sequence Order *required*
  ├── Expected Duration Days
  ├── Is Mandatory (checkbox, default: true)
  ├── Notes (textarea)
  └── Tasks (Nested Repeatable Section)
      ├── Task Name *required*
      ├── Description (textarea)
      ├── Days After Planting (number, can be negative) *required*
      ├── Duration Days (default: 1)
      ├── Task Type (dropdown: Cultivation, Planting, Fertilizing, PestControl, Harvesting, PostHarvest) *required*
      ├── Priority (dropdown: Low, Normal, High, Critical)
      ├── Sequence Order *required*
      └── Materials (Nested Repeatable Section)
          ├── Material (dropdown/autocomplete) *required*
          └── Quantity per Hectare (decimal) *required*
  ```

- **UX Requirements:**
  - "Add Stage" button at bottom
  - "Add Task" button per stage
  - "Add Material" button per task
  - "Remove" button for each stage/task/material
  - Drag-and-drop to reorder stages and tasks (optional but recommended)
  - Material selector with search/autocomplete functionality
  - Auto-increment sequence order when adding new items
  
- **Validation:**
  - Stage sequence order must be unique
  - Task sequence order must be unique within stage
  - Quantity per hectare must be positive
  - Days after can be negative (for pre-planting tasks like land preparation)
  - All required fields must be filled

- **Submit Flow:**
  1. Validate form client-side
  2. POST to `/api/StandardPlan` with request body matching `CreateStandardPlanCommand`
  3. On success (200 OK): Show success toast, redirect to plan list
  4. On error (400 Bad Request): Display validation errors inline near relevant fields

#### Screen: Edit Standard Plan
- **Route:** `/standard-plans/:id/edit`
- **API:** 
  - GET: `GET /api/StandardPlan/{id}` (load existing data)
  - PUT: `PUT /api/StandardPlan/{id}`
- **Display:** Same form as Create, pre-filled with existing data
- **Note:** Editing a template doesn't affect existing production plans created from it

#### Screen: Review Standard Plan
- **Route:** `/standard-plans/:id/review`
- **API:** `GET /api/StandardPlan/{id}/review`
- **Display:**
  - Plan metadata: Name, Duration, Category, Description
  - Timeline visualization (recommended):
    - Horizontal timeline showing Day 0 as planting day
    - Tasks positioned by `daysAfter` value
    - Color code tasks by task type
  - Expandable accordion for each stage
  - Per stage: List of tasks with:
    - Task name, days after, duration, task type
    - Materials list (name, quantity per ha, unit)
  - Read-only view
  - "Edit Plan" button → Edit screen

---

### **2. Production Plan Management (Supervisor)**

#### Screen: Production Plan List
- **Route:** `/production-plans`
- **APIs:** 
  - Approved plans: `GET /api/production-plans/approved`
  - Pending plans: `GET /api/production-plans/pending-approvals`
  - Plan details: `GET /api/production-plans/{id}` (for each plan)
  
- **Display:**
  - Tab navigation: "My Drafts" | "Pending Approval" | "Approved" | "Rejected"
  - Table with columns: Plan Name, Group, Season, Status, Planting Date, Created Date, Actions
  - Status badges with colors:
    - Draft (gray/blue)
    - Pending (yellow/orange)
    - Approved (green)
    - Rejected (red)
  - Filter options: Group, Season, Date range
  - Sort by: Created date (default: newest first)
  
- **Actions per row:**
  - "View Details" → Detail screen
  - "Edit" (only for Draft status) → Edit screen
  - "Submit for Approval" (only for Draft status) → Submit API
  - "View Execution" (only for Approved status) → Execution dashboard

#### Screen: Create Production Plan (Multi-Step Wizard)

**Step 1: Select Template & Parameters**
- **Route:** `/production-plans/create/step1`
- **Form Fields:**
  ```
  Standard Plan (dropdown/select with search) *required*
    └── On select: Show preview card with plan details
  
  Group (dropdown/select) *required*
    └── On select: Display group info:
        - Group name
        - Total area (hectares)
        - Number of plots
        - Season name
  
  Base Planting Date (date picker) *required*
    └── Note: All task dates will be calculated from this date
  
  Plan Name (text input) *required*
    └── Auto-fill suggestion: "{StandardPlan.name} - {Group.name}"
  ```
  
- **Buttons:**
  - "Cancel" → Back to list
  - "Next: Review Draft" → Step 2

**Step 2: Preview & Review Draft**
- **Route:** `/production-plans/create/step2`
- **API:** `GET /api/production-plans/draft?standardPlanId={id}&groupId={id}&basePlantingDate={date}`
- **Display:**

  **Summary Cards (Top):**
  ```
  ┌────────────────────┬────────────────────┬────────────────────┐
  │ Total Area         │ Estimated Cost     │ Duration           │
  │ 15.5 hectares      │ ₫45,750,000        │ 145 days           │
  └────────────────────┴────────────────────┴────────────────────┘
  ```

  **⚠️ Price Warnings Section** (if `hasPriceWarnings: true`):
  ```
  ┌──────────────────────────────────────────────────────────┐
  │ ⚠️ Price Warnings                                        │
  │                                                          │
  │ Some material prices may be outdated. Please review:    │
  │                                                          │
  │ • Material 'Urea Fertilizer' has no valid price for     │
  │   date 2025-02-08. Using latest available price from    │
  │   2024-12-01 (70 days old).                             │
  │                                                          │
  │ • Material 'Pesticide XYZ' has no valid price for date  │
  │   2025-03-03. Using latest available price from         │
  │   2024-11-15 (108 days old).                            │
  │                                                          │
  │ ℹ️ Contact material vendor to update prices before      │
  │   creating this plan.                                    │
  └──────────────────────────────────────────────────────────┘
  ```

  **Stages & Tasks Table:**
  - Expandable accordion per stage
  - Per task row:
    - Task name
    - Scheduled date (calculated absolute date, not relative)
    - Duration days
    - Task type badge
    - Materials column (expandable):
      - Material name
      - Total quantity (quantityPerHa × totalArea)
      - Unit price (if available)
      - Estimated cost
      - Price warning icon (if material has warning)
  - Stage subtotal cost
  - **Grand total cost at bottom**

- **Buttons:**
  - "Back" → Step 1 (preserve form data)
  - "Create Production Plan" → Call create API, go to Step 3

**Step 3: Confirmation & Next Steps**
- **Route:** `/production-plans/create/success`
- **API:** `POST /api/production-plans` (called when user clicks "Create" on Step 2)
- **Display:**
  - Success icon ✓
  - Message: "Production Plan created successfully!"
  - Plan info card: Name, Group, Status (Draft)
  - Next steps suggestion:
    - "You can now edit the plan if needed, or submit it for approval."
  
- **Action Buttons:**
  - "View Plan Details" → Detail screen
  - "Edit Plan" → Edit screen  
  - "Submit for Approval" → Submit API, redirect to list
  - "Create Another Plan" → Step 1
  - "Back to Plan List" → List screen

#### Screen: Edit Production Plan
- **Route:** `/production-plans/:id/edit`
- **APIs:** 
  - GET: `GET /api/production-plans/{id}` (load current data)
  - PUT: `PUT /api/production-plans/{id}`
  
- **Important:** Only allow editing if `status === 'Draft'` or `status === 'Rejected'`
  - If status is Pending or Approved: Show message "Cannot edit plan in current status" and disable form

- **Form Structure:**
  - Plan name (editable)
  - Base planting date (editable) - **Note:** Changing this recalculates all task dates
  - Stages (editable accordion):
    - Stage name
    - Sequence order
    - Tasks (editable nested list):
      - Task name
      - Days after (editable)
      - Description
      - Task type
      - Materials:
        - Material
        - Estimated amount (cost)
        
- **Save Options:**
  - "Save as Draft" button → PUT request, show success toast, stay on page
  - "Save & Submit for Approval" button → PUT request + POST to `/api/production-plans/{id}/submit`, redirect to list

#### Screen: Production Plan Detail
- **Route:** `/production-plans/:id`
- **API:** `GET /api/production-plans/{id}`
- **Display:**

  **Header Section:**
  - Plan name (large heading)
  - Status badge (colored)
  - Breadcrumb: Production Plans > {Plan Name}

  **Metadata Cards:**
  ```
  ┌────────────────┬────────────────┬────────────────┬────────────────┐
  │ Group          │ Season         │ Planting Date  │ Total Area     │
  │ Group A        │ Winter-Spring  │ Feb 1, 2025    │ 15.5 ha        │
  └────────────────┴────────────────┴────────────────┴────────────────┘
  ```

  **Status-Specific Info:**
  - If Approved:
    - "Approved by: Dr. Tran Van B"
    - "Approved on: Jan 20, 2025"
  - If Rejected:
    - Alert box with rejection comments
    - "Rejected by: Dr. Tran Van B"

  **Cost Summary:**
  - Estimated Total Cost: ₫45,750,000
  - If Approved, also show:
    - Actual Cost (from execution): ₫18,250,000
    - Variance: -60% (color code: green if under budget)

  **Stages & Tasks Accordion:**
  - Expandable per stage
  - Task details per row
  - Materials list per task

  **Action Buttons (Context-Sensitive):**
  - If Draft: "Edit Plan" | "Submit for Approval" | "Delete Plan"
  - If Pending: "View Status" (read-only)
  - If Approved: "View Execution Dashboard" → Execution screen
  - If Rejected: "Edit & Resubmit" → Edit screen

---

### **3. Plan Approval (AgronomyExpert)**

#### Screen: Pending Approvals List
- **Route:** `/approvals/pending`
- **API:** `GET /api/production-plans/pending-approvals?groupId={optional}&seasonId={optional}`
- **Display:**
  - Page title: "Pending Approvals"
  - Filters:
    - Group dropdown (optional)
    - Season dropdown (optional)
  - Table columns:
    - Plan Name
    - Group Name
    - Supervisor Name
    - Submitted Date
    - Estimated Cost
    - Plot Count
    - Actions
  - Sort by: Submitted date (oldest first - priority queue)
  - Badge showing total count: "5 plans awaiting approval"
  
- **Actions per row:**
  - "Review & Approve" button → Approval review screen

#### Screen: Plan Approval Review
- **Route:** `/approvals/:id/review`
- **APIs:** 
  - GET: `GET /api/production-plans/{id}`
  - POST: `POST /api/production-plans/{id}/approve-reject`
  
- **Layout:** Two-column layout (desktop) or stacked (mobile)

  **Left Column (or Top):** Plan Details (read-only)
  - Plan name, group, supervisor
  - Cost summary
  - Stages & tasks accordion
  - All the same info as Plan Detail screen

  **Right Column (or Bottom):** Approval Actions
  ```
  ┌──────────────────────────────────────────────┐
  │ Approval Actions                             │
  │                                              │
  │ Comments (optional):                         │
  │ ┌──────────────────────────────────────────┐ │
  │ │                                          │ │
  │ │                                          │ │
  │ └──────────────────────────────────────────┘ │
  │                                              │
  │ [Reject Plan]        [Approve Plan]         │
  │  (Red/Outline)         (Green/Solid)        │
  └──────────────────────────────────────────────┘
  ```

- **Approve Flow:**
  1. User clicks "Approve Plan" button
  2. Show confirmation modal:
     - Title: "Approve Production Plan?"
     - Message: "This will create cultivation tasks for all 12 plots in the group. This action cannot be undone."
     - Buttons: "Cancel" | "Confirm Approval"
  3. On confirm: POST to `/api/production-plans/{id}/approve-reject` with:
     ```json
     {
       "planId": "xxx",
       "isApproved": true,
       "comments": "Plan approved. Budget is reasonable."
     }
     ```
  4. On success:
     - Show success toast: "Plan approved! Cultivation tasks created for 12 plots."
     - Redirect to execution summary: `/production-plans/{id}/execution`

- **Reject Flow:**
  1. User clicks "Reject Plan" button
  2. Show modal with required comments field:
     - Title: "Reject Production Plan"
     - Message: "Please provide feedback for the supervisor."
     - Textarea: "Comments *required*"
     - Buttons: "Cancel" | "Confirm Rejection"
  3. Validate: Comments must not be empty
  4. On confirm: POST to `/api/production-plans/{id}/approve-reject` with:
     ```json
     {
       "planId": "xxx",
       "isApproved": false,
       "comments": "Please reduce pesticide usage and add more organic alternatives."
     }
     ```
  5. On success:
     - Show success toast: "Plan rejected. Supervisor has been notified."
     - Redirect to pending list

---

### **4. Execution Monitoring (AgronomyExpert / Supervisor)**

#### Screen: Plan Execution Dashboard
- **Route:** `/production-plans/:id/execution`
- **API:** `GET /api/production-plans/{id}/execution-summary`
- **Display:**

  **Top Section - Plan Info:**
  ```
  Production Plan Execution
  
  Plan: Winter-Spring Rice 2025 - Group A
  Group: Group A - District 1 | Season: Winter-Spring 2025
  Approved by: Dr. Tran Van B | Approved on: Jan 20, 2025
  ```

  **Key Metrics (4 Cards in a Row):**
  ```
  ┌─────────────────┬─────────────────┬─────────────────┬─────────────────┐
  │ Total Tasks     │ Completion Rate │ Estimated Cost  │ Actual Cost     │
  │ 84              │ ███████░░ 42%   │ ₫45,750,000     │ ₫18,250,000     │
  │ tasks created   │                 │                 │ 60% under budget│
  └─────────────────┴─────────────────┴─────────────────┴─────────────────┘
  ```

  **Progress Breakdown (Visual Stat Cards):**
  ```
  ┌─────────────────┬─────────────────┬─────────────────┬─────────────────┐
  │ ✓ Completed     │ ⟳ In Progress   │ ⧖ Pending       │ ✗ Cancelled     │
  │ 36 tasks        │ 12 tasks        │ 36 tasks        │ 0 tasks         │
  └─────────────────┴─────────────────┴─────────────────┴─────────────────┘
  ```

  **Overall Progress Bar:**
  ```
  Progress: 42.86%
  [██████████████████░░░░░░░░░░░░░░░░░░░░░░░░░░░]
  ```
  - Color: Green if >70%, Yellow if 30-70%, Red if <30%

  **Timeline Info:**
  - First task started: Jan 29, 2025 at 8:00 AM
  - Last task completed: Feb 15, 2025 at 4:30 PM
  - Current date marker (if in progress)

  **Plots Summary Section:**
  - Section title: "Implementation by Plot (12 plots, 12 farmers)"
  - Sort options: By completion rate | By plot name | By farmer name
  - Filter: Show only delayed plots (checkbox)
  
  **Plots Table:**
  | Plot | Farmer | Area (ha) | Tasks | Completed | Progress | Actions |
  |------|--------|-----------|-------|-----------|----------|---------|
  | 123/45 | Nguyen Van C | 1.2 | 7 | 3 | █████░░░░░ 43% | View Details |
  | 123/46 | Tran Thi D | 0.8 | 7 | 4 | ███████░░ 57% | View Details |
  
  - Row background color based on completion rate:
    - Green tint: >70%
    - Yellow tint: 30-70%
    - Red tint: <30%
  - "View Details" button → Plot implementation screen

  **View All Tasks Button:**
  - Bottom button: "View All Cultivation Tasks" → Tasks list screen

#### Screen: Cultivation Tasks List
- **Route:** `/production-plans/:id/tasks`
- **API:** `GET /api/production-plans/{id}/cultivation-tasks?status={status}&plotId={plotId}`
- **Display:**

  **Filters Section:**
  ```
  Filters: [Status: All ▼] [Plot: All Plots ▼] [Clear Filters]
  ```
  - Status dropdown: All | Draft | Pending Approval | In Progress | Completed | Cancelled
  - Plot dropdown: All Plots | Individual plot names
  - Apply filters dynamically (new API call on change)

  **Export Button:**
  - "Export to CSV" or "Export to Excel" (optional, but useful)

  **Tasks Table:**
  | Task Name | Plot | Farmer | Status | Scheduled | Started | Completed | Cost | Actions |
  |-----------|------|--------|--------|-----------|---------|-----------|------|---------|
  | Apply Organic Fertilizer | 123/45 | Nguyen Van C | ✓ Completed | Jan 29 | Jan 29 8:00 | Jan 29 16:00 | ₫312,500 | View |
  | Plowing | 123/45 | Nguyen Van C | ⟳ In Progress | Jan 30 | Jan 30 7:00 | - | ₫0 | View |
  
  - Status badges with icons and colors
  - Sort by: Scheduled date (default) | Status | Plot | Cost
  - Pagination: 50 items per page
  - "View" action → Task detail modal or page

  **Task Count Summary:**
  - "Showing 84 tasks: 36 completed, 12 in progress, 36 pending"

#### Screen: Plot Implementation Detail
- **Route:** `/plots/:plotId/plans/:planId/implementation`
- **API:** `GET /api/production-plans/plot-implementation?plotId={plotId}&productionPlanId={planId}`
- **Display:**

  **Header Section:**
  ```
  Plot Implementation Details
  
  Plot: 123/45 (Thua 123, To 45)
  Area: 1.2 hectares
  Farmer: Nguyen Van C
  
  Production Plan: Winter-Spring Rice 2025 - Group A
  Season: Winter-Spring 2025 | Rice Variety: IR50404
  Planting Date: February 1, 2025
  ```

  **Progress Card:**
  ```
  ┌─────────────────────────────────────────────────────────┐
  │ Implementation Progress                                 │
  │                                                         │
  │ ████████████░░░░░░░░░░░░  42.86%                       │
  │                                                         │
  │ Total: 7 tasks                                          │
  │ ✓ Completed: 3  |  ⟳ In Progress: 1  |  ⧖ Pending: 3  │
  └─────────────────────────────────────────────────────────┘
  ```

  **Task Timeline (Vertical Timeline or Table):**
  
  For each task, display a card:
  ```
  ┌──────────────────────────────────────────────────────┐
  │ 1. Apply Organic Fertilizer          [✓ Completed]   │
  │ Type: Fertilizing | Priority: High                   │
  │                                                      │
  │ Scheduled: Jan 29, 2025                              │
  │ Started: Jan 29, 8:00 AM | Completed: Jan 29, 4:00 PM │
  │                                                      │
  │ Materials: [Show Materials ▼]                        │
  │   ┌────────────────────────────────────────────┐     │
  │   │ Organic Compost                            │     │
  │   │ Planned: 600 kg | Actual: 620 kg (+20 kg) │     │
  │   │ Cost: ₫312,500                             │     │
  │   └────────────────────────────────────────────┘     │
  │                                                      │
  │ Total Material Cost: ₫312,500                        │
  └──────────────────────────────────────────────────────┘
  ```

  - Tasks ordered by execution order
  - Color-coded status indicator on left border
  - Expand/collapse materials section
  - Show variance between planned and actual:
    - Green text: "+20 kg" if over
    - Red text: "-20 kg" if under
    - Gray: "As planned" if exact match
  - If task hasn't started: Show "Scheduled for: {date}"
  - If task in progress: Show "Started: {date}, In progress..."

  **Back Button:**
  - "← Back to Execution Dashboard"

---

