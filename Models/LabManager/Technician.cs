using System.ComponentModel.DataAnnotations;

namespace Project.Models.LabManager
{
    public class Technician
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "First name is required.")]
        [StringLength(50)]
        public string Name { get; set; }

        [Required(ErrorMessage = "Surname is required.")]
        [StringLength(50)]
        public string Surname { get; set; }

        // ✔ Full SA ID validation - Luhn + citizenship digit + date check
        [Required(ErrorMessage = "SA ID number is required.")]
        [SouthAfricanId]
        public string IdNumber { get; set; }

        [Required(ErrorMessage = "Employee number is required.")]
        [StringLength(20)]
        public string EmployeeNumber { get; set; }

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

        public ICollection<TechnicianTestType> TechnicianTestTypes { get; set; }
            = new List<TechnicianTestType>();

    }//end of class Technician
}//end of namespace Project.Models.LabManager