namespace RemoteHub;

/// <summary>
/// App identity. The display name is "Remora"; the on-disk storage folder and the DPAPI entropy keep the
/// original "RemoteHub" identifier so existing encrypted vaults keep working across the rename.
/// </summary>
internal static class App
{
    public const string Name = "Remora";
}
