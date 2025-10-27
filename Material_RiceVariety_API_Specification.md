# Material & Rice Variety Management API Specification

## Table of Contents
- [Overview](#overview)
- [Material Management API](#material-management-api)
- [Rice Variety Management API](#rice-variety-management-api)
- [Common Response Structure](#common-response-structure)
- [Error Handling](#error-handling)

---

## Overview

This document describes the API endpoints for managing **Materials** (fertilizers, pesticides) and **Rice Varieties** (with categories) in the Rice Production Management System.

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
      "isActive": true
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
      "isActive": true
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

#### Rice Variety Management
- **Duplicate Season Association:** Rice variety already associated with the season
- **Invalid Category:** Category ID doesn't exist
- **Rice Variety Not Found:** Requested variety doesn't exist
- **No Rice Varieties Found:** Export request when no varieties match filters
- **Invalid Search Parameters:** Search term or filter values are invalid

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
4. **Season Association**: Check existing associations before creating new ones

---

## Changelog

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

**Document Version:** 1.0  
**Last Updated:** January 2024  
**API Version:** v1
