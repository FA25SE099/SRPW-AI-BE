using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.Common.Interfaces.External;

namespace RiceProduction.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AiController : ControllerBase
    {
        private readonly IGeminiService _geminiService;

        public AiController(IGeminiService geminiService)
        {
            _geminiService = geminiService;
        }

        /// <summary>
        /// Gợi ý AI sử dụng Gemini
        /// </summary>
        /// <param name="request">Yêu cầu từ người dùng</param>
        /// <returns>Phản hồi từ AI</returns>
        [HttpPost("suggest")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<AiSuggestionResponse>> GetAiSuggestion([FromBody] AiSuggestionRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Prompt))
            {
                return BadRequest(new { message = "Nội dung câu hỏi không được để trống" });
            }

            try
            {
                var response = await _geminiService.GenerateContentAsync(request.Prompt);
                return Ok(new AiSuggestionResponse
                {
                    Success = true,
                    Content = response,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi gọi Gemini AI",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Gợi ý AI với ngữ cảnh (ví dụ: gợi ý cho cây trồng, thời vụ)
        /// </summary>
        /// <param name="request">Yêu cầu với ngữ cảnh</param>
        /// <returns>Phản hồi từ AI</returns>
        [HttpPost("suggest-with-context")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<AiSuggestionResponse>> GetAiSuggestionWithContext([FromBody] AiSuggestionWithContextRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Prompt))
            {
                return BadRequest(new { message = "Nội dung câu hỏi không được để trống" });
            }

            if (string.IsNullOrWhiteSpace(request.Context))
            {
                return BadRequest(new { message = "Ngữ cảnh không được để trống" });
            }

            try
            {
                var response = await _geminiService.GenerateContentWithContextAsync(request.Prompt, request.Context);
                return Ok(new AiSuggestionResponse
                {
                    Success = true,
                    Content = response,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi gọi Gemini AI",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Gợi ý chăm sóc cây lúa dựa trên tình trạng hiện tại
        /// </summary>
        /// <param name="request">Thông tin về tình trạng cây lúa</param>
        /// <returns>Gợi ý chăm sóc</returns>
        [HttpPost("rice-care-suggestion")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<AiSuggestionResponse>> GetRiceCareSuggestion([FromBody] RiceCareSuggestionRequest request)
        {
            try
            {
                var context = $@"Bạn là chuyên gia nông nghiệp về cây lúa. 
Giống lúa: {request.RiceVariety ?? "Không xác định"}
Giai đoạn sinh trưởng: {request.GrowthStage ?? "Không xác định"}
Thời tiết hiện tại: {request.WeatherCondition ?? "Không xác định"}
Vấn đề gặp phải: {request.Issue ?? "Không có"}";

                var prompt = "Hãy đưa ra gợi ý chăm sóc cụ thể, bao gồm: phân bón, tưới tiêu, phòng trừ sâu bệnh (nếu có).";

                var response = await _geminiService.GenerateContentWithContextAsync(prompt, context);
                return Ok(new AiSuggestionResponse
                {
                    Success = true,
                    Content = response,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi gọi Gemini AI",
                    error = ex.Message
                });
            }
        }
    }

    // DTOs
    public class AiSuggestionRequest
    {
        public string Prompt { get; set; } = string.Empty;
    }

    public class AiSuggestionWithContextRequest
    {
        public string Prompt { get; set; } = string.Empty;
        public string Context { get; set; } = string.Empty;
    }

    public class RiceCareSuggestionRequest
    {
        public string? RiceVariety { get; set; }
        public string? GrowthStage { get; set; }
        public string? WeatherCondition { get; set; }
        public string? Issue { get; set; }
    }

    public class AiSuggestionResponse
    {
        public bool Success { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}

