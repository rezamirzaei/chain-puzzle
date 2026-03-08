namespace ChainPuzzle.Desktop;

internal static class StoragePaths
{
    private const string AppDirectoryName = "ChainPuzzle";

    public static string Resolve(string fileName, string? rootDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("A file name is required.", nameof(fileName));
        }

        var root = string.IsNullOrWhiteSpace(rootDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppDirectoryName)
            : rootDirectory;

        return Path.Combine(root, fileName);
    }
}
