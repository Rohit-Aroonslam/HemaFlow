using System.ComponentModel.DataAnnotations;

namespace Project.Models
{
    // Used by AdminController.CreateAdminUser - SuperAdmin-only screen for
    // creating a new Admin account. Put this in your Models folder alongside
    // the other ViewModels (EditUserViewModel, etc).
    public class CreateAdminUserViewModel
    {
        [Required(ErrorMessage = "First name is required.")]
        public string FirstName { get; set; } = "";

        [Required(ErrorMessage = "Surname is required.")]
        public string Surname { get; set; } = "";

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Password is required.")]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
        public string Password { get; set; } = "";

        [Required(ErrorMessage = "Please confirm the password.")]
        public string ConfirmPassword { get; set; } = "";
    }
}
