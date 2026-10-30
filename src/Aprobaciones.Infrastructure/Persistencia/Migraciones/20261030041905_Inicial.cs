using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Aprobaciones.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "DocumentoNumeros");

            migrationBuilder.CreateTable(
                name: "ReglasAprobacion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MontoDesde = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MontoHasta = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Activa = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReglasAprobacion", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Cuenta = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    FechaAlta = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UltimoAcceso = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReglaPasos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReglaAprobacionId = table.Column<int>(type: "int", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    GrupoAd = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReglaPasos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReglaPasos_ReglasAprobacion_ReglaAprobacionId",
                        column: x => x.ReglaAprobacionId,
                        principalTable: "ReglasAprobacion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Documentos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Numero = table.Column<int>(type: "int", nullable: false, defaultValueSql: "NEXT VALUE FOR dbo.DocumentoNumeros"),
                    Folio = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, computedColumnSql: "'DOC-' + IIF([Numero] > 999999, CAST([Numero] AS varchar(10)), RIGHT('00000' + CAST([Numero] AS varchar(10)), 6))", stored: true),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Estado = table.Column<byte>(type: "tinyint", nullable: false),
                    CreadoPorId = table.Column<int>(type: "int", nullable: false),
                    CfdiUuid = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CfdiRfcEmisor = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: true),
                    CfdiNombreEmisor = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CfdiRfcReceptor = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: true),
                    CfdiSerie = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: true),
                    CfdiFolio = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    CfdiFecha = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CfdiSubTotal = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    CfdiTotal = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    CfdiMoneda = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    CfdiTipoCambio = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    CfdiTipoComprobante = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    MontoMxn = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Ronda = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaEnvio = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaAutorizacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaRechazo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaProcesado = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReferenciaTesoreria = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documentos_Usuarios_CreadoPorId",
                        column: x => x.CreadoPorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentoAprobaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentoId = table.Column<int>(type: "int", nullable: false),
                    Ronda = table.Column<int>(type: "int", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    GrupoAd = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Estado = table.Column<byte>(type: "tinyint", nullable: false),
                    ResueltoPorId = table.Column<int>(type: "int", nullable: true),
                    FechaResolucion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Comentario = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentoAprobaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentoAprobaciones_Documentos_DocumentoId",
                        column: x => x.DocumentoId,
                        principalTable: "Documentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentoAprobaciones_Usuarios_ResueltoPorId",
                        column: x => x.ResueltoPorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentoArchivos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentoId = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<byte>(type: "tinyint", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Vigente = table.Column<bool>(type: "bit", nullable: false),
                    NombreOriginal = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    RutaRelativa = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TamanoBytes = table.Column<long>(type: "bigint", nullable: false),
                    HashSha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    SubidoPorId = table.Column<int>(type: "int", nullable: false),
                    FechaSubida = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentoArchivos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentoArchivos_Documentos_DocumentoId",
                        column: x => x.DocumentoId,
                        principalTable: "Documentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentoArchivos_Usuarios_SubidoPorId",
                        column: x => x.SubidoPorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentoHistorial",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentoId = table.Column<int>(type: "int", nullable: false),
                    Accion = table.Column<byte>(type: "tinyint", nullable: false),
                    EstadoAnterior = table.Column<byte>(type: "tinyint", nullable: true),
                    EstadoNuevo = table.Column<byte>(type: "tinyint", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    Comentario = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentoHistorial", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentoHistorial_Documentos_DocumentoId",
                        column: x => x.DocumentoId,
                        principalTable: "Documentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentoHistorial_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "ReglasAprobacion",
                columns: new[] { "Id", "Activa", "MontoDesde", "MontoHasta", "Nombre" },
                values: new object[,]
                {
                    { 1, true, 0m, 50000m, "Hasta $50,000" },
                    { 2, true, 50000.01m, 250000m, "$50,000.01 a $250,000" },
                    { 3, true, 250000.01m, null, "Más de $250,000" }
                });

            migrationBuilder.InsertData(
                table: "ReglaPasos",
                columns: new[] { "Id", "GrupoAd", "Nombre", "Orden", "ReglaAprobacionId" },
                values: new object[,]
                {
                    { 1, "APR_GerenteArea", "Gerente de Área", 1, 1 },
                    { 2, "APR_GerenteArea", "Gerente de Área", 1, 2 },
                    { 3, "APR_GerenteFinanciero", "Gerente Financiero", 2, 2 },
                    { 4, "APR_GerenteArea", "Gerente de Área", 1, 3 },
                    { 5, "APR_GerenteFinanciero", "Gerente Financiero", 2, 3 },
                    { 6, "APR_DirectorGeneral", "Director General", 3, 3 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentoAprobaciones_DocumentoId_Ronda_Orden",
                table: "DocumentoAprobaciones",
                columns: new[] { "DocumentoId", "Ronda", "Orden" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentoAprobaciones_Estado_GrupoAd",
                table: "DocumentoAprobaciones",
                columns: new[] { "Estado", "GrupoAd" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentoAprobaciones_ResueltoPorId",
                table: "DocumentoAprobaciones",
                column: "ResueltoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentoArchivos_DocumentoId_Tipo",
                table: "DocumentoArchivos",
                columns: new[] { "DocumentoId", "Tipo" },
                unique: true,
                filter: "[Vigente] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentoArchivos_SubidoPorId",
                table: "DocumentoArchivos",
                column: "SubidoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentoHistorial_DocumentoId_Fecha",
                table: "DocumentoHistorial",
                columns: new[] { "DocumentoId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentoHistorial_UsuarioId",
                table: "DocumentoHistorial",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Documentos_CfdiUuid",
                table: "Documentos",
                column: "CfdiUuid",
                unique: true,
                filter: "[CfdiUuid] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Documentos_CreadoPorId_Estado",
                table: "Documentos",
                columns: new[] { "CreadoPorId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_Documentos_Estado",
                table: "Documentos",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_Documentos_Folio",
                table: "Documentos",
                column: "Folio",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReglaPasos_ReglaAprobacionId_Orden",
                table: "ReglaPasos",
                columns: new[] { "ReglaAprobacionId", "Orden" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Cuenta",
                table: "Usuarios",
                column: "Cuenta",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentoAprobaciones");

            migrationBuilder.DropTable(
                name: "DocumentoArchivos");

            migrationBuilder.DropTable(
                name: "DocumentoHistorial");

            migrationBuilder.DropTable(
                name: "ReglaPasos");

            migrationBuilder.DropTable(
                name: "Documentos");

            migrationBuilder.DropTable(
                name: "ReglasAprobacion");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropSequence(
                name: "DocumentoNumeros");
        }
    }
}
