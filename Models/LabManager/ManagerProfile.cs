using System.ComponentModel.DataAnnotations;

namespace Project.Models.LabManager
{
    public class ManagerProfile
    {
        [Required]
        public string Name { get; set; }

        [Required]
        public string Surname { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [RegularExpression(@"^0\d{9}$", ErrorMessage = "Contact number must be 10 digits starting with 0.")]
        public string ContactNumber { get; set; }

        public string Password { get; set; }

    }//end of class ManagerProfile
}//end of namespace