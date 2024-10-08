﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using WeeklyIL.Database;

#nullable disable

namespace WeeklyIL.Migrations
{
    [DbContext(typeof(WilDbContext))]
    [Migration("20241002093350_WeekEnd")]
    partial class WeekEnd
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "8.0.8");

            modelBuilder.Entity("WeeklyIL.Database.AchievementRole", b =>
                {
                    b.Property<ulong>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<ulong?>("GuildEntityId")
                        .HasColumnType("INTEGER");

                    b.Property<uint>("Requirement")
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("RoleId")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("GuildEntityId");

                    b.ToTable("AchievementRole");
                });

            modelBuilder.Entity("WeeklyIL.Database.GuildEntity", b =>
                {
                    b.Property<ulong>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("AnnouncementsChannel")
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("ModeratorRole")
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("OrganizerRole")
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("SubmissionsChannel")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.ToTable("Guilds");
                });

            modelBuilder.Entity("WeeklyIL.Database.ScoreEntity", b =>
                {
                    b.Property<ulong>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<uint?>("TimeMs")
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("UserId")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("Verified")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Video")
                        .HasColumnType("TEXT");

                    b.Property<ulong>("WeekId")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.ToTable("Scores");
                });

            modelBuilder.Entity("WeeklyIL.Database.UserEntity", b =>
                {
                    b.Property<ulong>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<uint>("WeeklyWins")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.ToTable("Users");
                });

            modelBuilder.Entity("WeeklyIL.Database.WeekEntity", b =>
                {
                    b.Property<ulong>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<bool>("Ended")
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("GuildId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Level")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<uint>("StartTimestamp")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.ToTable("Weeks");
                });

            modelBuilder.Entity("WeeklyIL.Database.AchievementRole", b =>
                {
                    b.HasOne("WeeklyIL.Database.GuildEntity", null)
                        .WithMany("WeeklyRoles")
                        .HasForeignKey("GuildEntityId");
                });

            modelBuilder.Entity("WeeklyIL.Database.GuildEntity", b =>
                {
                    b.Navigation("WeeklyRoles");
                });
#pragma warning restore 612, 618
        }
    }
}
