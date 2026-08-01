using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPS.Admisiones.Infrastructure.Persistence.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "admisiones");

            migrationBuilder.CreateTable(
                name: "Admisiones",
                schema: "admisiones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PacienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoDocumento = table.Column<int>(type: "int", nullable: false),
                    NumeroDocumento = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ValorCopago = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MonedaCopago = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    HistoriaClinicaId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FechaAdmisionUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    IntentosSincronizacion = table.Column<int>(type: "int", nullable: false),
                    SincronizadaEnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MotivoFallo = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Admisiones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "admisiones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreadoEnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcesadoEnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Intentos = table.Column<int>(type: "int", nullable: false),
                    DisponibleDesdeUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UltimoError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pacientes",
                schema: "admisiones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoDocumento = table.Column<int>(type: "int", nullable: false),
                    NumeroDocumento = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Apellido = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pacientes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Atenciones",
                schema: "admisiones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PacienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdmisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Moneda = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    RequiereAuditoria = table.Column<bool>(type: "bit", nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Atenciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Atenciones_Pacientes_PacienteId",
                        column: x => x.PacienteId,
                        principalSchema: "admisiones",
                        principalTable: "Pacientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Admisiones_Estado_Pendientes",
                schema: "admisiones",
                table: "Admisiones",
                column: "Estado",
                filter: "[Estado] <> 2");

            migrationBuilder.CreateIndex(
                name: "IX_Admisiones_FechaAdmisionUtc",
                schema: "admisiones",
                table: "Admisiones",
                column: "FechaAdmisionUtc",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "UX_Admisiones_HistoriaClinicaId",
                schema: "admisiones",
                table: "Admisiones",
                column: "HistoriaClinicaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Atenciones_Auditoria_Fecha",
                schema: "admisiones",
                table: "Atenciones",
                columns: new[] { "RequiereAuditoria", "FechaUtc" })
                .Annotation("SqlServer:Include", new[] { "PacienteId" });

            migrationBuilder.CreateIndex(
                name: "IX_Atenciones_PacienteId",
                schema: "admisiones",
                table: "Atenciones",
                column: "PacienteId");

            migrationBuilder.CreateIndex(
                name: "UX_Atenciones_AdmisionId",
                schema: "admisiones",
                table: "Atenciones",
                column: "AdmisionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_Pendientes",
                schema: "admisiones",
                table: "OutboxMessages",
                columns: new[] { "DisponibleDesdeUtc", "CreadoEnUtc" },
                filter: "[ProcesadoEnUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Pacientes_Documento",
                schema: "admisiones",
                table: "Pacientes",
                columns: new[] { "TipoDocumento", "NumeroDocumento" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Admisiones",
                schema: "admisiones");

            migrationBuilder.DropTable(
                name: "Atenciones",
                schema: "admisiones");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "admisiones");

            migrationBuilder.DropTable(
                name: "Pacientes",
                schema: "admisiones");
        }
    }
}
