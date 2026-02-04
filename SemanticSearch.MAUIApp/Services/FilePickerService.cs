using SemanticSearch.Services;

namespace SemanticSearch.MAUIApp.Services;

//public interface IFilePickerService
//{
//    Task<FileResult?> PickAsync(PickOptions? options = null);
//    Task<IEnumerable<FileResult>> PickMultipleAsync(PickOptions? options = null);
//}

public class FilePickerService : IFilePickerService
{
    // Unterstützte Dateitypen zentral definieren
    private static readonly FilePickerFileType SupportedDocumentTypes = new(
        new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            {
                DevicePlatform.WinUI,
                new[] { ".pdf", ".epub", ".txt", ".md", ".html", ".htm" }
            },
            {
                DevicePlatform.macOS,
                new[] { "pdf", "epub", "txt", "md", "html", "htm" }
            },
            {
                DevicePlatform.iOS,
                new[] { "public.pdf", "public.epub", "public.text", "public.html" }
            },
            {
                DevicePlatform.Android,
                new[] { "application/pdf", "application/epub+zip", "text/plain", "text/html" }
            }
        });

    public async Task<PickedFileResult?> PickSingleFileAsync()
    {
        try
        {
            var options = new PickOptions
            {
                PickerTitle = "Dokument auswählen",
                FileTypes = SupportedDocumentTypes
            };

            var result = await FilePicker.Default.PickAsync(options);
            return result != null ? await MapFileResultAsync(result) : null;
        }
        catch (Exception)
        {
            // Unter bestimmten Umständen (z.B. Benutzer bricht ab) wird eine Exception geworfen
            return null;
        }
    }

    public async Task<List<PickedFileResult>> PickMultipleFilesAsync()
    {
        try
        {
            var options = new PickOptions
            {
                PickerTitle = "Dokumente auswählen",
                FileTypes = SupportedDocumentTypes
            };

            var results = await FilePicker.Default.PickMultipleAsync(options);

            var mapped = new List<PickedFileResult>();

            if (results != null)
            {
                foreach (var file in results)
                {
                    mapped.Add(await MapFileResultAsync(file));
                }
            }

            return mapped;
        }
        catch (Exception)
        {
            return new List<PickedFileResult>();
        }
    }

    /// <summary>
    /// Konvertiert MAUI's FileResult in unsere plattform-unabhängige Klasse.
    /// FullPath wird auf Windows direkt vom FileResult geliefert.
    /// Auf iOS/Android muss die Datei erst in einen lokalen Pfad kopiert werden.
    /// </summary>
    private static async Task<PickedFileResult> MapFileResultAsync(FileResult fileResult)
    {
        // FullPath kann auf mobilen Plattformen leer sein → Datei kopieren
        var fullPath = fileResult.FullPath;

        if (string.IsNullOrEmpty(fullPath))
        {
            // Kopiere Datei in AppDataDirectory (iOS/Android)
            fullPath = await CopyToLocalAsync(fileResult);
        }

        var fileInfo = new FileInfo(fullPath);

        return new PickedFileResult
        {
            Name = fileResult.FileName,
            FullPath = fullPath,
            Length = fileInfo.Exists ? fileInfo.Length : 0,
            Extension = Path.GetExtension(fileResult.FileName).ToLowerInvariant(),
            OpenStreamAsync = async () =>
            {
                var stream = await fileResult.OpenReadAsync();
                return stream;
            }
        };
    }

    /// <summary>
    /// Kopiert eine Datei aus dem Plattform-spezifischen Speicher in das AppDataDirectory.
    /// Nötig auf iOS/Android, wo FullPath nicht direkt verfügbar ist.
    /// </summary>
    private static async Task<string> CopyToLocalAsync(FileResult fileResult)
    {
        var targetDirectory = Path.Combine(FileSystem.AppDataDirectory, "picked");
        Directory.CreateDirectory(targetDirectory);

        var targetPath = Path.Combine(targetDirectory, fileResult.FileName);

        using (var sourceStream = await fileResult.OpenReadAsync())
        using (var targetStream = File.OpenWrite(targetPath))
        {
            await sourceStream.CopyToAsync(targetStream);
        }

        return targetPath;
    }

    //public async Task<FileResult?> PickAsync(PickOptions? options = null)
    //{
    //    try
    //    {
    //        return await FilePicker.Default.PickAsync(options);
    //    }
    //    catch (Exception)
    //    {
    //        return null;
    //    }
    //}

    //public async Task<IEnumerable<FileResult>> PickMultipleAsync(PickOptions? options = null)
    //{
    //    try
    //    {
    //        return await FilePicker.Default.PickMultipleAsync(options);
    //    }
    //    catch (Exception)
    //    {
    //        return Array.Empty<FileResult>();
    //    }
    //}
}