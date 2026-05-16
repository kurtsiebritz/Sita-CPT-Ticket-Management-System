using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SitaCptTicketApp.Migrations
{
    public partial class addingTeamMemberDropDown : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HowTos_SitaContracts_CompanyId",
                table: "HowTos");

            migrationBuilder.DropForeignKey(
                name: "FK_Notes_AspNetUsers_CreatedById",
                table: "Notes");

            migrationBuilder.AlterColumn<int>(
                name: "CreatedById",
                table: "Notes",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddForeignKey(
                name: "FK_HowTos_SitaContracts_CompanyId",
                table: "HowTos",
                column: "CompanyId",
                principalTable: "SitaContracts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Notes_SitaTeamMembers_CreatedById",
                table: "Notes",
                column: "CreatedById",
                principalTable: "SitaTeamMembers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HowTos_SitaContracts_CompanyId",
                table: "HowTos");

            migrationBuilder.DropForeignKey(
                name: "FK_Notes_SitaTeamMembers_CreatedById",
                table: "Notes");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedById",
                table: "Notes",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_HowTos_SitaContracts_CompanyId",
                table: "HowTos",
                column: "CompanyId",
                principalTable: "SitaContracts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Notes_AspNetUsers_CreatedById",
                table: "Notes",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
