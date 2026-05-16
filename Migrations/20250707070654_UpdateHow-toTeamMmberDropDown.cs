using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SitaCptTicketApp.Migrations
{
    public partial class UpdateHowtoTeamMmberDropDown : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HowTos_AspNetUsers_CreatedById",
                table: "HowTos");

            migrationBuilder.DropForeignKey(
                name: "FK_HowTos_SitaContracts_CompanyId",
                table: "HowTos");

            migrationBuilder.AlterColumn<string>(
                name: "ImagePath",
                table: "HowToSteps",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<int>(
                name: "CreatedById",
                table: "HowTos",
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
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_HowTos_SitaTeamMembers_CreatedById",
                table: "HowTos",
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
                name: "FK_HowTos_SitaTeamMembers_CreatedById",
                table: "HowTos");

            migrationBuilder.AlterColumn<string>(
                name: "ImagePath",
                table: "HowToSteps",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedById",
                table: "HowTos",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_HowTos_AspNetUsers_CreatedById",
                table: "HowTos",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_HowTos_SitaContracts_CompanyId",
                table: "HowTos",
                column: "CompanyId",
                principalTable: "SitaContracts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
