using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace LingoHub.Api.Migrations
{
    // ============================================================
    // 文件作用：数据库“迁移”（EF Core 自动生成）。
    // 这次的改动：给 Chunks 表加两列
    //   Embedding      vector(1024)：这块文字的向量（pgvector 类型）
    //   EmbeddingModel：向量是哪个模型生成的
    // 两列都可以为空 → 已有的块不受影响，以后再补向量。
    // ============================================================
    /// <inheritdoc />
    public partial class AddChunkEmbeddings : Migration
    {
        /// <inheritdoc />
        // Up = 升级：执行 dotnet ef database update 时运行
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Vector>(
                name: "Embedding",
                table: "Chunks",
                type: "vector(1024)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmbeddingModel",
                table: "Chunks",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        // Down = 撤销：删除这两列
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Embedding",
                table: "Chunks");

            migrationBuilder.DropColumn(
                name: "EmbeddingModel",
                table: "Chunks");
        }
    }
}
