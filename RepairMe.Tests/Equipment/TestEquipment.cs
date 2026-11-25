namespace RepairMe.Tests;

internal static class TestEquipment
{
    public static EquipmentData Build(uint[] ids, ushort[] condition, ushort[] spiritbond)
        => new(Pad(ids), Pad(condition), Pad(spiritbond));

    /// <summary>Every slot filled with item 1 at full condition and no spiritbond.</summary>
    public static EquipmentData Full(Action<uint[], ushort[], ushort[]>? customize = null)
    {
        var ids = new uint[RepairMeConstants.Equipment.ContainerSize];
        var condition = new ushort[ids.Length];
        var spiritbond = new ushort[ids.Length];
        for (var i = 0; i < ids.Length; i++) { ids[i] = 1; condition[i] = 30000; }

        customize?.Invoke(ids, condition, spiritbond);
        return new EquipmentData(ids, condition, spiritbond);
    }

    public static T[] Pad<T>(T[] values)
    {
        var padded = new T[RepairMeConstants.Equipment.ContainerSize];
        Array.Copy(values, padded, values.Length);
        return padded;
    }
}
