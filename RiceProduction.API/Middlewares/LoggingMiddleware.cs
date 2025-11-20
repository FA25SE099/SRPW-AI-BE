using Microsoft.IO;
using Serilog.Context;
using System.Diagnostics;
using System.Text;

namespace RiceProduction.API.Middlewares
{
    public class LoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<LoggingMiddleware> _logger;
        private readonly RecyclableMemoryStreamManager _memoryManager = new();

        public LoggingMiddleware(RequestDelegate next, ILogger<LoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var requestId = Guid.NewGuid().ToString();
            var stopwatch = Stopwatch.StartNew();

            _logger.LogInformation("→ {Method} {Path}{Query} | RequestId: {RequestId}",
                context.Request.Method,
                context.Request.Path,
                context.Request.QueryString,
                requestId);

            var (requestBody, contentType) = await FormatRequestAsync(context.Request);

            using (LogContext.PushProperty("RequestId", requestId))
            using (LogContext.PushProperty("RequestBody", requestBody))
            using (LogContext.PushProperty("ContentType", contentType))
            {
                Exception? caughtException = null;

                try
                {
                    await _next(context);
                }
                catch (Exception ex)
                {
                    caughtException = ex;
                    // ← We catch it, log it, but DO NOT re-throw
                    // ASP.NET will still set 500 and your exception filters/problem details still work
                }
                finally
                {
                    stopwatch.Stop();

                    var logLevel = caughtException != null
                        ? LogLevel.Error
                        : context.Response.StatusCode >= 400 ? LogLevel.Warning : LogLevel.Information;

                    var message = "← {Method} {Path} → {StatusCode} in {ElapsedMs}ms";

                    if (caughtException != null)
                    {
                        _logger.LogError(caughtException, message,
                            context.Request.Method,
                            context.Request.Path,
                            context.Response.StatusCode,
                            stopwatch.ElapsedMilliseconds);
                    }
                    else
                    {
                        _logger.Log(logLevel, message,
                            context.Request.Method,
                            context.Request.Path,
                            context.Response.StatusCode,
                            stopwatch.ElapsedMilliseconds);
                    }
                }
            }
        }

        // Your beloved method — unchanged
        private async Task<(string body, string contentType)> FormatRequestAsync(HttpRequest request)
        {
            if (request.ContentType?.Contains("multipart/form-data") == true)
            {
                return ("[File Upload - Not Logged]", request.ContentType);
            }

            request.EnableBuffering();
            await using var requestStream = _memoryManager.GetStream();
            await request.Body.CopyToAsync(requestStream);

            var bodyText = ReadStreamInChunks(requestStream);
            request.Body.Seek(0, SeekOrigin.Begin);

            var truncatedBody = bodyText.Length > 5000
                ? bodyText.Substring(0, 5000) + "\n[Truncated...]"
                : bodyText;

            return (truncatedBody, request.ContentType ?? "none");
        }

        private static string ReadStreamInChunks(Stream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);
            using var stringWriter = new StringWriter();
            using var streamReader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            var buffer = new char[4096];
            int charsRead;
            while ((charsRead = streamReader.Read(buffer, 0, buffer.Length)) > 0)
                stringWriter.Write(buffer, 0, charsRead);
            return stringWriter.ToString();
        }
    }
}