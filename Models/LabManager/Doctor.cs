using System.ComponentModel.DataAnnotations;

namespace Project.Models.LabManager
{
    public class Doctor
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "First name is required.")]
        [StringLength(50)]
        public string Name { get; set; }

        [Required(ErrorMessage = "Surname is required.")]
        [StringLength(50)]
        public string Surname { get; set; }

        // ✔ HPCSA format: MP followed by 7 digits (e.g. MP0234871)
        [Required(ErrorMessage = "HPCSA number is required.")]
        [RegularExpression(@"^MP\d{7}$", ErrorMessage = "HPCSA number must be in the format MP followed by 7 digits (e.g. MP0234871).")]
        public string HPCSANumber { get; set; }

        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Contact number is required.")]
        [RegularExpression(@"^0\d{9}$", ErrorMessage = "Contact number must be 10 digits starting with 0.")]
        public string ContactNumber { get; set; }

        public string Password { get; set; }

        public bool MustChangePassword { get; set; } = true;

        // ✔ Archive flag
        public bool IsArchived { get; set; } = false;

    }//end of class Doctor
}//end of namespace Project.Models.LabManager