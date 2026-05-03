using System.Collections.Generic;
using System.Linq;
using System.Numerics;

public static class ItemExtraMeshDecoder
{
    // EXTRA MESH IDs (bits 60-61) - ENCODED VALUES, not bitwise flags!
    // Use: meshType == ixmesh_carry (equality), NOT: (meshType & ixmesh_carry)
    private static readonly Dictionary<string, BigInteger> ExtraMeshTypes = new Dictionary<string, BigInteger>
    {
        { "ixmesh_inventory", new BigInteger(0x1000000000000000) },
        { "ixmesh_flying_ammo", new BigInteger(0x2000000000000000) },
        { "ixmesh_carry", new BigInteger(0x3000000000000000) },
    };

    // Mask for extracting bits 60-61 (the extra mesh ID field)
    private static readonly BigInteger EXTRA_MESH_MASK = new BigInteger(0x3000000000000000);

    /// <summary>
    /// Gets the mesh usage type from bits 60-61 as a MeshUsageType enum
    /// </summary>
    public static MountAndBlade.Data.MBItemData.ItemMesh.MeshUsageType GetMeshUsageType(string value)
    {
        // Extract bits 60-61 as encoded value
        BigInteger meshValue = BigInteger.Parse(value) & EXTRA_MESH_MASK;
        
        // Map to MeshUsageType enum
        if (meshValue == BigInteger.Zero)
            return MountAndBlade.Data.MBItemData.ItemMesh.MeshUsageType.Default;
        else if (meshValue == ExtraMeshTypes["ixmesh_inventory"])
            return MountAndBlade.Data.MBItemData.ItemMesh.MeshUsageType.Inventory;
        else if (meshValue == ExtraMeshTypes["ixmesh_flying_ammo"])
            return MountAndBlade.Data.MBItemData.ItemMesh.MeshUsageType.FlyingAmmo;
        else if (meshValue == ExtraMeshTypes["ixmesh_carry"])
            return MountAndBlade.Data.MBItemData.ItemMesh.MeshUsageType.Carry;
        
        // Default fallback for unknown values
        return MountAndBlade.Data.MBItemData.ItemMesh.MeshUsageType.Default;
    }
    
    /// <summary>
    /// Gets the extra mesh type from bits 60-61 (ENCODED VALUE, not bitwise flag!)
    /// </summary>
    public static string GetExtraMeshType(BigInteger value)
    {
        // Extract bits 60-61 as encoded value
        BigInteger meshValue = value & EXTRA_MESH_MASK;
        
        // Find matching type by EQUALITY, not bitwise AND
        var meshType = ExtraMeshTypes.FirstOrDefault(x => x.Value == meshValue);
        
        // Return the key, or indicate no extra mesh if value is 0
        if (meshValue == BigInteger.Zero)
            return "ixmesh_none";
            
        return meshType.Key ?? $"ixmesh_unknown_{meshValue:X}";
    }

    /// <summary>
    /// Checks if a value has an extra mesh defined (bits 60-61 are non-zero)
    /// </summary>
    public static bool HasExtraMesh(BigInteger value)
    {
        return (value & EXTRA_MESH_MASK) != BigInteger.Zero;
    }

    /// <summary>
    /// Gets the raw extra mesh value (for debugging/display)
    /// </summary>
    public static BigInteger GetExtraMeshValue(BigInteger value)
    {
        return value & EXTRA_MESH_MASK;
    }

    /// <summary>
    /// Encodes an extra mesh type into a flags value
    /// </summary>
    public static BigInteger EncodeExtraMesh(string meshTypeName)
    {
        if (ExtraMeshTypes.TryGetValue(meshTypeName, out BigInteger meshValue))
            return meshValue;
        
        return BigInteger.Zero; // Return 0 if unknown type
    }

    /// <summary>
    /// Removes extra mesh bits from a value (clears bits 60-61)
    /// </summary>
    public static BigInteger ClearExtraMesh(BigInteger value)
    {
        return value & ~EXTRA_MESH_MASK;
    }

    /// <summary>
    /// Sets/replaces the extra mesh type in a value
    /// </summary>
    public static BigInteger SetExtraMesh(BigInteger value, string meshTypeName)
    {
        // Clear existing extra mesh bits
        BigInteger cleared = ClearExtraMesh(value);
        
        // Add new extra mesh bits
        BigInteger meshBits = EncodeExtraMesh(meshTypeName);
        
        return cleared | meshBits;
    }

    // Static accessors
    public static Dictionary<string, BigInteger> GetExtraMeshTypes() => 
        new Dictionary<string, BigInteger>(ExtraMeshTypes);
    
    public static BigInteger GetExtraMeshMask() => EXTRA_MESH_MASK;
}
