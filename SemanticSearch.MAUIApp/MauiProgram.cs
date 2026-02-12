using Microsoft.Extensions.Logging;
using Minerva.Persistence;
using SemanticSearch.MAUIApp.Services;
using SemanticSearch.Services;

namespace SemanticSearch.MAUIApp
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();

#if DEBUG
    		builder.Services.AddBlazorWebViewDeveloperTools();
    		builder.Logging.AddDebug();
#endif

            // Pfade für Datenbank und Modell
            var appDataPath = FileSystem.AppDataDirectory;
            var dbPath = Path.Combine(appDataPath, "semantic_search.db");
            var modelPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,  // ← Build-Output-Verzeichnis
                "models",
                "all-MiniLM-L6-v2.onnx"
            );
            //var modelPath = Path.Combine(appDataPath, "models", "all-MiniLM-L6-v2.onnx");

            // Registriere Services als Singleton
            builder.Services.AddSingleton(sp =>
            {
                var manager = new SemanticSearchManager(dbPath, modelPath, embeddingDimension: 384);

                // Initialisiere asynchron beim Start
                Task.Run(async () =>
                {
                    try
                    {
                        await manager.InitializeAsync();
                    }
                    catch (Exception ex)
                    {
                        var logger = sp.GetService<ILogger<SemanticSearchManager>>();
                        logger?.LogError(ex, "Fehler beim Initialisieren des SemanticSearchManagers");
                    }
                });

                return manager;
            });

            // Optionale separate Services falls benötigt
            builder.Services.AddSingleton<IFilePickerService, FilePickerService>();

            // ═══════════════════════════════════════════════════════════
            // NER & Entity Extraction Services
            // ═══════════════════════════════════════════════════════════

            builder.Services.AddSingleton(sp =>
            {
                var embeddingService = new EmbeddingService(modelPath, embeddingDimension: 384);
                return embeddingService;
            });

            builder.Services.AddSingleton(sp =>
            {
                var embeddingService = sp.GetRequiredService<EmbeddingService>();
                var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
                return new NerService(embeddingService, apiKey);
            });

            builder.Services.AddScoped(sp =>
            {
                var dbContext = sp.GetRequiredService<MinervaDbContext>();
                return new EntityPersistenceService(dbContext);
            });

            return builder.Build();
        }
    }
}
