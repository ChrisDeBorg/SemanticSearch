using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;

namespace Alexandria.Crawler.Services;

public static class CrawlerServiceExtensions
{
    public static IServiceCollection AddCrawlerServices(this IServiceCollection services)
    {
        // HttpClient mit Polly Retry Policy
        services.AddHttpClient("CrawlerClient")
            .ConfigureHttpClient(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Add("User-Agent", "AlexandriaCrawler/1.0");
            })
            .AddPolicyHandler(GetRetryPolicy());

        services.AddSingleton<ICrawlerService, CrawlerService>();

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }
}
