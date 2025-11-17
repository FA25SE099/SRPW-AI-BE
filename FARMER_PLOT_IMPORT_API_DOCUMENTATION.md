# Farmer and Plot Import API Documentation

## Overview

This document describes the enhanced farmer and plot import system that allows cluster managers to easily import farmers with multiple plots and automatically create plot cultivation records for the current season.

## System Features

✅ **Dynamic Plot Support**: Each farmer can have a different number of plots (1-4+)  
✅ **Personalized Templates**: Download Excel templates pre-filled with farmer data  
✅ **Rice Variety Integration**: Automatically create PlotCultivation records  
✅ **Human-Readable**: Use FarmCode and RiceVarietyName instead of GUIDs  
✅ **Season-Aware**: Automatically associates with current active season  
✅ **Validation**: Comprehensive validation with detailed error messages  

---

## Two-Step Import Process

### Step 1: Import Farmers
Import farmers with their basic information and number of plots they own.

### Step 2: Import Plots
Download a personalized template with pre-filled farmer data, then import plot details.

---

## API Endpoints

### 1. Farmer Import Template Download

**Endpoint:** `GET /api/farmer/download-import-template`

**Description:** Downloads an Excel template for importing farmers with sample data.

**Authentication:** Optional (filters by cluster if authenticated)

**Response:** Excel file (.xlsx)

**Excel Structure:**
| Column | Type | Required | Description | Example |
|--------|------|----------|-------------|---------|
| Id | Guid? | No | Farmer ID (leave empty for auto-generation) | |
| FullName | string | Yes | Farmer's full name | Nguyen Van A |
| PhoneNumber | string | Yes | Phone number (must be unique) | 0901234567 |
| Address | string | No | Full address | 123 Main St, District 1 |
| FarmCode | string | Yes | Unique farm code | FARM001 |
| NumberOfPlots | int? | No | Number of plots farmer owns (default: 1) | 3 |

**Example Request:**
```bash
curl -X GET "https://api.example.com/api/farmer/download-import-template" \
  -H "Authorization: Bearer {token}" \
  -o farmer_template.xlsx
```

**Success Response:**
- Status: 200 OK
- Content-Type: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet
- File: `Farmer_Import_Template_YYYYMMDD.xlsx`

---

### 2. Farmer Import

**Endpoint:** `POST /api/farmer/import`

**Description:** Imports farmers from Excel file. Creates farmer accounts and stores NumberOfPlots for template generation.

**Authentication:** Optional (associates farmers with cluster if authenticated)

**Request:**
- Content-Type: multipart/form-data
- File parameter: `File`

**Example Request:**
```bash
curl -X POST "https://api.example.com/api/farmer/import" \
  -H "Authorization: Bearer {token}" \
  -F "File=@farmers.xlsx"
```

**Success Response:**
```json
{
  "totalRows": 10,
  "successCount": 10,
  "failureCount": 0,
  "errors": [],
  "importedFarmers": [
    {
      "phoneNumber": "0901234567",
      "fullName": "Nguyen Van A",
      "address": "123 Main Street",
      "farmCode": "FARM001",
      "numberOfPlots": 3
    }
  ]
}
```

**Error Response:**
```json
{
  "totalRows": 10,
  "successCount": 8,
  "failureCount": 2,
  "errors": [
    {
      "rowNumber": 3,
      "fieldName": "PhoneNumber",
      "errorMessage": "Phone number '0901234567' already exists in system"
    },
    {
      "rowNumber": 5,
      "fieldName": "FullName",
      "errorMessage": "Name cannot be empty"
    }
  ],
  "importedFarmers": [...]
}
```

**Validation Rules:**
- FullName: Required, cannot be empty
- PhoneNumber: Required, must be unique
- FarmCode: Recommended for plot import
- NumberOfPlots: Optional, defaults to 1
- Password: Auto-generated as "Farmer@123"

---

### 3. Plot Import Template Download

**Endpoint:** `GET /api/plot/download-import-template`

**Description:** Downloads a personalized Excel template with pre-filled farmer data and rice variety reference sheet. Generates one row per plot based on each farmer's `NumberOfPlots`.

**Authentication:** Optional (filters by cluster if authenticated)

**Response:** Excel file (.xlsx) with 2 sheets

**Sheet 1: Plot_Import**
| Column | Type | Required | Pre-filled | Description |
|--------|------|----------|------------|-------------|
| FarmCode | string | Yes | ✅ Yes | Farmer's unique code |
| FarmerName | string | No | ✅ Yes | Farmer's name (reference) |
| PhoneNumber | string | No | ✅ Yes | Farmer's phone (reference) |
| PlotNumber | int | No | ✅ Yes | Plot number for this farmer (1, 2, 3...) |
| SoThua | int? | Yes | ❌ No | Land parcel number |
| SoTo | int? | Yes | ❌ No | Land map sheet number |
| Area | decimal? | Yes | ❌ No | Plot area in square meters |
| SoilType | string? | No | ❌ No | Soil type (Clay, Sandy, Loam) |
| RiceVarietyName | string? | No | ❌ No | Rice variety name (see Sheet 2) |

**Sheet 2: Rice_Varieties** (Reference Only)
| Column | Description |
|--------|-------------|
| VarietyName | Rice variety name to use in Sheet 1 |
| SeasonType | Suitable season (Wet/Dry/All) |
| GrowthDuration | Growth period in days |
| Description | Additional information |

**Example Request:**
```bash
curl -X GET "https://api.example.com/api/plot/download-import-template" \
  -H "Authorization: Bearer {token}" \
  -o plot_template.xlsx
```

**Success Response:**
- Status: 200 OK
- Content-Type: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet
- File: `Plot_Import_Template_YYYYMMDD_HHMMSS.xlsx`
- Message: "Template generated successfully: 10 farmers with 25 plot rows"

**Example Template Content:**

**Sheet 1 (Plot_Import):**
| FarmCode | FarmerName | PhoneNumber | PlotNumber | SoThua | SoTo | Area | SoilType | RiceVarietyName |
|----------|------------|-------------|------------|--------|------|------|----------|-----------------|
| FARM001 | Nguyen Van A | 0901234567 | 1 | | | | | |
| FARM001 | Nguyen Van A | 0901234567 | 2 | | | | | |
| FARM002 | Tran Van B | 0909876543 | 1 | | | | | |
| FARM002 | Tran Van B | 0909876543 | 2 | | | | | |
| FARM002 | Tran Van B | 0909876543 | 3 | | | | | |
| FARM002 | Tran Van B | 0909876543 | 4 | | | | | |

**Sheet 2 (Rice_Varieties):**
| VarietyName | SeasonType | GrowthDuration | Description |
|-------------|------------|----------------|-------------|
| IR64 | Wet Season | 115 days | High yield variety |
| Jasmine | All Season | 120 days | Aromatic rice |
| OM5451 | Dry Season | 95 days | Short duration |

**Error Response:**
```json
{
  "succeeded": false,
  "message": "No farmers found. Please import farmers first using the farmer import template.",
  "data": null
}
```

---

### 4. Plot Import

**Endpoint:** `POST /api/plot/import-excel`

**Description:** Imports plots from Excel file. Creates plot records and optionally creates PlotCultivation records for the current season if RiceVarietyName is provided.

**Authentication:** Optional

**Request:**
- Content-Type: multipart/form-data
- Parameters:
  - `excelFile` (IFormFile): Required - The Excel file
  - `importDate` (DateTime): Optional - Import date (default: current date)

**Example Request:**
```bash
curl -X POST "https://api.example.com/api/plot/import-excel" \
  -H "Authorization: Bearer {token}" \
  -F "excelFile=@plots.xlsx" \
  -F "importDate=2024-01-15"
```

**Success Response:**
```json
{
  "succeeded": true,
  "message": "Successfully imported 15 plots with 12 cultivation records for Winter-Spring 2024 (3 empty rows skipped)",
  "data": [
    {
      "plotId": "a1b2c3d4-...",
      "soThua": 123,
      "soTo": 45,
      "area": 1000.5,
      "farmerId": "e5f6g7h8-...",
      "farmerName": "Nguyen Van A",
      "soilType": "Clay",
      "status": "Active",
      "groupId": null
    }
  ]
}
```

**Error Response - Validation Failed:**
```json
{
  "succeeded": false,
  "message": "Validation failed:\nRow 3: Farmer 'FARM999' not found. Please import farmers first.\nRow 5: SoThua is required and must be > 0\nRow 8: Rice variety 'InvalidVariety' not found. Check 'Rice_Varieties' sheet.",
  "data": null
}
```

**Validation Rules:**
- FarmCode: Required, must exist in database
- SoThua: Required, must be > 0
- SoTo: Required, must be > 0
- Area: Required, must be > 0
- SoilType: Optional, max 100 characters
- RiceVarietyName: Optional, must exist in database if provided
- Empty rows (no SoThua, SoTo, Area) are automatically skipped

**Automatic PlotCultivation Creation:**
- If `RiceVarietyName` is provided AND valid
- If current season is found
- Creates PlotCultivation with:
  - Status: Planned
  - PlantingDate: Current date (can be updated later)
  - Area: Same as plot area
  - SeasonId: Current active season
  - RiceVarietyId: Matched by variety name

---

## Complete Workflow Example

### Scenario
A cluster manager needs to import 3 farmers with their plots:
- Farmer A: 2 plots
- Farmer B: 4 plots  
- Farmer C: 3 plots

### Step-by-Step Process

#### 1. Download Farmer Template
```bash
GET /api/farmer/download-import-template
```
Receives: `Farmer_Import_Template_20240115.xlsx`

#### 2. Fill Farmer Data
Edit Excel file:
| FullName | PhoneNumber | Address | FarmCode | NumberOfPlots |
|----------|-------------|---------|----------|---------------|
| Nguyen Van A | 0901111111 | Address 1 | FARM001 | 2 |
| Tran Van B | 0902222222 | Address 2 | FARM002 | 4 |
| Le Thi C | 0903333333 | Address 3 | FARM003 | 3 |

#### 3. Import Farmers
```bash
POST /api/farmer/import
File: farmers.xlsx
```
Response: 3 farmers created successfully

#### 4. Download Plot Template
```bash
GET /api/plot/download-import-template
```
Receives: `Plot_Import_Template_20240115_143022.xlsx`

**Template automatically contains 9 rows** (2+4+3):
| FarmCode | FarmerName | PlotNumber |
|----------|------------|------------|
| FARM001 | Nguyen Van A | 1 |
| FARM001 | Nguyen Van A | 2 |
| FARM002 | Tran Van B | 1 |
| FARM002 | Tran Van B | 2 |
| FARM002 | Tran Van B | 3 |
| FARM002 | Tran Van B | 4 |
| FARM003 | Le Thi C | 1 |
| FARM003 | Le Thi C | 2 |
| FARM003 | Le Thi C | 3 |

#### 5. Fill Plot Details
Complete the Excel with plot data:
| FarmCode | FarmerName | PlotNumber | SoThua | SoTo | Area | SoilType | RiceVarietyName |
|----------|------------|------------|--------|------|------|----------|-----------------|
| FARM001 | Nguyen Van A | 1 | 100 | 10 | 1000 | Clay | IR64 |
| FARM001 | Nguyen Van A | 2 | 101 | 10 | 1200 | Clay | IR64 |
| FARM002 | Tran Van B | 1 | 200 | 20 | 800 | Sandy | Jasmine |
| FARM002 | Tran Van B | 2 | 201 | 20 | 900 | Sandy | Jasmine |
| FARM002 | Tran Van B | 3 | 202 | 20 | 850 | Sandy | Jasmine |
| FARM002 | Tran Van B | 4 | 203 | 20 | 950 | Sandy | Jasmine |
| FARM003 | Le Thi C | 1 | 300 | 30 | 1100 | Loam | OM5451 |
| FARM003 | Le Thi C | 2 | 301 | 30 | 1050 | Loam | OM5451 |
| FARM003 | Le Thi C | 3 | 302 | 30 | 1150 | Loam | OM5451 |

#### 6. Import Plots
```bash
POST /api/plot/import-excel
File: plots.xlsx
```

Response:
```json
{
  "succeeded": true,
  "message": "Successfully imported 9 plots with 9 cultivation records for Winter-Spring 2024",
  "data": [
    {
      "plotId": "...",
      "soThua": 100,
      "soTo": 10,
      "area": 1000,
      "farmerName": "Nguyen Van A",
      ...
    },
    ...
  ]
}
```

**Result:**
- ✅ 9 plots created
- ✅ 9 PlotCultivation records created for current season
- ✅ All associated with correct farmers
- ✅ Ready for production planning

---

## Error Handling

### Common Errors and Solutions

#### 1. "No farmers found. Please import farmers first"
**Cause:** Trying to download plot template before importing farmers  
**Solution:** Complete Step 1 (Farmer Import) first

#### 2. "Farmer 'FARM001' not found"
**Cause:** FarmCode in plot Excel doesn't match any imported farmer  
**Solution:** 
- Check FarmCode spelling
- Ensure farmers are imported first
- Download fresh template after importing farmers

#### 3. "Rice variety 'XYZ' not found"
**Cause:** RiceVarietyName doesn't exist in database  
**Solution:**
- Check spelling against Sheet 2 (Rice_Varieties)
- Use exact name from reference sheet
- Leave empty if variety not available (can add later)

#### 4. "Phone number already exists"
**Cause:** Duplicate phone number in farmer import  
**Solution:** Each farmer must have unique phone number

#### 5. "Duplicate plot in system"
**Cause:** Plot with same SoThua + SoTo already exists for this farmer  
**Solution:** Check existing plots or use different SoThua/SoTo

#### 6. "No current season found"
**Cause:** No active season configured in system  
**Solution:** Contact administrator to configure seasons

---

## Best Practices

### 1. Farmer Import
- ✅ Use meaningful FarmCode (e.g., "FARM001", "VN_FARM_01")
- ✅ Specify accurate NumberOfPlots for template efficiency
- ✅ Verify phone numbers are unique
- ✅ Keep farmer data backup

### 2. Plot Import
- ✅ Download template AFTER importing farmers
- ✅ Use template as-is (don't delete pre-filled columns)
- ✅ Refer to Sheet 2 for rice variety names
- ✅ Leave RiceVarietyName empty if unsure (can update later)
- ✅ Double-check SoThua and SoTo for accuracy

### 3. Data Validation
- ✅ Test with small batches first (5-10 farmers)
- ✅ Review validation errors carefully
- ✅ Fix all errors before retrying
- ✅ Keep successful import records

### 4. Template Management
- ✅ Download fresh template for each import session
- ✅ Don't reuse old templates (farmer data may change)
- ✅ Save completed templates for records

---

## Technical Notes

### Season Detection
- System automatically detects current season based on date ranges
- PlotCultivation records use detected season
- If no season is active, plots are created but no cultivation records

### Default Values
- Plot Status: `Active`
- Plot Boundary: Default polygon (will be updated by supervisor)
- Cultivation Status: `Planned`
- Cultivation PlantingDate: Current date
- Farmer Password: `Farmer@123`

### Performance
- Batch processing for large imports
- Caching for farmer and rice variety lookups
- Validation before database operations
- Duplicate detection prevents conflicts

### Data Integrity
- Transaction-based operations
- Rollback on critical errors
- Validation at multiple levels
- Comprehensive error reporting

---

## Support

For issues or questions:
1. Check error messages for specific guidance
2. Verify data format matches templates
3. Review this documentation
4. Contact system administrator

---

## Version History

**Version 1.0** (Current)
- Dynamic plot support based on NumberOfPlots
- Personalized template generation
- Automatic PlotCultivation creation
- Rice variety reference sheet
- Comprehensive validation

---

## Related Endpoints

- `POST /api/farmer/Export-Farmer-BasicData` - Export existing farmers
- `GET /api/plot/download-sample-excel` - Download generic plot sample
- `GET /api/plot/get-current-farmer-plots` - View farmer's current plots

---

**End of Documentation**

