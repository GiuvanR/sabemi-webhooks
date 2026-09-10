using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SabemiWebhooks.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "eventos_log_bruto",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IdTransacao = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IdContrato = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DataPagamento = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StatusBanco = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    RecebidoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StatusProcessamento = table.Column<int>(type: "integer", nullable: false),
                    MensagemErro = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eventos_log_bruto", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "status_contrato",
                columns: table => new
                {
                    IdContrato = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UltimoValorPago = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    UltimaDataPagamento = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UltimoIdTransacao = table.Column<string>(type: "text", nullable: false),
                    StatusAtual = table.Column<int>(type: "integer", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_status_contrato", x => x.IdContrato);
                });

            migrationBuilder.CreateIndex(
                name: "IX_eventos_log_bruto_IdContrato",
                table: "eventos_log_bruto",
                column: "IdContrato");

            migrationBuilder.CreateIndex(
                name: "IX_eventos_log_bruto_StatusProcessamento",
                table: "eventos_log_bruto",
                column: "StatusProcessamento");

            migrationBuilder.CreateIndex(
                name: "ix_eventos_log_bruto_id_transacao",
                table: "eventos_log_bruto",
                column: "IdTransacao",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "eventos_log_bruto");

            migrationBuilder.DropTable(
                name: "status_contrato");
        }
    }
}
