# Hướng dẫn tích hợp Gemini AI vào dự án Rice Production

## 📋 Tổng quan
Hướng dẫn này mô tả cách tích hợp Google Gemini AI API vào dự án Rice Production để cung cấp chức năng gợi ý thông minh.

## 🔑 Bước 1: Lấy Gemini API Key

1. Truy cập [Google AI Studio](https://makersuite.google.com/app/apikey)
2. Đăng nhập bằng tài khoản Google
3. Click "Create API Key"
4. Copy API key vừa tạo

## ⚙️ Bước 2: Cấu hình API Key

Mở file `appsettings.json` hoặc `appsettings.Development.json` và cập nhật:

```json
"GeminiApi": {
  "ApiKey": "YOUR_GEMINI_API_KEY_HERE",
  "BaseUrl": "https://generativelanguage.googleapis.com",
  "Model": "gemini-1.5-flash"
}
```

**Lưu ý:** 
- Thay `YOUR_GEMINI_API_KEY_HERE` bằng API key thực của bạn
- Không commit API key lên Git! Sử dụng User Secrets trong môi trường development:

```bash
dotnet user-secrets set "GeminiApi:ApiKey" "your-actual-api-key"
```

## 🚀 Bước 3: Các API Endpoints đã được tạo

### 3.1 API gợi ý đơn giản
```http
POST /api/ai/suggest
Content-Type: application/json

{
  "prompt": "Làm thế nào để chăm sóc cây lúa trong giai đoạn đẻ nhánh?"
}
```

### 3.2 API gợi ý với ngữ cảnh
```http
POST /api/ai/suggest-with-context
Content-Type: application/json

{
  "prompt": "Nên bón phân gì?",
  "context": "Cây lúa đang trong giai đoạn trỗ bông, thời tiết mưa nhiều"
}
```

### 3.3 API gợi ý chăm sóc cây lúa
```http
POST /api/ai/rice-care-suggestion
Content-Type: application/json

{
  "riceVariety": "OM 5451",
  "growthStage": "Đẻ nhánh",
  "weatherCondition": "Nắng nóng, 35°C",
  "issue": "Lá vàng"
}
```

## 📝 Cấu trúc code đã tạo

### Files đã được tạo/cập nhật:

1. **Interface**: `RiceProduction.Application/Common/Interfaces/External/IGeminiService.cs`
   - Định nghĩa contract cho Gemini service

2. **Implementation**: `RiceProduction.Infrastructure/Services/GeminiService.cs`
   - Thực thi logic gọi Gemini API
   - Xử lý HTTP requests và responses

3. **Controller**: `RiceProduction.API/Controllers/AiController.cs`
   - 3 endpoints để sử dụng Gemini AI
   - Validation và error handling

4. **Configuration**: 
   - `appsettings.json` - Cấu hình Gemini API
   - `DependencyInjection.cs` - Đăng ký service

## 🧪 Test API

### Sử dụng cURL:
```bash
curl -X POST http://localhost:5000/api/ai/suggest \
  -H "Content-Type: application/json" \
  -d "{\"prompt\": \"Cách phòng trừ sâu cuốn lá cho cây lúa?\"}"
```

### Sử dụng Postman:
1. Tạo request mới với method POST
2. URL: `http://localhost:5000/api/ai/suggest`
3. Headers: `Content-Type: application/json`
4. Body (raw JSON):
```json
{
  "prompt": "Hãy tư vấn cách chăm sóc lúa trong mùa mưa"
}
```

## 🔒 Bảo mật

### Production:
- Không lưu API key trong `appsettings.json`
- Sử dụng Azure Key Vault hoặc biến môi trường
- Thêm rate limiting để tránh spam

### Ví dụ với biến môi trường:
```csharp
// Trong GeminiService.cs constructor:
_apiKey = _configuration["GeminiApi:ApiKey"] 
    ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY")
    ?? throw new InvalidOperationException("Gemini API Key không được cấu hình");
```

## 📊 Models được hỗ trợ

Bạn có thể thay đổi model trong `appsettings.json`:

- `gemini-1.5-flash` - Nhanh, phù hợp cho production (mặc định)
- `gemini-1.5-pro` - Chất lượng cao hơn, chậm hơn
- `gemini-1.0-pro` - Phiên bản cũ

## 🛠️ Mở rộng

### Thêm chức năng chat history:
```csharp
public async Task<string> GenerateContentWithHistoryAsync(
    string prompt, 
    List<ChatMessage> history, 
    CancellationToken cancellationToken = default)
{
    var contents = history.Select(h => new
    {
        role = h.Role,
        parts = new[] { new { text = h.Content } }
    }).ToList();
    
    contents.Add(new
    {
        role = "user",
        parts = new[] { new { text = prompt } }
    });
    
    // ... gọi API với history
}
```

### Thêm streaming response:
```csharp
public async IAsyncEnumerable<string> StreamContentAsync(
    string prompt, 
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    var url = $"{_baseUrl}/v1beta/models/{_model}:streamGenerateContent?key={_apiKey}";
    // ... xử lý streaming
}
```

## ❗ Troubleshooting

### Lỗi 400 Bad Request
- Kiểm tra API key có đúng không
- Kiểm tra format request body

### Lỗi 429 Too Many Requests
- Bạn đã vượt quota của Gemini API
- Thêm retry logic hoặc rate limiting

### Lỗi 500 Internal Server Error
- Kiểm tra logs trong `logs/` folder
- Kiểm tra kết nối internet
- Verify Gemini API service đang hoạt động

## 📚 Tài liệu tham khảo

- [Google Gemini API Documentation](https://ai.google.dev/docs)
- [Gemini API Quickstart](https://ai.google.dev/tutorials/rest_quickstart)
- [Available Models](https://ai.google.dev/models/gemini)

## ✅ Checklist

- [ ] Đã lấy Gemini API key
- [ ] Đã cập nhật appsettings.json
- [ ] Đã test endpoint `/api/ai/suggest`
- [ ] Đã test endpoint `/api/ai/suggest-with-context`
- [ ] Đã test endpoint `/api/ai/rice-care-suggestion`
- [ ] Đã thiết lập bảo mật cho production
- [ ] Đã thêm rate limiting (nếu cần)
