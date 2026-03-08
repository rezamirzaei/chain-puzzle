using System.Text.Json;

namespace ChainPuzzle.Desktop;

internal static class JsonFileStore
{
    public static void SaveAtomic<TDocument>(string filePath, TDocument document, JsonSerializerOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);

        var directory = Path.GetDirectoryName(filePath)
            ?? throw new InvalidOperationException("A target directory is required.");
        Directory.CreateDirectory(directory);

        var tempFilePath = Path.Combine(directory, $".{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var stream = File.Create(tempFilePath))
            {
                JsonSerializer.Serialize(stream, document, options);
            }

            File.Move(tempFilePath, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }
}
