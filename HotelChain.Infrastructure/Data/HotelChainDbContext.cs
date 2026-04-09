using HotelChain.Domain.Entities;
using HotelChain.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HotelChain.Infrastructure.Data;

public class HotelChainDbContext
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    // DbSETS
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<ReservationPayment> ReservationPayments => Set<ReservationPayment>();
    public DbSet<ReservationRoom> ReservationRooms => Set<ReservationRoom>();
    public DbSet<ReservationAudit> ReservationAudits => Set<ReservationAudit>();
    public DbSet<SearchAudit> SearchAudits => Set<SearchAudit>();
    public DbSet<HotelReview> HotelReviews => Set<HotelReview>();
    public DbSet<RoomType> RoomTypes => Set<RoomType>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<RoomInventory> RoomInventories => Set<RoomInventory>();

    public DbSet<City> Cities => Set<City>();
    public DbSet<Hotel> Hotels => Set<Hotel>();

    public HotelChainDbContext(DbContextOptions<HotelChainDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>(e =>
        {
            e.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            e.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Country).HasMaxLength(100).IsRequired();
            e.Property(x => x.PassportNumber).HasMaxLength(50).IsRequired();
        });

        modelBuilder.Entity<City>(e =>
        {
            e.ToTable("Cities");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.CountryCode).HasMaxLength(5).IsRequired();
        });

        modelBuilder.Entity<Hotel>(e =>
        {
            e.ToTable("Hotels");
            e.HasKey(x => x.Id);

            e.Property(x => x.Code).HasMaxLength(20).IsRequired();
            e.HasIndex(x => x.Code).IsUnique();

            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.Address).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2000);

            e.HasOne(x => x.City)
             .WithMany(x => x.Hotels)
             .HasForeignKey(x => x.CityId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RoomType>(e =>
        {
            e.ToTable("RoomTypes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<Room>(e =>
        {
            e.ToTable("Rooms");
            e.HasKey(x => x.Id);

            e.Property(x => x.NameOrNumber).HasMaxLength(100).IsRequired();
            e.Property(x => x.BasePricePerNight).HasColumnType("decimal(18,2)");

            e.HasOne(x => x.Hotel)
             .WithMany(h => h.Rooms)
             .HasForeignKey(x => x.HotelId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.RoomType)
             .WithMany(x => x.Rooms)
             .HasForeignKey(x => x.RoomTypeId);
        });

        modelBuilder.Entity<RoomInventory>(e =>
        {
            e.ToTable("RoomInventories");
            e.HasKey(x => x.Id);

            e.HasOne(x => x.Room)
             .WithMany()
             .HasForeignKey(x => x.RoomId);

            e.HasIndex(x => new { x.RoomId, x.Date }).IsUnique();
        });

        modelBuilder.Entity<Reservation>(e =>
        {
            e.ToTable("Reservations");
            e.HasKey(x => x.Id);

            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.HasIndex(x => x.Code).IsUnique();

            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.TotalAmount).HasColumnType("decimal(18,2)");

            e.HasOne(x => x.Hotel)
             .WithMany()
             .HasForeignKey(x => x.HotelId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne<ApplicationUser>()
             .WithMany()
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReservationRoom>(e =>
        {
            e.ToTable("ReservationRooms");
            e.HasKey(x => x.Id);

            e.Property(x => x.PricePerNight).HasColumnType("decimal(18,2)");
            e.Property(x => x.Subtotal).HasColumnType("decimal(18,2)");

            e.HasOne(x => x.Reservation)
             .WithMany(r => r.Rooms)
             .HasForeignKey(x => x.ReservationId);

            e.HasOne(x => x.Room)
             .WithMany()
             .HasForeignKey(x => x.RoomId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReservationAudit>(e =>
        {
            e.ToTable("ReservationAudits");
            e.HasKey(x => x.Id);

            e.Property(x => x.Action).HasMaxLength(20).IsRequired();
            e.Property(x => x.OldStatus).HasMaxLength(20);
            e.Property(x => x.NewStatus).HasMaxLength(20);
            e.Property(x => x.Reason).HasMaxLength(500);
            e.Property(x => x.Actor).HasMaxLength(100);
            e.Property(x => x.CreatedAt).IsRequired();

            e.HasOne(x => x.Reservation)
             .WithMany()
             .HasForeignKey(x => x.ReservationId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => x.ReservationId);
        });

        modelBuilder.Entity<SearchAudit>(e =>
        {
            e.ToTable("SearchAudits");
            e.HasKey(x => x.Id);

            e.Property(x => x.Guests).IsRequired();
            e.Property(x => x.CheckIn).IsRequired();
            e.Property(x => x.CheckOut).IsRequired();

            e.Property(x => x.MinPrice).HasColumnType("decimal(18,2)");
            e.Property(x => x.MaxPrice).HasColumnType("decimal(18,2)");

            e.Property(x => x.Source)
             .HasMaxLength(20)
             .IsRequired();

            e.Property(x => x.CreatedAt).IsRequired();

            e.HasOne(x => x.City)
             .WithMany()
             .HasForeignKey(x => x.CityId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => x.CreatedAt);
        });

        modelBuilder.Entity<HotelReview>(e =>
        {
            e.ToTable("HotelReviews");

            e.HasKey(x => x.Id);

            e.Property(x => x.Rating)
             .IsRequired();

            e.Property(x => x.Comment)
             .HasMaxLength(1000);

            e.Property(x => x.CreatedAt)
             .IsRequired();

            e.HasOne(x => x.Hotel)
             .WithMany(h => h.Reviews)
             .HasForeignKey(x => x.HotelId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne<ApplicationUser>()
             .WithMany()
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReservationPayment>()
            .HasOne(p => p.Reservation)
            .WithOne(r => r.Payment)
            .HasForeignKey<ReservationPayment>(p => p.ReservationId);
    }
}