using Alexandria.Crawler.Services;
using Microsoft.AspNetCore.Mvc;

namespace Alexandria.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CrawlerController : Controller
    {
        private readonly ICrawlerService _crawlerService;

        public CrawlerController(ICrawlerService crawlerService)
        {
            _crawlerService = crawlerService;
        }

        [HttpGet("single")]
        public async Task<IActionResult> CrawlSingle([FromQuery] string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return BadRequest("URL is required");

            var result = await _crawlerService.CrawlAsync(url);
            return Ok(result);
        }

        [HttpPost("deep")]
        public async Task<IActionResult> CrawlDeep(
            [FromQuery] string startUrl,
            [FromQuery] int maxDepth = 2,
            [FromQuery] int maxPages = 50)
        {
            if (string.IsNullOrWhiteSpace(startUrl))
                return BadRequest("Start URL is required");

            var results = await _crawlerService.CrawlDeepAsync(startUrl, maxDepth, maxPages);

            return Ok(new
            {
                TotalPages = results.Count,
                SuccessfulPages = results.Count(r => r.Success),
                Results = results
            });
        }
    }
}
