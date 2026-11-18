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

    public IFormFile Photo { get; set; }
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
