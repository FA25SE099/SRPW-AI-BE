namespace RiceProduction.Domain.Enums;

public enum FarmerStatus
{
    /// <summary>
    /// Farmer is normal and can be grouped
    /// </summary>
    Normal,
    
    /// <summary>
    /// Farmer has been warned due to lateness or other issues
    /// </summary>
    Warned,
    
    /// <summary>
    /// Farmer is temporarily not allowed to be grouped
    /// </summary>
    NotAllowed,
    
    /// <summary>
    /// Farmer has resigned but has a chance to keep cooperating after making a promise
    /// </summary>
    Resigned
}
