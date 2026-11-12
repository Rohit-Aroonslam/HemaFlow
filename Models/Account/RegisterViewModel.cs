using System.ComponentModel.DataAnnotations;

namespace Project.Models
{
    // Backs the real patient self-registration POST (AccountController.Register).
    // Client-side JS in Register.cshtml already does the heavy validation
    // (SA ID format, password rules, matching confirm password) - these
    // attributes are a server-side safety net, not the primary UX.
    public class RegisterViewModel
    {
        [Required]
        public string FirstName { get; set; } = "";

        [Required]
        public string Surname { get; set; } = "";

        [Required]
        [StringLength(13, MinimumLength = 13, ErrorMessage = "SA ID number must be exactly 13 digits.")]
        [Project.Models.LabManager.SouthAfricanId(ErrorMessage = "Enter a valid South African ID number (13 digits, real birth date, valid check digit).")]
        public string IdNumber { get; set; } = "";

        [Required]
        [DataType(DataType.Date)]
        public DateTime DateOfBirth { get; set; }

        [Required]
        public string Phone { get; set; } = "";

        [Required]
        [EmailAddress]
        [RegularExpression(@"^[A-Za-z0-9._%+\-]+@[A-Za-z0-9\-]+(\.[A-Za-z0-9\-]+)*\.[A-Za-z]{2,}$", ErrorMessage = "Enter a valid email address, for example name@example.com.")]
        public string Email { get; set; } = "";

        [Required]
        public string AddressLine1 { get; set; } = "";

        [StringLength(100)]
        public string? ComplexName { get; set; }

        // Chosen from the Province -> Town -> Suburb dropdowns (null if the location tables are empty).
        public int? SuburbId { get; set; }
        public int? ProvinceId { get; set; }

        [Required]
        public string Suburb { get; set; } = "";

        [Required]
        public string City { get; set; } = "";

        [Required]
        public string PostalCode { get; set; } = "";

        [Required]
        [MinLength(8)]
        public string Password { get; set; } = "";

        [Required]
        [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = "";
    }
}
