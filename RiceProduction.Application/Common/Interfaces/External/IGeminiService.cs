namespace RiceProduction.Application.Common.Interfaces.External;

public interface IGeminiService
{
    /// <summary>
    /// Gửi câu hỏi đến Gemini AI và nhận phản hồi
    /// </summary>
    /// <param name="prompt">Câu hỏi hoặc nội dung cần AI xử lý</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Phản hồi từ Gemini AI</returns>
    Task<string> GenerateContentAsync(string prompt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gửi câu hỏi với ngữ cảnh đến Gemini AI
    /// </summary>
    /// <param name="prompt">Câu hỏi hoặc nội dung cần AI xử lý</param>
    /// <param name="context">Ngữ cảnh bổ sung cho AI</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Phản hồi từ Gemini AI</returns>
    Task<string> GenerateContentWithContextAsync(string prompt, string context, CancellationToken cancellationToken = default);
}
