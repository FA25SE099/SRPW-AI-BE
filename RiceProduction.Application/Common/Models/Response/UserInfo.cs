namespace RiceProduction.Application.Common.Models.Response;

public class UserInfo
{
    public string Id { get; set; } = string.Empty; // Will contain Guid.ToString()
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
}