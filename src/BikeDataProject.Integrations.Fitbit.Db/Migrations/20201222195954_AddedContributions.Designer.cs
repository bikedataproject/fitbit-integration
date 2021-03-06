﻿// <auto-generated />
using System;
using BikeDataProject.Integrations.Fitbit.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace BikeDataProject.Integrations.Fitbit.Db.Migrations
{
    [DbContext(typeof(FitbitDbContext))]
    [Migration("20201222195954_AddedContributions")]
    partial class AddedContributions
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .UseIdentityByDefaultColumns()
                .HasAnnotation("Relational:MaxIdentifierLength", 63)
                .HasAnnotation("ProductVersion", "5.0.1");

            modelBuilder.Entity("BikeDataProject.Integrations.Fitbit.Db.Contribution", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .UseIdentityByDefaultColumn();

                    b.Property<int>("BikeDataProjectId")
                        .HasColumnType("integer");

                    b.Property<int>("FitBitLogId")
                        .HasColumnType("integer");

                    b.Property<int>("UserId")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("Contributions");
                });

            modelBuilder.Entity("BikeDataProject.Integrations.Fitbit.Db.User", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .UseIdentityByDefaultColumn();

                    b.Property<bool>("AllSynced")
                        .HasColumnType("boolean");

                    b.Property<int?>("BikeDataProjectId")
                        .HasColumnType("integer");

                    b.Property<int>("ExpiresIn")
                        .HasColumnType("integer");

                    b.Property<DateTime?>("LatestSyncedStamp")
                        .HasColumnType("timestamp without time zone");

                    b.Property<string>("RefreshToken")
                        .HasColumnType("text");

                    b.Property<string>("Scope")
                        .HasColumnType("text");

                    b.Property<string>("Token")
                        .HasColumnType("text");

                    b.Property<DateTime>("TokenCreated")
                        .HasColumnType("timestamp without time zone");

                    b.Property<string>("TokenType")
                        .HasColumnType("text");

                    b.Property<string>("UserId")
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.ToTable("Users");
                });

            modelBuilder.Entity("BikeDataProject.Integrations.Fitbit.Db.Contribution", b =>
                {
                    b.HasOne("BikeDataProject.Integrations.Fitbit.Db.User", "User")
                        .WithMany("Contributions")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("BikeDataProject.Integrations.Fitbit.Db.User", b =>
                {
                    b.Navigation("Contributions");
                });
#pragma warning restore 612, 618
        }
    }
}
