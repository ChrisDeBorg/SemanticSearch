using Alexandria.Crawler.Data;
using Microsoft.EntityFrameworkCore;

namespace Alexandria.Migrations
{
    internal class Program
    {
        static int Main(string[] args)
        {
            Console.WriteLine("Migrations helper project - use 'dotnet ef' commands here");

            try
            {
                // Versucht die Design-Time Factory zu instanziieren und einen DbContext zu erzeugen.
                var factory = new CrawlerDbContextFactory();
                using var ctx = factory.CreateDbContext(args);

                Console.WriteLine("Factory hat DbContext erstellt.");
                Console.WriteLine("ConnectionString: " + ctx.Database.GetDbConnection().ConnectionString);

                // Optional: Test-Verbindung (führt echten Connect aus)
                Console.WriteLine("CanConnect: " + ctx.Database.CanConnect());

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Fehler in DesignTime-Factory: " + ex);
                return 1;
            }
        }
    }
}
