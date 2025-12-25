# Standard Plan Material Cost and Profit Analysis APIs

## Overview
Two new APIs have been created to calculate material costs and profit analysis based on Standard Plans.

## API 1: Calculate Standard Plan Material Cost

### Endpoint
```
POST /api/Material/calculate-standard-plan-material-cost
```

### Description
Calculates the total material costs based on a Standard Plan. The API automatically retrieves all materials defined in the standard plan and calculates their costs for the specified area.

### Request Body
```json
{
  "plotId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",  // Optional: Use plot's actual area
  "area": 2.5,                                        // Optional: Or provide area directly
  "standardPlanId": "a1b2c3d4-5678-90ab-cdef-1234567890ab"  // Required
}
```

**Note:** Either `plotId` OR `area` must be provided (not both, not neither).

### Response Structure
```json
{
  "succeeded": true,
  "data": {
    "area": 2.5,
    "totalCostPerHa": 15000000,
    "totalCostForArea": 37500000,
    "materialCostItems": [
      {
        "materialId": "mat-001",
        "materialName": "NPK Fertilizer",
        "unit": "kg",
        "quantityPerHa": 200,
        "totalQuantityNeeded": 500,
        "amountPerMaterial": 50,
        "packagesNeeded": 10,
        "actualQuantity": 500,
        "pricePerMaterial": 500000,
        "totalCost": 5000000,
        "costPerHa": 2000000,
        "priceValidFrom": "2024-01-01T00:00:00Z"
      }
      // ... more materials
    ],
    "priceWarnings": []
  },
  "message": "Successfully calculated material costs from standard plan."
}
```

### Calculation Logic

1. **Area Determination:**
   - If `plotId` is provided: Uses the actual area from the Plot entity
   - If `area` is provided: Uses the provided value

2. **Material Aggregation:**
   - Retrieves all materials from all stages and tasks in the Standard Plan
   - Groups materials by MaterialId and sums their quantities per hectare

3. **Cost Calculation for Each Material:**
   ```
   Total Quantity Needed = QuantityPerHa × Area
   Packages Needed = CEILING(Total Quantity Needed ÷ Amount Per Material)
   Total Cost = Packages Needed × Price Per Material
   Cost Per Ha = Total Cost ÷ Area
   ```

4. **Important:** The total cost is calculated by first determining the total quantity needed for the entire area, then calculating packages needed (with ceiling for whole packages), and finally calculating the cost. This prevents incorrect calculations that would occur from multiplying per-hectare costs.

### Usage Examples

**Example 1: Using Plot ID**
```bash
curl -X POST "https://api.example.com/api/Material/calculate-standard-plan-material-cost" \
  -H "Content-Type: application/json" \
  -d '{
    "plotId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "standardPlanId": "a1b2c3d4-5678-90ab-cdef-1234567890ab"
  }'
```

**Example 2: Using Direct Area**
```bash
curl -X POST "https://api.example.com/api/Material/calculate-standard-plan-material-cost" \
  -H "Content-Type: application/json" \
  -d '{
    "area": 5.0,
    "standardPlanId": "a1b2c3d4-5678-90ab-cdef-1234567890ab"
  }'
```

---

## API 2: Calculate Standard Plan Profit Analysis

### Endpoint
```
POST /api/Material/calculate-standard-plan-profit-analysis
```

### Description
Performs a comprehensive profit analysis based on a Standard Plan. Calculates expected revenue, costs (materials + services), profit, and profit margins for both per-hectare and total area.

**IMPORTANT:** The per-hectare values are calculated for **exactly 1 hectare** and remain consistent regardless of the total area input. This ensures accurate per-hectare metrics for planning purposes.

### Request Body
```json
{
  "plotId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",  // Optional: Use plot's actual area
  "area": 2.5,                                        // Optional: Or provide area directly
  "standardPlanId": "a1b2c3d4-5678-90ab-cdef-1234567890ab",  // Required
  "pricePerKgRice": 8000,                            // Required: Price per kg (VND)
  "expectedYieldPerHa": 6000,                        // Required: Expected yield (kg/ha)
  "otherServiceCostPerHa": 5000000                   // Optional: Other costs (VND/ha), default: 0
}
```

**Note:** Either `plotId` OR `area` must be provided (not both, not neither).

### Response Structure
```json
{
  "succeeded": true,
  "data": {
    "area": 10,
    "pricePerKgRice": 7500,
    "expectedYieldPerHa": 6500,
    "expectedRevenuePerHa": 48750000,
    "materialCostPerHa": 16479500,           // ? Always based on exactly 1 ha
    "otherServiceCostPerHa": 7300000,
    "totalCostPerHa": 23779500,              // ? Always based on exactly 1 ha
    "profitPerHa": 24970500,                 // ? Always based on exactly 1 ha
    "profitMarginPerHa": 51.22,              // ? Always based on exactly 1 ha
    "expectedRevenueForArea": 487500000,     // ? Scaled to total area
    "materialCostForArea": 164688000,        // ? Optimized for total area
    "otherServiceCostForArea": 73000000,     // ? Scaled to total area
    "totalCostForArea": 237688000,           // ? Total for the area
    "profitForArea": 249812000,              // ? Total profit for the area
    "profitMarginForArea": 51.24,            // ? Margin for the area
    "materialCostDetails": [
      {
        "materialId": "mat-001",
        "materialName": "NPK Fertilizer",
        "unit": "kg",
        "quantityPerHa": 200,
        "totalQuantityForArea": 2000,
        "packagesNeeded": 40,
        "totalCost": 20000000,
        "costPerHa": 2000000
      }
      // ... more materials
    ],
    "warnings": []
  },
  "message": "Successfully calculated profit analysis."
}
```

### Calculation Logic

#### Per Hectare Calculations (ALWAYS for exactly 1 ha):
```
Material Cost Per Ha = [Calculate for exactly 1 ha with package ceiling]
Expected Revenue Per Ha = Price Per Kg Rice × Expected Yield Per Ha
Total Cost Per Ha = Material Cost Per Ha + Other Service Cost Per Ha
Profit Per Ha = Expected Revenue Per Ha - Total Cost Per Ha
Profit Margin Per Ha = (Profit Per Ha ÷ Expected Revenue Per Ha) × 100
```

**Key Point:** The `materialCostPerHa` is ALWAYS calculated for exactly 1 hectare, regardless of the `area` input. This ensures consistency:
- For 1 ha input ? materialCostPerHa = 16,479,500 VND
- For 10 ha input ? materialCostPerHa = 16,479,500 VND (SAME!)

#### Total Area Calculations (Optimized for the specified area):
```
Expected Revenue For Area = Expected Revenue Per Ha × Area
Material Cost For Area = [Calculate optimized packages for total area]
Other Service Cost For Area = Other Service Cost Per Ha × Area
Total Cost For Area = Material Cost For Area + Other Service Cost For Area
Profit For Area = Expected Revenue For Area - Total Cost For Area
Profit Margin For Area = (Profit For Area ÷ Expected Revenue For Area) × 100
```

**Key Point:** The `materialCostForArea` may be slightly less per hectare than `materialCostPerHa` due to package optimization across larger areas.

#### Why Two Separate Calculations?

**Scenario:** You need 120kg of fertilizer per hectare, sold in 50kg bags.

| Area | Quantity Needed | Bags Needed (ceiling) | Actual Amount | Cost |
|------|----------------|----------------------|---------------|------|
| 1 ha | 120 kg | 3 bags | 150 kg | 3 × 500,000 = 1,500,000 VND |
| 10 ha | 1,200 kg | 24 bags | 1,200 kg | 24 × 500,000 = 12,000,000 VND |

- **Per Ha Cost (for 1 ha):** 1,500,000 VND (3 bags, 150kg)
- **Per Ha Cost (for 10 ha):** 1,200,000 VND (24 bags ÷ 10 = 2.4 bags per ha, 1,200kg ÷ 10 = 120kg per ha)

The 10 ha scenario is more efficient (1,200,000 per ha vs 1,500,000 per ha) because there's no wasted material.

**Solution:**
- Use **consistent 1 ha cost** for per-hectare planning and comparisons
- Use **optimized total cost** for actual area procurement and budgeting

### Usage Examples

**Example 1: Using Plot ID with All Parameters**
```bash
curl -X POST "https://api.example.com/api/Material/calculate-standard-plan-profit-analysis" \
  -H "Content-Type: application/json" \
  -d '{
    "plotId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "standardPlanId": "a1b2c3d4-5678-90ab-cdef-1234567890ab",
    "pricePerKgRice": 8000,
    "expectedYieldPerHa": 6000,
    "otherServiceCostPerHa": 5000000
  }'
```

**Example 2: Using Direct Area without Other Service Costs**
```bash
curl -X POST "https://api.example.com/api/Material/calculate-standard-plan-profit-analysis" \
  -H "Content-Type: application/json" \
  -d '{
    "area": 3.0,
    "standardPlanId": "a1b2c3d4-5678-90ab-cdef-1234567890ab",
    "pricePerKgRice": 7500,
    "expectedYieldPerHa": 5500,
    "otherServiceCostPerHa": 0
  }'
```

---

## Validation Rules

### Both APIs:
- Either `plotId` OR `area` must be provided (mutually exclusive)
- `area` must be greater than 0 (when provided)
- `standardPlanId` is required

### Profit Analysis API Additional Rules:
- `pricePerKgRice` must be greater than 0
- `expectedYieldPerHa` must be greater than 0
- `otherServiceCostPerHa` must be greater than or equal to 0

---

## Key Features

### 1. Consistent Per-Hectare Metrics
- **Per-hectare values are ALWAYS calculated for exactly 1 hectare**
- This ensures comparable metrics regardless of total area
- Useful for comparing different standard plans on an equal basis
- Example: Whether you input 1 ha or 100 ha, the per-hectare cost remains the same

### 2. Optimized Total Area Calculations
- Total area costs are optimized for package purchasing
- May result in slightly lower per-hectare costs due to efficiency
- Provides accurate total procurement costs
- Example: Buying for 10 ha may be more efficient than 10 × (cost for 1 ha)

### 3. Two-Level Analysis
```
Per Hectare (1 ha):  For planning, comparison, and standardization
Total Area:          For actual procurement, budgeting, and execution
```

### 4. Flexible Area Input
- Use actual plot area or provide custom area
- Useful for planning different scenarios
- Per-hectare metrics remain consistent for comparison

### 5. Accurate Material Cost Calculation
- Correctly handles package rounding (ceiling)
- Calculates for total area, not per-hectare multiplication
- Prevents cost miscalculations on larger areas

### 6. Comprehensive Profit Analysis
- Revenue calculations based on expected yield
- Full cost breakdown (materials + services)
- Profit and profit margin calculations
- Both per-hectare and total area metrics

---

## Understanding the Response

### For Planning and Comparison
Use the **per-hectare values**:
- `materialCostPerHa`: Consistent cost for 1 ha
- `totalCostPerHa`: Total cost for 1 ha
- `profitPerHa`: Expected profit for 1 ha
- `profitMarginPerHa`: Profit margin for 1 ha

These values remain **constant** regardless of area input.

### For Procurement and Budgeting
Use the **total area values**:
- `materialCostForArea`: Optimized cost for total area
- `totalCostForArea`: Total cost for total area
- `profitForArea`: Expected profit for total area
- `profitMarginForArea`: Profit margin for total area

These values are **optimized** for the specific area.

---

## Business Logic Summary

### Why Calculate 1 Ha Separately?
? **Consistent comparison:** Compare different plans on equal footing  
? **Standard metrics:** Per-hectare cost is a standard agricultural metric  
? **Independent of scale:** Values don't change based on total area  
? **Easier planning:** Predict costs for any area using consistent base

### Why Not Multiply Per-Hectare Costs?
The correct approach is:
```
? WRONG: (Cost for 1 ha) × Area
? RIGHT: Calculate total quantity needed ? Determine packages ? Calculate cost
```

This is because:
1. Materials come in packages (e.g., 50kg bags)
2. For 1 ha: might need 120kg ? 3 bags (ceiling) ? 150kg actual
3. For 2 ha: need 240kg ? 5 bags (ceiling) ? 250kg actual
4. If we multiply: (3 bags × 2) = 6 bags ? 300kg (WRONG! We only need 5 bags)

### Profit Margin Calculation
```
Profit Margin % = (Profit ÷ Revenue) × 100
```

This indicates what percentage of revenue is profit after all costs.

---

## Real-World Example

**Input:**
- Area: 10 hectares
- Standard Plan with various materials
- Rice price: 7,500 VND/kg
- Expected yield: 6,500 kg/ha
- Other services: 7,300,000 VND/ha

**Output:**

| Metric | Per Hectare (1 ha) | Total (10 ha) |
|--------|-------------------|---------------|
| Revenue | 48,750,000 VND | 487,500,000 VND |
| Material Cost | 16,479,500 VND | 164,688,000 VND |
| Other Services | 7,300,000 VND | 73,000,000 VND |
| **Total Cost** | **23,779,500 VND** | **237,688,000 VND** |
| **Profit** | **24,970,500 VND** | **249,812,000 VND** |
| **Margin** | **51.22%** | **51.24%** |

**Notice:**
- Per-hectare material cost: 16,479,500 VND (based on 1 ha calculation)
- Total material cost: 164,688,000 VND (optimized for 10 ha)
- Average per ha from total: 164,688,000 ÷ 10 = 16,468,800 VND (slightly cheaper due to optimization)
- But we report the **consistent 1 ha cost** for comparison purposes!
