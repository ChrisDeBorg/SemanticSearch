using Alexandria.Crawler.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.SqlServer; // <-- Hinzugefügt für UseSqlServer
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace Alexandria.Crawler.Data;

// Design-Time DbContext Factory
public class CrawlerDbContextFactory : IDesignTimeDbContextFactory<CrawlerDbContext>
{
    public CrawlerDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__CrawlerDb")
                                   ?? Environment.GetEnvironmentVariable("CrawlerDb")
                                   // Fallback: harte ConnectionString für lokale Entwicklung
                                   ?? "Server=.\\SQLEXPRESS;Database=AlexandriaCrawler;Trusted_Connection=True;TrustServerCertificate=True";

        var optionsBuilder = new DbContextOptionsBuilder<CrawlerDbContext>();
        optionsBuilder.UseSqlServer(
            connectionString
        );


        //var config = new ConfigurationBuilder()
        //.SetBasePath(Directory.GetCurrentDirectory())
        //.AddJsonFile("appsettings.json")
        //    .Build();

        //var connectionString = config.GetConnectionString("CrawlerDb");

        //var optionsBuilder = new DbContextOptionsBuilder<CrawlerDbContext>();
        //optionsBuilder.UseSqlServer(connectionString);


        var context = new CrawlerDbContext(optionsBuilder.Options);

        //try
        //{
        //    // DEBUG: versuche echte Verbindung zu öffnen, damit wir die innere Exception sehen
        //    var dbConn = context.Database.GetDbConnection();
        //    Console.WriteLine("DesignTime: Versuch, DB-Verbindung zu öffnen...");
        //    dbConn.Open();
        //    Console.WriteLine("DesignTime: Verbindung geöffnet.");
        //    dbConn.Close();
        //}
        //catch (Exception ex)
        //{
        //    // Detaillierte Ausgabe — wird bei dotnet ef --verbose sichtbar
        //    Console.Error.WriteLine("DesignTime: Verbindung fehlgeschlagen: " + ex);
        //    // Wichtig: weiterwerfen, damit dotnet ef die Fehlermeldung erhält
        //    throw;
        //}

        return context;
    }
}
