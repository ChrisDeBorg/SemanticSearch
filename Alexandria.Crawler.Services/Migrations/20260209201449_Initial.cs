using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Crawler.Services.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CrawlSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    MaxDepth = table.Column<int>(type: "int", nullable: false),
                    MaxPages = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalPagesCrawled = table.Column<int>(type: "int", nullable: false),
                    SuccessfulPages = table.Column<int>(type: "int", nullable: false),
                    FailedPages = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrawlSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrawledPages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    Domain = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StatusCode = table.Column<int>(type: "int", nullable: false),
                    IsSuccess = table.Column<bool>(type: "bit", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CrawledAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CrawlDepth = table.Column<int>(type: "int", nullable: false),
                    CrawlSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrawledPages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CrawledPages_CrawlSessions_CrawlSessionId",
                        column: x => x.CrawlSessionId,
                        principalTable: "CrawlSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ExtractedImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    AltText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExtractedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExtractedImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExtractedImages_CrawledPages_PageId",
                        column: x => x.PageId,
                        principalTable: "CrawledPages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExtractedLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourcePageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    AnchorText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsInternal = table.Column<bool>(type: "bit", nullable: false),
                    ExtractedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExtractedLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExtractedLinks_CrawledPages_SourcePageId",
                        column: x => x.SourcePageId,
                        principalTable: "CrawledPages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PageMetaData",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MetaKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MetaValue = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageMetaData", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PageMetaData_CrawledPages_PageId",
                        column: x => x.PageId,
                        principalTable: "CrawledPages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CrawledPages_ContentHash",
                table: "CrawledPages",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_CrawledPages_CrawledAt",
                table: "CrawledPages",
                column: "CrawledAt");

            migrationBuilder.CreateIndex(
                name: "IX_CrawledPages_CrawlSessionId",
                table: "CrawledPages",
                column: "CrawlSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_CrawledPages_Domain",
                table: "CrawledPages",
                column: "Domain");

            migrationBuilder.CreateIndex(
                name: "IX_CrawledPages_Url",
                table: "CrawledPages",
                column: "Url",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrawlSessions_StartedAt",
                table: "CrawlSessions",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CrawlSessions_Status",
                table: "CrawlSessions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ExtractedImages_PageId",
                table: "ExtractedImages",
                column: "PageId");

            migrationBuilder.CreateIndex(
                name: "IX_ExtractedLinks_SourcePageId",
                table: "ExtractedLinks",
                column: "SourcePageId");

            migrationBuilder.CreateIndex(
                name: "IX_ExtractedLinks_SourcePageId_TargetUrl",
                table: "ExtractedLinks",
                columns: new[] { "SourcePageId", "TargetUrl" });

            migrationBuilder.CreateIndex(
                name: "IX_ExtractedLinks_TargetUrl",
                table: "ExtractedLinks",
                column: "TargetUrl");

            migrationBuilder.CreateIndex(
                name: "IX_PageMetaData_MetaKey",
                table: "PageMetaData",
                column: "MetaKey");

            migrationBuilder.CreateIndex(
                name: "IX_PageMetaData_PageId",
                table: "PageMetaData",
                column: "PageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExtractedImages");

            migrationBuilder.DropTable(
                name: "ExtractedLinks");

            migrationBuilder.DropTable(
                name: "PageMetaData");

            migrationBuilder.DropTable(
                name: "CrawledPages");

            migrationBuilder.DropTable(
                name: "CrawlSessions");
        }
    }
}
