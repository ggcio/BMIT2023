using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Demo.Models;

#nullable disable warnings

public class DB(DbContextOptions options) : DbContext(options)
{
    // DB Sets
    public DbSet<User> Users { get; set; }
    public DbSet<Admin> Admins { get; set; }
    public DbSet<Member> Members { get; set; }

    public DbSet<Category> Categories { get; set; }
    public DbSet<Facility> Facilities { get; set; }
    public DbSet<FacilityPhoto> FacilityPhotos { get; set; }
    public DbSet<TimeSlot> TimeSlots { get; set; }
    public DbSet<Booking> Bookings { get; set; }
    public DbSet<Payment> Payments { get; set; }
}

// Entity Classes -------------------------------------------------------------

public class User
{
    [Key, MaxLength(100)]
    public string Email { get; set; }
    [MaxLength(100)]
    public string Hash { get; set; }
    [MaxLength(100)]
    public string Name { get; set; }

    // Login blocking after 3 failed attempts
    public int FailedAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }

    // Blocked by an admin (or by the SuperAdmin for admin accounts)
    public bool IsBlocked { get; set; }

    // Email verification and password reset token links
    public bool EmailVerified { get; set; }
    [MaxLength(100)]
    public string? VerifyToken { get; set; }
    [MaxLength(100)]
    public string? ResetToken { get; set; }
    public DateTime? ResetExpiresAt { get; set; }

    public string Role => GetType().Name;
}

public class Admin : User
{
    public bool IsSuperAdmin { get; set; }
}

public class Member : User
{
    [MaxLength(100)]
    public string PhotoURL { get; set; }

    // Student status: None, Pending, Approved, Rejected
    [MaxLength(20)]
    public string? StudentId { get; set; }
    [MaxLength(10)]
    public string StudentStatus { get; set; } = "None";
    [MaxLength(200)]
    public string? ReviewNote { get; set; }

    // Navigation Properties
    public List<Booking> Bookings { get; set; } = [];
}

// Category, Facility, FacilityPhoto, TimeSlot

public class Category
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [MaxLength(100)]
    public string Name { get; set; }

    // Navigation Properties
    public List<Facility> Facilities { get; set; } = [];
}

public class Facility
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [MaxLength(100)]
    public string Name { get; set; }

    [MaxLength(100)]
    public string Location { get; set; }

    public int Capacity { get; set; }

    [Precision(6, 2)]
    public decimal HourlyRate { get; set; }

    [Precision(6, 2)]
    public decimal PeakSurcharge { get; set; }

    // Foreign Keys
    public int CategoryId { get; set; }

    // Navigation Properties
    public Category Category { get; set; }
    public List<FacilityPhoto> Photos { get; set; } = [];
    public List<TimeSlot> TimeSlots { get; set; } = [];
}

public class FacilityPhoto
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [MaxLength(100)]
    public string FileName { get; set; }

    // Foreign Keys
    public int FacilityId { get; set; }

    // Navigation Properties
    public Facility Facility { get; set; }
}

public class TimeSlot
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public DateOnly Date { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public bool IsPeak { get; set; }

    // Foreign Keys
    public int FacilityId { get; set; }

    // Navigation Properties
    public Facility Facility { get; set; }
    public List<Booking> Bookings { get; set; } = [];
}

// Booking, Payment

public class Booking
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    // Price that applied when the booking was made (0 for students)
    [Precision(6, 2)]
    public decimal Price { get; set; }

    // Pending, Confirmed, Cancelled
    [MaxLength(10)]
    public string Status { get; set; }

    // A pending booking holds its capacity until this time (15 minutes)
    public DateTime? HoldExpiresAt { get; set; }

    // None, RefundDue, Refunded
    [MaxLength(10)]
    public string RefundStatus { get; set; } = "None";

    public DateTime? CheckedInAt { get; set; }

    // Foreign Keys (MemberEmail, TimeSlotId)
    public string MemberEmail { get; set; }
    public int TimeSlotId { get; set; }

    // Navigation Properties
    public Member Member { get; set; }
    public TimeSlot TimeSlot { get; set; }
    public Payment? Payment { get; set; }
}

public class Payment
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Precision(6, 2)]
    public decimal Amount { get; set; }

    // Stripe Checkout session id
    [MaxLength(200)]
    public string StripeReference { get; set; }

    // Pending, Paid, Failed
    [MaxLength(10)]
    public string Status { get; set; }

    public DateTime? PaidAt { get; set; }

    // Foreign Keys
    public int BookingId { get; set; }

    // Navigation Properties
    public Booking Booking { get; set; }
}
