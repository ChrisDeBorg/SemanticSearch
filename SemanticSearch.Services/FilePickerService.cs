namespace SemanticSearch.Services;

//public interface IFilePickerService
//{
//    Task<FileResult?> PickAsync(PickOptions? options = null);
//    Task<IEnumerable<FileResult>> PickMultipleAsync(PickOptions? options = null);
//}
public interface IFilePickerService
{
    /// <summary>
    /// Einzelne Datei auswählen
    /// </summary>
    Task<PickedFileResult?> PickSingleFileAsync();

    /// <summary>
    /// Mehrere Dateien auswählen
    /// </summary>
    Task<List<PickedFileResult>> PickMultipleFilesAsync();
}

/// <summary>
/// Plattform-unabhängiges Ergebnis-Modell für eine ausgewählte Datei.
/// Keine Abhängigkeit auf MAUI-Typen wie FileResult.
/// </summary>
public class PickedFileResult
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public long Length { get; set; }
    public string Extension { get; set; } = string.Empty;

    /// <summary>
    /// Gibt einen lesbaren Stream zur Datei zurück.
    /// Muss vom Aufrufer geschlossen werden.
    /// </summary>
    public Func<Task<Stream>>? OpenStreamAsync { get; set; }
}

//public class FilePickerService : IFilePickerService
//{
//    public async Task<FileResult?> PickAsync(PickOptions? options = null)
//    {
//        try
//        {
//            return await FilePicker.Default.PickAsync(options);
//        }
//        catch (Exception)
//        {
//            return null;
//        }
//    }

//    public async Task<IEnumerable<FileResult>> PickMultipleAsync(PickOptions? options = null)
//    {
//        try
//        {
//            return await FilePicker.Default.PickMultipleAsync(options);
//        }
//        catch (Exception)
//        {
//            return Array.Empty<FileResult>();
//        }
//    }
//}