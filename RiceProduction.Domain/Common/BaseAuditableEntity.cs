using Microsoft.AspNetCore.Identity;

namespace RiceProduction.Domain.Common;

public abstract class BaseAuditableEntity 
{
    public Guid Id { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastModified { get; set; }

    public Guid? LastModifiedBy { get; set; }
}
