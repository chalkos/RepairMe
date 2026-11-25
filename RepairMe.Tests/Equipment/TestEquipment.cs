namespace RepairMe.Tests;

internal static class TestEquipment
{
    public static EquipmentData Build(uint[] ids, ushort[] condition, ushort[] spiritbond)
        => new(Pad(ids), Pad(condition), Pad(spiritbond));

    public static T[] Pad<T>(T[] values)
    {
        var padded = new T[RepairMeConstants.Equipment.ContainerSize];
        Array.Copy(values, padded, values.Length);
        return padded;
    }
}
