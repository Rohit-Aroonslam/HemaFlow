using System.ComponentModel.DataAnnotations;

namespace Project.Models.LabManager
{
    public class Supplier
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        [StringLength(100)]
        public string ContactPerson { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        // ✔ Archive flag (soft delete)
        public bool IsArchived { get; set; } = false;

        public ICollection<Consumable> Consumables { get; set; } = new List<Consumable>();

    }//end of class Supplier
}//end of namespace