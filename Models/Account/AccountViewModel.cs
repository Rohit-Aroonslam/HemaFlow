namespace Project.Models
{
    public class AccountChangePasswordViewModel
    {
        public string CurrentPassword { get; set; } = "";
        public string NewPassword { get; set; } = "";
        public string ConfirmPassword { get; set; } = "";
    }

    public class ForgotPasswordViewModel
    {
        public string Email { get; set; } = "";
    }
}
