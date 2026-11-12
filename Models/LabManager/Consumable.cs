using System.ComponentModel.DataAnnotations;

namespace Project.Models.LabManager
{
    public class Consumable
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        public int ReorderLevel { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        public int QuantityOnHand { get; set; }

        // ✔ Supplier relationship
        [Required]
        public int SupplierId { get; set; }
        public Supplier Supplier { get; set; }

        // ✔ Archive flag (soft delete)
        public bool IsArchived { get; set; } = false;

        public ICollection<TestTypeConsumable> TestTypeConsumables { get; set; } = new List<TestTypeConsumable>();
        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    }//end of class Consumable
}//end of namespace