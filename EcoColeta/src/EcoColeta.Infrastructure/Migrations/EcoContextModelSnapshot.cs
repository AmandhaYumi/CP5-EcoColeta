using System;
using EcoColeta.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace EcoColeta.Infrastructure.Migrations;

[DbContext(typeof(EcoContext))]
partial class EcoContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder.HasAnnotation("ProductVersion", "8.0.22");

        modelBuilder.Entity("EcoColeta.Domain.PontoColeta", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedOnAdd();
            b.Property<string>("City").IsRequired();
            b.Property<string>("Name").IsRequired().HasMaxLength(100);
            b.Property<string>("PostalCode").IsRequired();
            b.Property<string>("State").IsRequired();
            b.Property<string>("Street").IsRequired();
            b.HasKey("Id");
            b.HasIndex("PostalCode", "Name").IsUnique();
            b.ToTable("Points");
        });

        modelBuilder.Entity("EcoColeta.Domain.Usuario", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedOnAdd();
            b.Property<string>("Email").IsRequired().HasMaxLength(254);
            b.Property<string>("PasswordHash").IsRequired();
            b.Property<string>("Role").IsRequired();
            b.HasKey("Id");
            b.HasIndex("Email").IsUnique();
            b.ToTable("Users");
        });

        modelBuilder.Entity("EcoColeta.Domain.Coleta", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedOnAdd();
            b.Property<Guid>("CollectionPointId").IsRequired();
            b.Property<int>("Material").IsRequired();
            b.Property<DateTime>("ScheduledAt").IsRequired();
            b.Property<int>("Status").IsConcurrencyToken().IsRequired();
            b.Property<Guid>("UserId").IsRequired();
            b.Property<double>("WeightKg").IsRequired();
            b.HasKey("Id");
            b.HasIndex("CollectionPointId");
            b.HasIndex("ScheduledAt", "Id");
            b.HasIndex("UserId", "ScheduledAt", "Id");
            b.ToTable("Pickups", t => t.HasCheckConstraint("CK_Pickup_Weight", "\"WeightKg\" > 0 AND \"WeightKg\" <= 1000"));
        });

        modelBuilder.Entity("EcoColeta.Domain.Coleta", b =>
        {
            b.HasOne("EcoColeta.Domain.PontoColeta", null).WithMany().HasForeignKey("CollectionPointId").OnDelete(DeleteBehavior.Restrict).IsRequired();
            b.HasOne("EcoColeta.Domain.Usuario", null).WithMany().HasForeignKey("UserId").OnDelete(DeleteBehavior.Restrict).IsRequired();
        });
#pragma warning restore 612, 618
    }
}
