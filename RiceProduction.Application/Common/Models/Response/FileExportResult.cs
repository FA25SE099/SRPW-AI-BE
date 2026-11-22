namespace RiceProduction.Application.Common.Models.Response;

public class FileExportResult
{
    public byte[] FileContents { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
}
