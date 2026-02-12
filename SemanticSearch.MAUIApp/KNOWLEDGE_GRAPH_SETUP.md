# 🕸️ Knowledge Graph Setup Guide

## Übersicht

Sie erhalten ein vollständiges Knowledge Graph System mit:
- **Entity Management**: Personen, Organisationen, Events
- **Relation Tracking**: Arbeitsbeziehungen, Mitgliedschaften, etc.
- **Automatische Extraktion**: NER aus Dokumenten
- **SQLite-Persistierung**: Lokale Datenbank
- **Blazor UI**: Moderne Weboberfläche

---

## 📦 Benötigte NuGet-Pakete

```bash
# Entity Framework Core (für DbContext)
dotnet add package Microsoft.EntityFrameworkCore.Sqlite --version 8.0.0
dotnet add package Microsoft.EntityFrameworkCore.Tools --version 8.0.0

# Bereits installiert (für Semantic Search)
# - Microsoft.Data.Sqlite
# - Microsoft.ML.OnnxRuntime
# - FuzzySharp
```

---

## 🗂️ Dateistruktur

```
YourProject.Shared/
├── Persistence/
│   ├── MinervaDbContext.cs              ← DbContext
│   ├── Entities/
│   │   ├── BaseEntity.cs                ← Ihre vorhandenen Dateien
│   │   ├── Person.cs
│   │   ├── Organization.cs
│   │   ├── Event.cs
│   │   └── PostalAddress.cs             ← Neu (falls nicht vorhanden)
│   └── Relations/
│       ├── Relation.cs                  ← Ihre vorhandenen Dateien
│       └── OrganizationMember.cs
│
├── Services/
│   ├── NerService.cs                    ← Neu
│   ├── EntityPersistenceService.cs      ← Neu
│   ├── EmbeddingService.cs              ← Bereits vorhanden
│   └── SemanticSearchManager.cs         ← Bereits vorhanden

YourProject.Client/
└── Pages/
    └── KnowledgeGraphPage.razor         ← Neu

YourProject.MAUI/
├── MauiProgram.cs                       ← Update erforderlich
└── Services/
    └── MauiFilePickerService.cs         ← Bereits vorhanden
```

---

## 🛠️ Installation Schritt für Schritt

### Schritt 1: NuGet-Pakete installieren

Im **Shared-Projekt**:

```bash
dotnet add package Microsoft.EntityFrameworkCore.Sqlite --version 8.0.0
dotnet add package Microsoft.EntityFrameworkCore.Tools --version 8.0.0
```

### Schritt 2: Dateien kopieren

1. **MinervaDbContext.cs** → `YourProject.Shared/Persistence/`
2. **NerService.cs** → `YourProject.Shared/Services/`
3. **EntityPersistenceService.cs** → `YourProject.Shared/Services/`
4. **KnowledgeGraphPage.razor** → `YourProject.Client/Pages/`

### Schritt 3: MauiProgram.cs aktualisieren

Ersetzen Sie Ihre `MauiProgram.cs` mit der aktualisierten Version, oder fügen Sie folgende Abschnitte hinzu:

```csharp
// DbContext registrieren
var dbPath = Path.Combine(FileSystem.AppDataDirectory, "minerva_knowledge_graph.db");

builder.Services.AddDbContext<MinervaDbContext>(options =>
{
    options.UseSqlite($"Data Source={dbPath}");
});

// Database sicherstellen
builder.Services.AddScoped(sp =>
{
    var context = sp.GetRequiredService<MinervaDbContext>();
    context.Database.EnsureCreated();
    return context;
});

// NER Services
builder.Services.AddSingleton<NerService>();
builder.Services.AddScoped<EntityPersistenceService>();
```

### Schritt 4: Navigation erweitern

In Ihrer `NavMenu.razor`:

```razor
<div class="nav-item px-3">
    <NavLink class="nav-link" href="knowledge-graph">
        <span class="bi bi-diagram-3-nav-menu" aria-hidden="true"></span> Knowledge Graph
    </NavLink>
</div>
```

### Schritt 5: PostalAddress-Klasse prüfen

Falls Sie noch keine `PostalAddress`-Klasse haben, ist eine Minimal-Version im `MinervaDbContext.cs` enthalten. Wenn Sie bereits eine haben, passen Sie ggf. den Namespace an.

---

## 🚀 Erste Schritte

### 1. App starten und Datenbank initialisieren

Beim ersten Start wird automatisch die SQLite-Datenbank erstellt:
```
C:\Users\YourName\AppData\Local\YourApp\minerva_knowledge_graph.db
```

### 2. Zur Knowledge Graph Seite navigieren

Gehen Sie zu: **Knowledge Graph** in der Navigation

### 3. Entities manuell hinzufügen (Test)

Sie können zunächst testweise Entities direkt im Code hinzufügen:

```csharp
// In einer Test-Methode oder beim App-Start
using var scope = app.Services.CreateScope();
var dbContext = scope.ServiceProvider.GetRequiredService<MinervaDbContext>();

var person = new Person
{
    Name = "Larry Fink",
    GivenName = "Laurence",
    FamilyName = "Fink",
    EntityType = "Person"
};

var company = new Company
{
    Name = "BlackRock",
    EntityType = "Organization:Company",
    CEO = "Larry Fink"
};

dbContext.Persons.Add(person);
dbContext.Companies.Add(company);
await dbContext.SaveChangesAsync();

// Relation erstellen
var relation = new OrganizationMember
{
    PersonId = person.Id,
    OrganizationId = company.Id,
    Role = "CEO",
    RelationType = "Work:Leadership"
};

dbContext.OrganizationMembers.Add(relation);
await dbContext.SaveChangesAsync();
```

### 4. NER-Extraktion testen

```csharp
var nerService = app.Services.GetRequiredService<NerService>();
var persistenceService = app.Services.GetRequiredService<EntityPersistenceService>();

var text = @"Larry Fink is the CEO of BlackRock, the world's largest asset manager.
    BlackRock was founded in 1988 and manages over $10 trillion in assets.";

// Entities extrahieren
var entities = await nerService.ExtractEntitiesAsync(text, useLLM: false);

// Relationen extrahieren
var relations = await nerService.ExtractRelationsAsync(text, entities, useLLM: false);

// In DB speichern
var mapping = await persistenceService.PersistEntitiesAsync(entities, "test-doc-1");
await persistenceService.PersistRelationsAsync(relations, mapping);
```

---

## 🎨 UI-Features

### Entities Tab
- **Grid-Ansicht** aller Entities
- **Filter** nach Typ (Person, Company, Bank, etc.)
- **Suche** nach Name
- **Detail-Modal** zeigt alle Informationen und Relationen

### Relationen Tab
- **Liste** aller Relationen
- **Filter** nach Relationstyp
- **Zeitangaben** (FromDate → ToDate)
- **Rollen-Anzeige** bei OrganizationMember

### Extraktion Tab
- **Automatische Extraktion** aus indexierten Dokumenten
- **LLM-Option** für bessere Qualität
- **Ergebnis-Anzeige** mit Statistiken

---

## 🔧 Erweiterte Konfiguration

### Migrations verwenden (statt EnsureCreated)

Für Produktionsumgebungen empfohlen:

```bash
# Initial Migration erstellen
dotnet ef migrations add InitialCreate --project YourProject.Shared

# Datenbank aktualisieren
dotnet ef database update --project YourProject.Shared
```

In `MauiProgram.cs` ändern:
```csharp
context.Database.Migrate(); // statt EnsureCreated()
```

### Claude API für bessere NER

Setzen Sie einen API-Key:

```bash
# Windows
setx ANTHROPIC_API_KEY "sk-ant-..."

# Linux/Mac
export ANTHROPIC_API_KEY="sk-ant-..."
```

Dann in der UI: ✅ **LLM-basierte Extraktion verwenden**

### Automatische Extraktion beim Indexieren

Erweitern Sie `SemanticSearchManager.IndexDocumentAsync`:

```csharp
// Nach erfolgreicher Indexierung
if (result.Success)
{
    var nerService = serviceProvider.GetRequiredService<NerService>();
    var persistenceService = serviceProvider.GetRequiredService<EntityPersistenceService>();
    
    var entities = await nerService.ExtractEntitiesAsync(text);
    var relations = await nerService.ExtractRelationsAsync(text, entities);
    
    var mapping = await persistenceService.PersistEntitiesAsync(entities, result.DocumentId);
    await persistenceService.PersistRelationsAsync(relations, mapping);
}
```

---

## 📊 Datenbank-Schema

```sql
-- BaseEntity (mit TPH - Table-Per-Hierarchy)
BaseEntities
├── Id (PK)
├── Discriminator (Person|Organization|Event|...)
├── EntityType
├── Name
├── Description
└── MetaDataJson

-- Person (erbt von BaseEntity)
├── BirthDate
├── DeathDate
├── GivenName
├── FamilyName
└── Gender

-- Organization (erbt von BaseEntity)
├── FoundingDate
├── DissolutionDate
├── VatID
└── ParentOrganizationId

-- Relations
Relations
├── Id (PK)
├── SourceEntityId (FK → BaseEntity)
├── TargetEntityId (FK → BaseEntity)
├── RelationType
├── FromDate
├── ToDate
└── Context

-- OrganizationMember (erbt von Relation)
├── PersonId (FK → Person)
├── OrganizationId (FK → Organization)
└── Role
```

---

## 🐛 Troubleshooting

### "Table already exists" Fehler

Löschen Sie die Datenbank und lassen Sie sie neu erstellen:
```bash
# Windows
del "%LOCALAPPDATA%\YourApp\minerva_knowledge_graph.db"
```

### Entities werden nicht angezeigt

Prüfen Sie:
1. Ist die Datenbank erstellt? (Pfad in Logs prüfen)
2. Sind Entities in der DB? (SQLite Browser öffnen)
3. Wird `DbContext` korrekt injected?

### NER findet keine Entities

Pattern-basierte NER ist limitiert. Für bessere Ergebnisse:
- Aktivieren Sie LLM-Extraktion
- Oder: Trainieren Sie ein eigenes NER-Modell

---

## 🎯 Nächste Schritte

1. ✅ **Testen** Sie die UI mit manuell erstellten Entities
2. ✅ **Indexieren** Sie ein paar Dokumente
3. ✅ **Extrahieren** Sie Entities aus den Dokumenten
4. ✅ **Visualisieren** Sie den Knowledge Graph (optional: D3.js/Cytoscape.js)
5. ✅ **Exportieren** Sie Daten (z.B. als JSON für weitere Analyse)

Viel Erfolg beim Aufbau Ihres Knowledge Graphs! 🚀
