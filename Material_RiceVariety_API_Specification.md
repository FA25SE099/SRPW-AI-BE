# Material, Rice Variety, Season & Standard Plan Management API Specification

## Table of Contents
- [Overview](#overview)
- [Material Management API](#material-management-api)
- [Rice Variety Management API](#rice-variety-management-api)
- [Season Management API](#season-management-api)
- [Rice Variety Season Association API](#rice-variety-season-association-api)
- [Standard Plan Management API](#standard-plan-management-api)
- [Common Response Structure](#common-response-structure)
- [Error Handling](#error-handling)

---

## Overview

This document describes the API endpoints for managing **Materials** (fertilizers, pesticides), **Rice Varieties** (with categories), **Seasons**, **Rice Variety-Season Associations**, and **Standard Plans** (cultivation templates) in the Rice Production Management System.

**Base URL:** `/api`

**Authentication:** Required for most endpoints (details TBD)

---

## Material Management API

### Base Path: `/api/material`

### 1. Get All Materials (Paginated)

Retrieve a paginated list of materials with optional filtering by type.

**Endpoint:** `POST /api/material/get-all`

**Method:** `POST`

**Content-Type:** `multipart/form-data`

**Request Body:**
```json
{
  "currentPage": 1,
  "pageSize": 20,
  "type": 0  // Optional: 0 = Fertilizer, 1 = Pesticide
}
```

**Query Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| currentPage | int | Yes | Page number (starts from 1) |
| pageSize | int | Yes | Number of items per page |
| type | MaterialType | No | Filter by material type (0=Fertilizer, 1=Pesticide) |

**Response:** `200 OK`
```json
{
  "succeeded": true,
  "data": [
    {
      "materialId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "name": "NPK Fertilizer 16-16-8",
      "type": 0,
      "ammountPerMaterial": 50,
      "unit": "kg",
      "showout": "50kg",
      "pricePerMaterial": 450000,
      "description": "High quality NPK fertilizer for rice cultivation",
      "manufacturer": "Phân bón Phú Mỹ",
      "isActive": true
    }
  ],
  "currentPage": 1,
  "totalPages": 5,
  "totalCount": 100,
  "pageSize": 20,
  "hasPreviousPage": false,
  "hasNextPage": true,
  "message": "Successfully retrieved materials",
  "errors": null
}
```

---

### 2. Download Material Price List (Excel)

Export all active materials with current prices to Excel file.

**Endpoint:** `POST /api/material/download-excel`

**Method:** `POST`

**Content-Type:** `application/json`

**Request Body:**
```json
"2024-01-15T00:00:00Z"
```

**Response:** `200 OK`
- **Content-Type:** `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`
- **Content-Disposition:** `attachment; filename="Bảng giá sản phẩm ngày 2024-01-15.xlsx"`

**Excel Columns:**
| Column Name (Vietnamese) | Field | Type | Description |
|-------------------------|-------|------|-------------|
| Mã sản phẩm | MaterialId | GUID | Unique material ID |
| Tên sản phẩm | Name | String | Material name |
| Loại sản phẩm | Type | Enum | 0=Fertilizer, 1=Pesticide |
| Dung tích mỗi sản phẩm | AmmountPerMaterial | Decimal | Amount per unit |
| Đơn vị dung tích | Unit | String | Unit of measurement |
| Dung tích sản phẩm (đã ghép đơn vị) | Showout | String | Combined amount + unit |
| Giá mỗi sản phẩm | PricePerMaterial | Decimal | Current price (VND) |
| Mô tả và ghi chú | Description | String | Description |
| Nhà phân phối | Manufacturer | String | Manufacturer name |
| Có đang được sử dụng hay không | IsActive | Boolean | Active status |

---

### 3. Download Sample Material Template (Excel)

Download a sample Excel template for creating new materials.

**Endpoint:** `GET /api/material/download-create-sample-excel`

**Method:** `GET`

**Response:** `200 OK`
- **Content-Type:** `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`
- **Content-Disposition:** `attachment; filename="Material_Sample_Template.xlsx"`

**Use Case:** Download this template, fill in material data, and use for bulk import.

---

### 4. Import Create Materials from Excel

Bulk create new materials by importing an Excel file.

**Endpoint:** `POST /api/material/import-create-excel`

**Method:** `POST`

**Content-Type:** `multipart/form-data`

**Request Body:**
```
excelFile: <file> (Excel file)
importDate: "2024-01-15T00:00:00Z"
```

**Form Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| excelFile | File | Yes | Excel file (.xlsx) with material data |
| importDate | DateTime | Yes | Import timestamp |

**Response:** `200 OK`
```json
{
  "succeeded": true,
  "data": [
    {
      "materialId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "name": "NPK Fertilizer 16-16-8",
      "type": 0,
      "ammountPerMaterial": 50,
      "unit": "kg",
      "showout": "50kg",
      "pricePerMaterial": 450000,
      "description": "High quality NPK fertilizer",
      "manufacturer": "Phân bón Phú Mỹ",
      "isActive": true
    }
  ],
  "message": "Successfully imported 10 materials",
  "errors": null
}
```

---

### 5. Import Update Materials from Excel

Bulk update existing materials by importing an Excel file.

**Endpoint:** `POST /api/material/import-update-excel`

**Method:** `POST`

**Content-Type:** `multipart/form-data`

**Request Body:**
```
excelFile: <file> (Excel file)
importDate: "2024-01-15T00:00:00Z"
```

**Form Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| excelFile | File | Yes | Excel file (.xlsx) with updated material data |
| importDate | DateTime | Yes | Update timestamp |

**Response:** `200 OK`
```json
{
  "succeeded": true,
  "data": [
    {
      "materialId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "name": "NPK Fertilizer 16-16-8 (Updated)",
      "type": 0,
      "ammountPerMaterial": 50,
      "unit": "kg",
      "showout": "50kg",
      "pricePerMaterial": 475000,
      "description": "Updated price",
      "manufacturer": "Phân bón Phú Mỹ",
      "isActive": true
    }
  ],
  "message": "Successfully updated 5 materials",
  "errors": null
}
```

---

### 6. Create Material

Create a new material with price.

**Endpoint:** `POST /api/material`

**Method:** `POST`

**Content-Type:** `application/json`

**Request Body:**
```json
{
  "name": "NPK Fertilizer 16-16-8",
  "type": 0,
  "ammountPerMaterial": 50,
  "unit": "kg",
  "pricePerMaterial": 450000,
  "description": "High quality NPK fertilizer",
  "manufacturer": "Phân bón Phú Mỹ",
  "isActive": true,
  "priceValidFrom": "2024-01-15T00:00:00Z"
}
```

**Response:** `200 OK`
```json
{
  "succeeded": true,
  "data": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "message": "Material created successfully",
  "errors": null
}
```

---

### 7. Update Material

Update an existing material and optionally update price.

**Endpoint:** `PUT /api/material/{id}`

**Method:** `PUT`

**Content-Type:** `application/json`

**Request Body:**
```json
{
  "materialId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "NPK Fertilizer 16-16-8",
  "type": 0,
  "ammountPerMaterial": 50,
  "unit": "kg",
  "pricePerMaterial": 475000,
  "description": "Updated description",
  "manufacturer": "Phân bón Phú Mỹ",
  "isActive": true,
  "priceValidFrom": "2024-02-01T00:00:00Z"
}
```

**Response:** `200 OK`
```json
{
  "succeeded": true,
  "data": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "message": "Material updated successfully",
  "errors": null
}
```

---

### 8. Delete Material

Soft delete a material (sets IsActive = false).

**Endpoint:** `DELETE /api/material/{id}`

**Method:** `DELETE`

**Response:** `200 OK`
```json
{
  "succeeded": true,
  "data": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "message": "Material deleted successfully",
  "errors": null
}
```

---

## Rice Variety Management API

### Base Path: `/api/ricevariety`

### 1. Get All Rice Varieties

Retrieve all rice varieties with optional filtering.

**Endpoint:** `GET /api/ricevariety`

**Method:** `GET`

**Query Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| search | string | No | Search by variety name or characteristics |
| isActive | boolean | No | Filter by active status |
| categoryId | GUID | No | Filter by category (short-day/long-day) |

**Example Requests:**
```http
GET /api/ricevariety
GET /api/ricevariety?isActive=true
GET /api/ricevariety?search=ST25
GET /api/ricevariety?categoryId=3fa85f64-5717-4562-b3fc-2c963f66afa6
GET /api/ricevariety?categoryId=3fa85f64-5717-4562-b3fc-2c963f66afa6&isActive=true
```

**Response:** `200 OK`
```json
{
  "succeeded": true,
  "data": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "varietyName": "ST25",
      "categoryId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
      "categoryName": "Giống ngắn ngày",
      "baseGrowthDurationDays": 95,
      "baseYieldPerHectare": 6.5,
      "description": "Giống lúa chất lượng cao, được xếp hạng ngon nhất thế giới",
      "characteristics": "Hạt dài, thơm, chống chịu hạn tốt",
      "isActive": true,
      "associatedSeasons": [
        {
          "seasonId": "season-guid-1",
          "seasonName": "Đông Xuân 2024",
          "startDate": "11/01",
          "endDate": "04/30",
          "growthDurationDays": 95,
          "expectedYieldPerHectare": 6.8,
          "optimalPlantingStart": "11/15",
          "optimalPlantingEnd": "12/31",
          "riskLevel": 1,
          "isRecommended": true
        }
      ]
    },
    {
      "id": "8d8c6d89-8f52-4a3e-9e12-3b7c4e8f9a1b",
      "varietyName": "OM5451",
      "categoryId": "9f7a5c8d-6b3e-4d2f-8a1c-5e7b9d4f2a6c",
      "categoryName": "Giống dài ngày",
      "baseGrowthDurationDays": 120,
      "baseYieldPerHectare": 7.2,
      "description": "Giống lúa năng suất cao",
      "characteristics": "Thân to, chống chịu sâu bệnh tốt",
      "isActive": true,
      "associatedSeasons": []
    }
  ],
  "message": "Successfully retrieved all rice varieties.",
  "errors": null
}
```

---

### 2. Change Rice Season Association

Associate a rice variety with a specific season.

**Endpoint:** `POST /api/ricevariety/change-rice-season`

**Method:** `POST`

**Content-Type:** `application/json`

**Request Body:**
```json
{
  "riceId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "seasonId": "7c9e6679-7425-40de-944b-e07fc1f90ae7"
}
```

**Request Schema:**
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| riceId | GUID | Yes | Rice variety ID |
| seasonId | GUID | Yes | Season ID |

**Response:** `200 OK`
```json
{
  "succeeded": true,
  "data": "9a8b7c6d-5e4f-3a2b-1c0d-9e8f7a6b5c4d",
  "message": "Successfully matched rice with season.",
  "errors": null
}
```

**Error Response:** `400 Bad Request`
```json
{
  "succeeded": false,
  "data": null,
  "message": "Already matching rice 3fa85f64-5717-4562-b3fc-2c963f66afa6 and season 7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "errors": ["Duplicate association"]
}
```

---

### 3. Download Rice Varieties (Excel)

Export rice varieties to Excel file with optional filtering.

**Endpoint:** `POST /api/ricevariety/download-excel`

**Method:** `POST`

**Content-Type:** `application/json`

**Request Body:**
```json
{
  "inputDate": "2024-01-15T00:00:00Z",
  "categoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "isActive": true
}
```

**Request Schema:**
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| inputDate | DateTime | Yes | Export date for filename |
| categoryId | GUID | No | Filter by category |
| isActive | Boolean | No | Filter by active status |

**Example Requests:**
```json
// Export all varieties
{
  "inputDate": "2024-01-15T00:00:00Z"
}

// Export only short-day varieties
{
  "inputDate": "2024-01-15T00:00:00Z",
  "categoryId": "SHORT_DAY_CATEGORY_GUID"
}

// Export only active long-day varieties
{
  "inputDate": "2024-01-15T00:00:00Z",
  "categoryId": "LONG_DAY_CATEGORY_GUID",
  "isActive": true
}
```

**Response:** `200 OK`
- **Content-Type:** `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`
- **Content-Disposition:** `attachment; filename="Danh_sach_giong_lua_20240115.xlsx"`

**Excel Columns:**
| Column Name | Field | Type | Description |
|-------------|-------|------|-------------|
| Id | Id | GUID | Variety ID |
| VarietyName | VarietyName | String | Rice variety name |
| CategoryId | CategoryId | GUID | Category ID |
| CategoryName | CategoryName | String | Category name (Giống ngắn ngày / Giống dài ngày) |
| BaseGrowthDurationDays | BaseGrowthDurationDays | Integer | Growth duration in days |
| BaseYieldPerHectare | BaseYieldPerHectare | Decimal | Expected yield per hectare (tons) |
| Description | Description | String | Variety description |
| Characteristics | Characteristics | String | Variety characteristics |
| IsActive | IsActive | Boolean | Active status |

---

### 4. Download Sample Rice Variety Template (Excel)

Download a sample Excel template with example rice variety data.

**Endpoint:** `GET /api/ricevariety/download-sample-excel`

**Method:** `GET`

**Response:** `200 OK`
- **Content-Type:** `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`
- **Content-Disposition:** `attachment; filename="Mau_nhap_lieu_giong_lua.xlsx"`

**Sample Data Included:**
- ST25 (Giống ngắn ngày) - 95 days, 6.5 tons/ha
- OM5451 (Giống dài ngày) - 120 days, 7.2 tons/ha

**Use Case:** Download this template to understand the expected format for rice variety data.

---

### 5. Create Rice Variety

Create a new rice variety.

**Endpoint:** `POST /api/ricevariety/create`

**Method:** `POST`

**Content-Type:** `application/json`

**Request Body:**
```json
{
  "varietyName": "ST25",
  "categoryId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "baseGrowthDurationDays": 95,
  "baseYieldPerHectare": 6.5,
  "description": "Giống lúa chất lượng cao",
  "characteristics": "Hạt dài, thơm, chống chịu hạn tốt",
  "isActive": true
}
```

**Response:** `200 OK`
```json
{
  "succeeded": true,
  "data": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "message": "Rice variety created successfully",
  "errors": null
}
```

---

### 6. Update Rice Variety

Update an existing rice variety.

**Endpoint:** `PUT /api/ricevariety/{id}`

**Method:** `PUT`

**Content-Type:** `application/json`

**Request Body:**
```json
{
  "riceVarietyId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "varietyName": "ST25 Updated",
  "categoryId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "baseGrowthDurationDays": 98,
  "baseYieldPerHectare": 6.8,
  "description": "Updated description",
  "characteristics": "Updated characteristics",
  "isActive": true
}
```

**Response:** `200 OK`
```json
{
  "succeeded": true,
  "data": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "message": "Rice variety updated successfully",
  "errors": null
}
```

---

### 7. Delete Rice Variety

Soft delete a rice variety (sets IsActive = false).

**Endpoint:** `DELETE /api/ricevariety/{id}`

**Method:** `DELETE`

**Response:** `200 OK`
```json
{
  "succeeded": true,
  "data": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "message": "Rice variety deleted successfully",
  "errors": null
}
```

---

## Season Management API

### Base Path: `/api/season`

### 1. Get All Seasons

Retrieve all seasons with optional filtering.

**Endpoint:** `GET /api/season`

**Method:** `GET`

**Query Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| search | string | No | Search by season name or type |
| isActive | boolean | No | Filter by active status |

**Example Requests:**
```http
GET /api/season
GET /api/season?isActive=true
GET /api/season?search=Đông
```

**Response:** `200 OK`
```json
{
  "succeeded": true,
  "data": [
    {
      "id": "guid",
      "seasonName": "Đông Xuân 2024",
      "startDate": "11/01",
      "endDate": "04/30",
      "seasonType": "Winter-Spring",
      "isActive": true,
      "createdAt": "2024-01-15T00:00:00Z"
    }
  ],
  "message": "Successfully retrieved all seasons.",
  "errors": null
}
```

---

### 2. Create Season

Create a new season.

**Endpoint:** `POST /api/season`

**Method:** `POST`

**Content-Type:** `application/json`

**Request Body:**
```json
{
  "seasonName": "Đông Xuân 2024",
  "startDate": "11/01",
  "endDate": "04/30",
  "seasonType": "Winter-Spring",
  "isActive": true
}
```

**Response:** `200 OK`
```json
{
  "succeeded": true,
  "data": "guid",
  "message": "Season created successfully",
  "errors": null
}
```

---

### 3. Update Season

Update an existing season.

**Endpoint:** `PUT /api/season/{id}`

**Method:** `PUT`

**Content-Type:** `application/json`

**Request Body:**
```json
{
  "seasonId": "guid",
  "seasonName": "Đông Xuân 2024-2025",
  "startDate": "11/01",
  "endDate": "04/30",
  "seasonType": "Winter-Spring",
  "isActive": true
}
```

**Response:** `200 OK`
```json
{
  "succeeded": true,
  "data": "guid",
  "message": "Season updated successfully",
  "errors": null
}
```

---

### 4. Delete Season

Soft delete a season (sets IsActive = false).

**Endpoint:** `DELETE /api/season/{id}`

**Method:** `DELETE`

**Response:** `200 OK`
```json
{
  "succeeded": true,
  "data": "guid",
  "message": "Season deleted successfully",
  "errors": null
}
```

---

## Rice Variety Season Association API

### Base Path: `/api/ricevarietyseason`

### 1. Get All Rice Variety Season Associations

Retrieve all rice variety-season associations with filtering.

**Endpoint:** `GET /api/ricevarietyseason`

**Method:** `GET`

**Query Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| riceVarietyId | GUID | No | Filter by rice variety |
| seasonId | GUID | No | Filter by season |
| isRecommended | boolean | No | Filter by recommendation status |

**Example Requests:**
```http
GET /api/ricevarietyseason
GET /api/ricevarietyseason?riceVarietyId=guid
GET /api/ricevarietyseason?seasonId=guid
GET /api/ricevarietyseason?isRecommended=true
```

**Response:** `200 OK`
```json
{
  "succeeded": true,
  "data": [
    {
      "id": "guid",
      "riceVarietyId": "guid",
      "riceVarietyName": "ST25",
      "seasonId": "guid",
      "seasonName": "Đông Xuân 2024",
      "growthDurationDays": 95,
      "expectedYieldPerHectare": 6.8,
      "optimalPlantingStart": "11/15",
      "optimalPlantingEnd": "12/31",
      "riskLevel": 1,
      "seasonalNotes": "Thích hợp vùng đất phù sa",
      "isRecommended": true,
      "createdAt": "2024-01-15T00:00:00Z"
    }
  ],
  "message": "Successfully retrieved all rice variety season associations.",
  "errors": null
}
```

---

### 2. Get Rice Variety Season Detail

Get detailed information about a specific rice variety-season association.

**Endpoint:** `GET /api/ricevarietyseason/{id}`

**Method:** `GET`

**Response:** `200 OK`
```json
{
  "succeeded": true,
  "data": {
    "id": "guid",
    "riceVarietyId": "guid",
    "riceVarietyName": "ST25",
    "categoryId": "guid",
    "categoryName": "Giống ngắn ngày",
    "seasonId": "guid",
    "seasonName": "Đông Xuân 2024",
    "seasonStartDate": "11/01",
    "seasonEndDate": "04/30",
    "growthDurationDays": 95,
    "expectedYieldPerHectare": 6.8,
    "optimalPlantingStart": "11/15",
    "optimalPlantingEnd": "12/31",
    "riskLevel": 1,
    "seasonalNotes": "Thích hợp vùng đất phù sa",
    "isRecommended": true,
    "createdAt": "2024-01-15T00:00:00Z",
    "lastModified": "2024-01-20T00:00:00Z"
  },
  "message": "Successfully retrieved rice variety season association details.",
  "errors": null
}
```

---

### 3. Create Rice Variety Season Association

Associate a rice variety with a season.

**Endpoint:** `POST /api/ricevarietyseason`

**Method:** `POST`

**Content-Type:** `application/json`

**Request Body:**
```json
{
  "riceVarietyId": "guid",
  "seasonId": "guid",
  "growthDurationDays": 95,
  "expectedYieldPerHectare": 6.8,
  "optimalPlantingStart": "11/15",
  "optimalPlantingEnd": "12/31",
  "riskLevel": 1,
  "seasonalNotes": "Thích hợp vùng đất phù sa",
  "isRecommended": true
}
```

**Response:** `200 OK`
```json
{
  "succeeded": true,
  "data": "guid",
  "message": "Rice variety season association created successfully",
  "errors": null
}
```

---

### 4. Update Rice Variety Season Association

Update an existing rice variety-season association.

**Endpoint:** `PUT /api/ricevarietyseason/{id}`

**Method:** `PUT`

**Content-Type:** `application/json`

**Request Body:**
```json
{
  "riceVarietySeasonId": "guid",
  "growthDurationDays": 98,
  "expectedYieldPerHectare": 7.0,
  "optimalPlantingStart": "11/10",
  "optimalPlantingEnd": "12/25",
  "riskLevel": 0,
  "seasonalNotes": "Updated notes",
  "isRecommended": true
}
```

**Response:** `200 OK`
```json
{
  "succeeded": true,
  "data": "guid",
  "message": "Rice variety season association updated successfully",
  "errors": null
}
```

---

### 5. Delete Rice Variety Season Association

Permanently delete a rice variety-season association.

**Endpoint:** `DELETE /api/ricevarietyseason/{id}`

**Method:** `DELETE`

**Response:** `200 OK`
```json
{
  "succeeded": true,
  "data": "guid",
  "message": "Rice variety season association deleted successfully",
  "errors": null
}
```

---

## Standard Plan Management API

### Base Path: `/api/standardplan`

### 1. Get All Standard Plans

Retrieve all standard plans with optional filtering.

**Endpoint:** `GET /api/standardplan`

**Method:** `GET`

**Query Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| categoryId | GUID | No | Filter by category (short-day/long-day) |
| searchTerm | string | No | Search by plan name or description |
| isActive | boolean | No | Filter by active status |

**Example Requests:**
```http
GET /api/standardplan
GET /api/standardplan?isActive=true
GET /api/standardplan?searchTerm=ngắn ngày
GET /api/standardplan?categoryId=3fa85f64-5717-4562-b3fc-2c963f66afa6
GET /api/standardplan?categoryId=3fa85f64-5717-4562-b3fc-2c963f66afa6&isActive=true
```

**Response:** `200 OK`
```json
{
  "succeeded": true,
  "data": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "name": "Kế hoạch canh tác giống ngắn ngày",
      "description": "Kế hoạch canh tác tiêu chuẩn cho giống lúa ngắn ngày",
      "categoryId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
      "categoryName": "Giống ngắn ngày",
      "totalDuration": 95,
      "isActive": true,
      "totalTasks": 15,
      "totalStages": 5,
      "createdAt": "2024-01-15T00:00:00Z",
      "createdBy": "9a8b7c6d-5e4f-3a2b-1c0d-9e8f7a6b5c4d",
      "lastModified": "2024-01-20T00:00:00Z",
      "lastModifiedBy": "9a8b7c6d-5e4f-3a2b-1c0d-9e8f7a6b5c4d"
    }
  ],
  "message": "Successfully retrieved standard plans",
  "errors": null
}
```

---

### 2. Get Standard Plan Detail

Retrieve detailed information about a specific standard plan including all stages, tasks, and materials.

**Endpoint:** `GET /api/standardplan/{id}`

**Method:** `GET`

**Path Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| id | GUID | Yes | Standard plan ID |

**Example Request:**
```http
GET /api/standardplan/3fa85f64-5717-4562-b3fc-2c963f66afa6
```

**Response:** `200 OK`
```json
{
  "succeeded": true,
  "data": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "planName": "Kế hoạch canh tác giống ngắn ngày",
    "description": "Kế hoạch canh tác tiêu chuẩn cho giống lúa ngắn ngày",
    "totalDurationDays": 95,
    "isActive": true,
    "categoryId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
    "categoryName": "Giống ngắn ngày",
    "createdBy": "9a8b7c6d-5e4f-3a2b-1c0d-9e8f7a6b5c4d",
    "creatorName": "Nguyễn Văn A",
    "createdAt": "2024-01-15T00:00:00Z",
    "lastModified": "2024-01-20T00:00:00Z",
    "stages": [
      {
        "id": "stage-guid",
        "stageName": "Chuẩn bị đất",
        "sequenceOrder": 1,
        "expectedDurationDays": 7,
        "isMandatory": true,
        "notes": "Làm đất, phân rơm rạ",
        "tasks": [
          {
            "id": "task-guid",
            "taskName": "Làm đất",
            "sequenceOrder": 1,
            "taskType": "LandPreparation",
            "priority": "High",
            "description": "Cày, bừa đất",
            "materials": []
          }
        ]
      }
    ],
    "totalStages": 5,
    "totalTasks": 15,
    "totalMaterialTypes": 8
  },
  "message": "Successfully retrieved standard plan details.",
  "errors": null
}
```

---

### 3. Review Standard Plan

Preview a standard plan with calculated dates, quantities, and costs based on sow date and area.

**Endpoint:** `GET /api/standardplan/{id}/review`

**Method:** `GET`

**Path Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| id | GUID | Yes | Standard plan ID |

**Query Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| sowDate | DateTime | Yes | Planned sowing date |
| areaInHectares | decimal | Yes | Cultivation area in hectares |

**Example Request:**
```http
GET /api/standardplan/3fa85f64-5717-4562-b3fc-2c963f66afa6/review?sowDate=2024-02-01T00:00:00Z&areaInHectares=10.5
```

**Response:** `200 OK`
```json
{
  "succeeded": true,
  "data": {
    "standardPlanId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "planName": "Kế hoạch canh tác giống ngắn ngày",
    "description": "Kế hoạch canh tác tiêu chuẩn",
    "categoryId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
    "categoryName": "Giống ngắn ngày",
    "sowDate": "2024-02-01T00:00:00Z",
    "areaInHectares": 10.5,
    "estimatedStartDate": "2024-01-25T00:00:00Z",
    "estimatedEndDate": "2024-05-05T00:00:00Z",
    "totalDurationDays": 95,
    "estimatedTotalCost": 47250000,
    "estimatedCostPerHectare": 4500000,
    "stages": [],
    "totalStages": 5,
    "totalTasks": 15,
    "totalMaterialTypes": 8,
    "totalMaterialQuantity": 1575.5
  },
  "message": "Successfully generated standard plan review.",
  "errors": null
}
```

---

### 4. Update Standard Plan

Update an existing standard plan.

**Endpoint:** `PUT /api/standardplan/{id}`

**Method:** `PUT`

**Content-Type:** `application/json`

**Path Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| id | GUID | Yes | Standard plan ID |

**Request Body:**
```json
{
  "standardPlanId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "planName": "Updated Plan Name",
  "description": "Updated description",
  "totalDurationDays": 100,
  "isActive": true
}
```

**Response:** `200 OK`
```json
{
  "succeeded": true,
  "data": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "message": "Standard plan updated successfully",
  "errors": null
}
```

---

### 5. Download Standard Plans (Excel)

Export standard plans to Excel file with optional filtering.

**Endpoint:** `POST /api/standardplan/download-excel`

**Method:** `POST`

**Content-Type:** `application/json`

**Request Body:**
```json
{
  "inputDate": "2024-01-15T00:00:00Z",
  "categoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "isActive": true
}
```

**Request Schema:**
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| inputDate | DateTime | Yes | Export date for filename |
| categoryId | GUID | No | Filter by category |
| isActive | Boolean | No | Filter by active status |

**Example Requests:**
```json
// Export all standard plans
{
  "inputDate": "2024-01-15T00:00:00Z"
}

// Export only short-day category plans
{
  "inputDate": "2024-01-15T00:00:00Z",
  "categoryId": "SHORT_DAY_CATEGORY_GUID"
}

// Export only active long-day plans
{
  "inputDate": "2024-01-15T00:00:00Z",
  "categoryId": "LONG_DAY_CATEGORY_GUID",
  "isActive": true
}
```

**Response:** `200 OK`
- **Content-Type:** `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`
- **Content-Disposition:** `attachment; filename="Ke_hoach_chuan_20240115.xlsx"`

**Excel Columns:**
| Column Name | Field | Type | Description |
|-------------|-------|------|-------------|
| Id | Id | GUID | Standard plan ID |
| Name | Name | String | Plan name |
| Description | Description | String | Plan description |
| CategoryId | CategoryId | GUID | Category ID |
| CategoryName | CategoryName | String | Category name (Giống ngắn ngày / Giống dài ngày) |
| TotalDuration | TotalDuration | Integer | Total duration in days |
| IsActive | IsActive | Boolean | Active status |
| TotalTasks | TotalTasks | Integer | Total number of tasks |
| TotalStages | TotalStages | Integer | Total number of stages |
| CreatedAt | CreatedAt | DateTimeOffset | Creation timestamp |
| CreatedBy | CreatedBy | GUID | Creator user ID |
| LastModified | LastModified | DateTimeOffset | Last modification timestamp |
| LastModifiedBy | LastModifiedBy | GUID | Last modifier user ID |

---

### 6. Download Sample Standard Plan Template (Excel)

Download a sample Excel template with example standard plan data.

**Endpoint:** `GET /api/standardplan/download-sample-excel`

**Method:** `GET`

**Response:** `200 OK`
- **Content-Type:** `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`
- **Content-Disposition:** `attachment; filename="Mau_ke_hoach_chuan.xlsx"`

**Sample Data Included:**
- Kế hoạch canh tác giống ngắn ngày - 95 days, 5 stages, 15 tasks
- Kế hoạch canh tác giống dài ngày - 120 days, 6 stages, 18 tasks

**Use Case:** Download this template to understand the expected format for standard plan data.

---

## Common Response Structure

### Success Response
```json
{
  "succeeded": true,
  "data": <object or array>,
  "message": "Success message",
  "errors": null
}
```

### Paginated Response
```json
{
  "succeeded": true,
  "data": [<array of items>],
  "currentPage": 1,
  "totalPages": 5,
  "totalCount": 100,
  "pageSize": 20,
  "hasPreviousPage": false,
  "hasNextPage": true,
  "message": "Success message",
  "errors": null
}
```

### Error Response
```json
{
  "succeeded": false,
  "data": null,
  "message": "Error message",
  "errors": ["Error detail 1", "Error detail 2"]
}
```

---

## Error Handling

### HTTP Status Codes

| Status Code | Description |
|-------------|-------------|
| 200 OK | Request successful |
| 400 Bad Request | Invalid request parameters or business logic error |
| 401 Unauthorized | Authentication required |
| 403 Forbidden | Insufficient permissions |
| 404 Not Found | Resource not found |
| 500 Internal Server Error | Server error |

### Common Error Scenarios

#### Material Management
- **Duplicate Material:** Attempting to create a material that already exists
- **Invalid Material Type:** Type value must be 0 (Fertilizer) or 1 (Pesticide)
- **Invalid Excel Format:** Uploaded Excel file format is incorrect
- **Material Not Found:** Attempting to update non-existent material
- **No Materials Found:** Export request when no materials match filters
- **Route ID Mismatch:** URL ID doesn't match request body ID

#### Rice Variety Management
- **Duplicate Rice Variety:** Rice variety with same name already exists
- **Invalid Category:** Category ID doesn't exist
- **Rice Variety Not Found:** Requested variety doesn't exist
- **No Rice Varieties Found:** Export request when no varieties match filters
- **Invalid Search Parameters:** Search term or filter values are invalid
- **Route ID Mismatch:** URL ID doesn't match request body ID

#### Season Management
- **Duplicate Season:** Season with same name already exists
- **Season Not Found:** Requested season doesn't exist
- **Invalid Date Format:** Start/end date format must be MM/DD
- **Route ID Mismatch:** URL ID doesn't match request body ID

#### Rice Variety Season Association Management
- **Duplicate Association:** Rice variety already associated with the season
- **Rice Variety Not Found:** Referenced rice variety doesn't exist
- **Season Not Found:** Referenced season doesn't exist
- **Association Not Found:** Requested association doesn't exist
- **Route ID Mismatch:** URL ID doesn't match request body ID

#### Standard Plan Management
- **Standard Plan Not Found:** Requested plan doesn't exist
- **Invalid Category:** Category ID doesn't exist
- **No Standard Plans Found:** Export request when no plans match filters
- **Invalid Review Parameters:** Sow date or area values are invalid
- **Route ID Mismatch:** URL ID doesn't match request body ID

---

## Data Models

### MaterialType Enum
```
0 = Fertilizer (Phân bón)
1 = Pesticide (Thuốc trừ sâu)
```

### Rice Variety Categories
- **Giống ngắn ngày** (Short-day variety): < 100 days growth duration
- **Giống dài ngày** (Long-day variety): ≥ 100 days growth duration

### Season Format
- **StartDate / EndDate**: String format "MM/DD" (e.g., "11/01" for November 1st)
- **SeasonType**: Examples - "Winter-Spring", "Summer-Autumn", "Wet Season", "Dry Season"

### Risk Level Enum
- **0** = Low Risk
- **1** = Medium Risk
- **2** = High Risk

### Standard Plan Structure
- **Standard Plan**: Template for cultivation process
  - Contains multiple **Stages** (e.g., Land Preparation, Sowing, Fertilization)
  - Each Stage contains multiple **Tasks**
  - Each Task may require **Materials** (fertilizers, pesticides)
- Linked to **Rice Variety Category** (not specific variety)
- Used to generate **Production Plans** for actual cultivation

### Rice Variety Season Association
- Links specific **Rice Varieties** with **Seasons**
- Tracks season-specific growth duration and yield expectations
- Provides optimal planting windows
- Includes risk assessment for each combination
- Recommendation status for farmer guidance

---

## Best Practices

### Material Management
1. **Pagination**: Always use reasonable page sizes (10-50 items)
2. **Excel Import**: Validate data before import to avoid partial updates
3. **Price Updates**: Use import-update-excel for bulk price changes
4. **Filtering**: Filter by material type to reduce payload size

### Rice Variety Management
1. **Search**: Use specific search terms for better performance
2. **Category Filtering**: Filter by category when working with specific growth duration ranges
3. **Excel Export**: Use date parameter for proper file organization
4. **Associated Seasons**: GetAll endpoint includes all associated seasons automatically
5. **Category Validation**: Ensure category exists before creating/updating varieties

### Season Management
1. **Date Format**: Always use MM/DD format for start and end dates
2. **Season Naming**: Use clear, descriptive names with year information
3. **Overlap Management**: Be cautious of overlapping season dates
4. **Active Status**: Only active seasons should be used for planning

### Rice Variety Season Association Management
1. **Prerequisites**: Ensure both rice variety and season exist before creating association
2. **Duplicate Check**: System prevents duplicate associations automatically
3. **Risk Assessment**: Set appropriate risk levels based on historical data
4. **Yield Tracking**: Update expected yields based on actual performance
5. **Recommendations**: Mark combinations as recommended only after validation

### Standard Plan Management
1. **Review Before Use**: Always review a standard plan with specific sow date and area before creating production plans
2. **Category Selection**: Choose appropriate category (short-day/long-day) based on cultivation timeline
3. **Cost Estimation**: Use review endpoint to estimate material costs before implementation
4. **Excel Export**: Export plans with filters for better organization
5. **Updates**: Verify plan is not in use by active production plans before major updates

---

## Changelog

### Version 2.0 (2024-01-28)
- Added full CRUD operations for Material management
- Added full CRUD operations for Rice Variety management
- Added complete Season management API
- Added Rice Variety Season Association API (full CRUD)
- Enhanced Rice Variety GET endpoint to include associated seasons
- Added Material price history management
- Added soft delete functionality for materials and rice varieties
- Improved error handling and validation

### Version 1.1 (2024-01-27)
- Added Standard Plan management endpoints
- Standard Plan Excel export functionality
- Review endpoint for cost and schedule estimation
- Enhanced category-based filtering

### Version 1.0 (2024-01-15)
- Initial API specification
- Material management endpoints
- Rice variety management endpoints
- Excel export/import functionality
- Category-based filtering for rice varieties

---

## Support & Contact

For API support, please contact:
- **Email:** support@riceproduction.vn
- **Documentation:** https://docs.riceproduction.vn

---

**Document Version:** 2.0  
**Last Updated:** January 28, 2024  
**API Version:** v1
