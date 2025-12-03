using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.EmailFeature.Commands.SendEmail;

namespace RiceProduction.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmailController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<EmailController> _logger;

    public EmailController(IMediator mediator, ILogger<EmailController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Send a single email
    /// </summary>
    /// <param name="command">Email sending command</param>
    /// <returns>Result with message ID</returns>
    [HttpPost("send")]
    public async Task<ActionResult<Result<string>>> SendEmail([FromBody] SendEmailCommand command)
    {
        try
        {
            _logger.LogInformation("Send email request received for {To}", command.To);

            var result = await _mediator.Send(command);

            if (!result.Succeeded)
            {
                _logger.LogWarning("Failed to send email to {To}: {Errors}",
                    command.To, string.Join(", ", result.Errors ?? []));
                return BadRequest(result);
            }

            _logger.LogInformation("Email sent successfully to {To}, MessageId: {MessageId}",
                command.To, result.Data);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while sending email to {To}", command.To);
            return StatusCode(500, Result<string>.Failure("An error occurred while processing your request"));
        }
    }

    /// <summary>
    /// Send bulk emails
    /// </summary>
    /// <param name="command">Bulk email command</param>
    /// <returns>Result with batch information</returns>
    [HttpPost("send-bulk")]
    public async Task<ActionResult<Result<string>>> SendBulkEmail([FromBody] SendBulkEmailCommand command)
    {
        try
        {
            _logger.LogInformation("Bulk email request received with {Count} recipients",
                command.EmailRequests?.Count ?? 0);

            var result = await _mediator.Send(command);

            if (!result.Succeeded)
            {
                _logger.LogWarning("Failed to send bulk email: {Errors}",
                    string.Join(", ", result.Errors ?? []));
                return BadRequest(result);
            }

            _logger.LogInformation("Bulk email batch created successfully: {BatchId}", result.Data);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while sending bulk email");
            return StatusCode(500, Result<string>.Failure("An error occurred while processing your request"));
        }
    }

    /// <summary>
    /// Send password reset email
    /// </summary>
    /// <param name="command">Password reset email command</param>
    /// <returns>Result with message ID</returns>
    [HttpPost("send-password-reset")]
    public async Task<ActionResult<Result<string>>> SendPasswordResetEmail([FromBody] SendPasswordResetEmailCommand command)
    {
        try
        {
            _logger.LogInformation("Password reset email request received for {Email}", command.Email);

            var result = await _mediator.Send(command);

            if (!result.Succeeded)
            {
                _logger.LogWarning("Failed to send password reset email to {Email}: {Errors}",
                    command.Email, string.Join(", ", result.Errors ?? []));
                return BadRequest(result);
            }

            _logger.LogInformation("Password reset email sent successfully to {Email}, MessageId: {MessageId}",
                command.Email, result.Data);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while sending password reset email to {Email}", command.Email);
            return StatusCode(500, Result<string>.Failure("An error occurred while processing your request"));
        }
    }

    /// <summary>
    /// Send templated email
    /// </summary>
    /// <param name="command">Templated email command</param>
    /// <returns>Result with message ID</returns>
    [HttpPost("send-template")]
    public async Task<ActionResult<Result<string>>> SendTemplatedEmail([FromBody] SendTemplatedEmailCommand command)
    {
        try
        {
            _logger.LogInformation("Templated email request received for {To} with template {Template}",
                command.To, command.TemplateName);

            var emailCommand = new SendEmailCommand
            {
                To = command.To,
                Cc = command.Cc,
                Bcc = command.Bcc,
                Subject = command.Subject,
                EmailType = command.EmailType,
                Campaign = command.Campaign,
                Priority = command.Priority,
                TemplateName = command.TemplateName,
                TemplateData = command.TemplateData,
                SaveToDatabase = command.SaveToDatabase
            };

            var result = await _mediator.Send(emailCommand);

            if (!result.Succeeded)
            {
                _logger.LogWarning("Failed to send templated email to {To}: {Errors}",
                    command.To, string.Join(", ", result.Errors ?? []));
                return BadRequest(result);
            }

            _logger.LogInformation("Templated email sent successfully to {To}, MessageId: {MessageId}",
                command.To, result.Data);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while sending templated email to {To}", command.To);
            return StatusCode(500, Result<string>.Failure("An error occurred while processing your request"));
        }
    }

    /// <summary>
    /// Get email batch status
    /// </summary>
    /// <param name="batchId">Batch ID to check status</param>
    /// <returns>Batch status information</returns>
    [HttpGet("batch/{batchId:guid}/status")]
    public async Task<ActionResult<Result<object>>> GetBatchStatus(Guid batchId)
    {
        try
        {
            var query = new GetEmailBatchStatusQuery { BatchId = batchId };
            var result = await _mediator.Send(query);

            if (!result.Succeeded)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving batch status for {BatchId}", batchId);
            return StatusCode(500, Result<object>.Failure("An error occurred while processing your request"));
        }
    }

    /// <summary>
    /// Get email history with pagination
    /// </summary>
    /// <param name="query">Email history query parameters</param>
    /// <returns>Paginated email history</returns>
    [HttpPost("get-history")]
    public async Task<ActionResult<PagedResult<List<object>>>> GetEmailHistory([FromBody] GetEmailHistoryQuery query)
    {
        try
        {
            var result = await _mediator.Send(query);

            if (!result.Succeeded)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while getting email history");
            return StatusCode(500, "An error occurred while processing your request");
        }
    }

    /// <summary>
    /// Test email connectivity
    /// </summary>
    /// <param name="command">Test email command</param>
    /// <returns>Test result</returns>
    [HttpPost("test")]
    public async Task<ActionResult<Result<string>>> TestEmail([FromBody] TestEmailCommand command)
    {
        try
        {
            _logger.LogInformation("Test email request received for {To}", command.To);

            var emailCommand = new SendEmailCommand
            {
                To = command.To,
                Subject = "Test Email from Rice Production System",
                HtmlBody = "<h2>Test Email</h2><p>This is a test email to verify email functionality.</p>",
                TextBody = "Test Email\n\nThis is a test email to verify email functionality.",
                EmailType = "test",
                SaveToDatabase = false
            };

            var result = await _mediator.Send(emailCommand);

            if (!result.Succeeded)
            {
                _logger.LogWarning("Failed to send test email to {To}: {Errors}",
                    command.To, string.Join(", ", result.Errors ?? []));
                return BadRequest(result);
            }

            _logger.LogInformation("Test email sent successfully to {To}, MessageId: {MessageId}",
                command.To, result.Data);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while sending test email to {To}", command.To);
            return StatusCode(500, Result<string>.Failure("An error occurred while processing your request"));
        }
    }
}

// Supporting command classes
public class SendBulkEmailCommand : IRequest<Result<string>>
{
    public List<SimpleEmailRequest> EmailRequests { get; set; } = [];
    public string? BatchName { get; set; }
    public string? Description { get; set; }
    public string EmailType { get; set; } = "bulk";
    public string? Campaign { get; set; }
    public int MaxConcurrency { get; set; } = 5;
    public int MaxRetries { get; set; } = 3;
}

public class SendPasswordResetEmailCommand : IRequest<Result<string>>
{
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string ResetToken { get; set; } = string.Empty;
}

public class SendTemplatedEmailCommand : IRequest<Result<string>>
{
    public string To { get; set; } = string.Empty;
    public string? Cc { get; set; }
    public string? Bcc { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public object? TemplateData { get; set; }
    public string EmailType { get; set; } = "template";
    public string? Campaign { get; set; }
    public int Priority { get; set; } = 0;
    public bool SaveToDatabase { get; set; } = true;
}

public class TestEmailCommand : IRequest<Result<string>>
{
    public string To { get; set; } = string.Empty;
}

public class GetEmailBatchStatusQuery : IRequest<Result<object>>
{
    public Guid BatchId { get; set; }
}

public class GetEmailHistoryQuery : IRequest<PagedResult<List<object>>>
{
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? EmailType { get; set; }
    public string? Status { get; set; }
    public string? Campaign { get; set; }
    public string? Search { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

