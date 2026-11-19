using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aprobaciones.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class Fase8ReglasYCorreos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FechaModificacion",
                table: "ReglasAprobacion",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ModificadoPorId",
                table: "ReglasAprobacion",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Notificaciones",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Evento = table.Column<byte>(type: "tinyint", nullable: false),
                    DocumentoId = table.Column<int>(type: "int", nullable: true),
                    UsuarioId = table.Column<int>(type: "int", nullable: true),
                    GrupoAd = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ExcluirCuentas = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Asunto = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CuerpoHtml = table.Column<string>(type: "nvarchar(max)", maxLength: 200, nullable: false),
                    CuerpoTexto = table.Column<string>(type: "nvarchar(max)", maxLength: 200, nullable: false),
                    Estado = table.Column<byte>(type: "tinyint", nullable: false),
                    Intentos = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProximoIntento = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BloqueadaHasta = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaEnvio = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Destinatarios = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    UltimoError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notificaciones", x => x.Id);
                    table.CheckConstraint("CK_Notificaciones_Destino", "([UsuarioId] IS NOT NULL AND [GrupoAd] IS NULL) OR ([UsuarioId] IS NULL AND [GrupoAd] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_Notificaciones_Documentos_DocumentoId",
                        column: x => x.DocumentoId,
                        principalTable: "Documentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Notificaciones_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "ReglasAprobacion",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FechaModificacion", "ModificadoPorId" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ReglasAprobacion",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "FechaModificacion", "ModificadoPorId" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ReglasAprobacion",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "FechaModificacion", "ModificadoPorId" },
                values: new object[] { null, null });

            migrationBuilder.CreateIndex(
                name: "IX_ReglasAprobacion_ModificadoPorId",
                table: "ReglasAprobacion",
                column: "ModificadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_Notificaciones_DocumentoId",
                table: "Notificaciones",
                column: "DocumentoId");

            migrationBuilder.CreateIndex(
                name: "IX_Notificaciones_Estado_ProximoIntento",
                table: "Notificaciones",
                columns: new[] { "Estado", "ProximoIntento" });

            migrationBuilder.CreateIndex(
                name: "IX_Notificaciones_UsuarioId",
                table: "Notificaciones",
                column: "UsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_ReglasAprobacion_Usuarios_ModificadoPorId",
                table: "ReglasAprobacion",
                column: "ModificadoPorId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReglasAprobacion_Usuarios_ModificadoPorId",
                table: "ReglasAprobacion");

            migrationBuilder.DropTable(
                name: "Notificaciones");

            migrationBuilder.DropIndex(
                name: "IX_ReglasAprobacion_ModificadoPorId",
                table: "ReglasAprobacion");

            migrationBuilder.DropColumn(
                name: "FechaModificacion",
                table: "ReglasAprobacion");

            migrationBuilder.DropColumn(
                name: "ModificadoPorId",
                table: "ReglasAprobacion");
        }
    }
}
