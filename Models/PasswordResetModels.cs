using System.ComponentModel.DataAnnotations;

namespace Project.Models
{
    // Backs the real ResetPassword GET/POST (AccountController). Brand new
    // feature - this is a fresh file, not shared with anything you already have.
    public class ResetPasswordViewModel
    {
        [Required]
        public string Token { get; set; } = "";

        [Required]
        [MinLength(8)]
        public string NewPassword { get; set; } = "";

        [Required]
        [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = "";
    }
}
