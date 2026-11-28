using Microsoft.AspNetCore.Mvc;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Demo.Models;

#nullable disable warnings

// View Models ----------------------------------------------------------------

public class LoginVM
{
    [StringLength(100)]
    [EmailAddress]
    public string Email { get; set; }

    [StringLength(100, MinimumLength = 5)]
    [DataType(DataType.Password)]
    public string Password { get; set; }

    public bool RememberMe { get; set; }
}

public class RegisterVM
{
    [StringLength(100)]
    [EmailAddress]
    [Remote("CheckEmail", "Account", ErrorMessage = "Duplicated {0}.")]
    public string Email { get; set; }

    [StringLength(100, MinimumLength = 5)]
    [DataType(DataType.Password)]
    public string Password { get; set; }

    [StringLength(100, MinimumLength = 5)]
    [Compare("Password")]
    [DataType(DataType.Password)]
    [DisplayName("Confirm Password")]
    public string Confirm { get; set; }

    [StringLength(100)]
    public string Name { get; set; }

    public IFormFile? Photo { get; set; }
}

public class UpdatePasswordVM
{
    [StringLength(100, MinimumLength = 5)]
    [DataType(DataType.Password)]
    [DisplayName("Current Password")]
    public string Current { get; set; }

    [StringLength(100, MinimumLength = 5)]
    [DataType(DataType.Password)]
    [DisplayName("New Password")]
    public string New { get; set; }

    [StringLength(100, MinimumLength = 5)]
    [Compare("New")]
    [DataType(DataType.Password)]
    [DisplayName("Confirm Password")]
    public string Confirm { get; set; }
}

public class UpdateProfileVM
{
    public string? Email { get; set; }

    [StringLength(100)]
    public string Name { get; set; }

    public string? PhotoURL { get; set; }

    public IFormFile? Photo { get; set; }
}

public class ResetPasswordVM
{
    [StringLength(100)]
    [EmailAddress]
    public string Email { get; set; }
}

public class ResetPasswordTokenVM
{
    public string Token { get; set; }

    [StringLength(100, MinimumLength = 5)]
    [DataType(DataType.Password)]
    [DisplayName("New Password")]
    public string New { get; set; }

    [StringLength(100, MinimumLength = 5)]
    [Compare("New")]
    [DataType(DataType.Password)]
    [DisplayName("Confirm Password")]
    public string Confirm { get; set; }
}

public class ResendVerificationVM
{
    [StringLength(100)]
    [EmailAddress]
    public string Email { get; set; }
}

public class CategoryVM
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "{0} is required.")]
    [StringLength(100, ErrorMessage = "{0} cannot exceed {1} characters.")]
    [Remote("CheckName", "Category", AdditionalFields = "Id", ErrorMessage = "Duplicated {0}.")]
    public string Name { get; set; }
}

public class FacilityVM
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "{0} is required.")]
    [StringLength(100, ErrorMessage = "{0} cannot exceed {1} characters.")]
    [Remote("CheckName", "Facility", AdditionalFields = "Id", ErrorMessage = "Duplicated {0}.")]
    public string Name { get; set; }

    [Required(ErrorMessage = "{0} is required.")]
    [StringLength(100, ErrorMessage = "{0} cannot exceed {1} characters.")]
    public string Location { get; set; }

    [Required(ErrorMessage = "{0} is required.")]
    [Range(1, 200, ErrorMessage = "{0} must be between {1} and {2}.")]
    public int Capacity { get; set; }

    [Required(ErrorMessage = "{0} is required.")]
    [Range(0.01, 9999.99, ErrorMessage = "{0} must be between {1} and {2}.")]
    [RegularExpression(@"\d+(\.\d{1,2})?", ErrorMessage = "{0} can have at most 2 decimal places.")]
    [DisplayName("Hourly Rate (RM)")]
    public decimal HourlyRate { get; set; }

    [Required(ErrorMessage = "{0} is required.")]
    [Range(0, 9999.99, ErrorMessage = "{0} must be between {1} and {2}.")]
    [RegularExpression(@"\d+(\.\d{1,2})?", ErrorMessage = "{0} can have at most 2 decimal places.")]
    [DisplayName("Peak Surcharge (RM)")]
    public decimal PeakSurcharge { get; set; }

    [Required(ErrorMessage = "Please select a category.")]
    [DisplayName("Category")]
    public int CategoryId { get; set; }
}

public class TimeSlotVM
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Please select a facility.")]
    [DisplayName("Facility")]
    public int FacilityId { get; set; }

    [Required(ErrorMessage = "{0} is required.")]
    [DataType(DataType.Date)]
    public DateOnly Date { get; set; }

    [Required(ErrorMessage = "{0} is required.")]
    [DataType(DataType.Time)]
    [DisplayName("Start Time")]
    public TimeOnly StartTime { get; set; }

    [DisplayName("Peak")]
    public bool IsPeak { get; set; }
}

public class BatchSlotVM
{
    [Required(ErrorMessage = "{0} is required.")]
    [DataType(DataType.Date)]
    [DisplayName("Start Date")]
    public DateOnly StartDate { get; set; }

    [Required(ErrorMessage = "{0} is required.")]
    [DataType(DataType.Date)]
    [DisplayName("End Date")]
    public DateOnly EndDate { get; set; }

    [DataType(DataType.Time)]
    [DisplayName("Peak From")]
    public TimeOnly? PeakFrom { get; set; }

    [DataType(DataType.Time)]
    [DisplayName("Peak To")]
    public TimeOnly? PeakTo { get; set; }
}

// One row of the time slot table on the Catalog Detail page
public class SlotRowVM
{
    public int Id { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool IsPeak { get; set; }
    public decimal Price { get; set; }
    public int SpotsLeft { get; set; }
    public bool IsClosed { get; set; }
}

// Booking

public class BookingVM
{
    [Range(1, int.MaxValue)]
    [DisplayName("Time Slot")]
    public int SlotId { get; set; }
}

public class CheckInVM
{
    [Required(ErrorMessage = "Please enter a booking ID.")]
    [Range(1, int.MaxValue, ErrorMessage = "Invalid booking ID.")]
    [DisplayName("Booking ID")]
    public int? BookingId { get; set; }
}

public class AdminUserInsertVM
{
    [StringLength(100)]
    [EmailAddress]
    [Remote("CheckEmail", "Account", ErrorMessage = "Duplicated {0}.")]
    public string Email { get; set; }

    [StringLength(100)]
    public string Name { get; set; }

    [StringLength(100, MinimumLength = 5)]
    [DataType(DataType.Password)]
    public string Password { get; set; }

    [StringLength(100, MinimumLength = 5)]
    [Compare("Password")]
    [DataType(DataType.Password)]
    [DisplayName("Confirm Password")]
    public string Confirm { get; set; }
}

public class AdminUserUpdateVM
{
    public string Email { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; }
}

public class ReportFilterVM
{
    // "Date" uses From/To, "Month" uses the month and year fields
    [RegularExpression("^(Date|Month)$", ErrorMessage = "Invalid {0}.")]
    public string Mode { get; set; } = "Date";

    [DataType(DataType.Date)]
    public DateOnly? From { get; set; }

    [DataType(DataType.Date)]
    public DateOnly? To { get; set; }

    [Range(1, 12, ErrorMessage = "Invalid {0}.")]
    [DisplayName("From Month")]
    public int? FromMonth { get; set; }

    [Range(2000, 2100, ErrorMessage = "Invalid {0}.")]
    [DisplayName("From Year")]
    public int? FromYear { get; set; }

    [Range(1, 12, ErrorMessage = "Invalid {0}.")]
    [DisplayName("To Month")]
    public int? ToMonth { get; set; }

    [Range(2000, 2100, ErrorMessage = "Invalid {0}.")]
    [DisplayName("To Year")]
    public int? ToYear { get; set; }

    [DisplayName("Facility")]
    public int? FacilityId { get; set; }

    [DisplayName("Category")]
    public int? CategoryId { get; set; }

    // Revenue chart periods
    [RegularExpression("^(Day|Week|Month)$", ErrorMessage = "Invalid {0}.")]
    [DisplayName("Group by")]
    public string GroupBy { get; set; } = "Day";

    // Which report tab is shown
    [RegularExpression("^(Overview|Bookings|Revenue|Attendance|Refunds)$", ErrorMessage = "Invalid {0}.")]
    public string Report { get; set; } = "Overview";
}
