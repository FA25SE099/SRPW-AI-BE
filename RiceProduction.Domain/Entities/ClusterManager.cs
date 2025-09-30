namespace RiceProduction.Domain.Entities;

public class ClusterManager : ApplicationUser
{

    public Guid? ClusterId { get; set; }

    public DateTime? AssignedDate { get; set; }

    [ForeignKey("ClusterId")]
    public Cluster? ManagedCluster { get; set; }
}