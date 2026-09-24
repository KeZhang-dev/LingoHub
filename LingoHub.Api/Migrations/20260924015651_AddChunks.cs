using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LingoHub.Api.Migrations
{
    // ============================================================
    // 文件作用：数据库“迁移”——告诉数据库怎么改结构。（这个文件是 EF Core 自动生成的）
    // 这次的改动：新建 Chunks 表，用来保存 PDF 切出来的文字块。
    // 生成命令：dotnet ef migrations add AddChunks
    // 执行命令：dotnet ef database update   ← 运行后数据库才真的改变
    // ============================================================
    /// <inheritdoc />
    public partial class AddChunks : Migration
    {
        /// <inheritdoc />
        // Up = 升级：执行 database update 时运行
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Chunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    ChunkIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chunks", x => x.Id);
                    // 外键：Chunk.DocumentId 必须是一个存在的 Document.Id。
                    // Cascade：删除文档时，它的块也一起删除
                    table.ForeignKey(
                        name: "FK_Chunks_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // 索引：让“按 DocumentId 查某个文档的所有块”更快
            migrationBuilder.CreateIndex(
                name: "IX_Chunks_DocumentId",
                table: "Chunks",
                column: "DocumentId");
        }

        /// <inheritdoc />
        // Down = 撤销：回退到上一个迁移时运行（删除 Chunks 表）
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Chunks");
        }
    }
}
