using System.ComponentModel.DataAnnotations;

namespace Project.Models.LabManager
{
    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "Current password is required.")]
        public string CurrentPassword { get; set; }

        [Required(ErrorMessage = "New password is required.")]
        [RegularExpression(
            @"^(?=.*[A-Z])(?=.*\d)(?=.*[@#$!%*?]).{8,}$",
            ErrorMessage = "Password must be at least 8 characters and include one uppercase letter, one number, and one special character (@#$!%*?).")]
        public string NewPassword { get; set; }

        [Required(ErrorMessage = "Please confirm your new password.")]
        [Compare("NewPassword", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; }

    }//end of class ChangePasswordViewModel
}//end of namespace