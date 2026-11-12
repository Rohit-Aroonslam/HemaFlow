using System.ComponentModel.DataAnnotations;

namespace Project.Models.LabManager
{
    public class Order
    {
        public int Id { get; set; }

        [Required]
        public string OrderNumber { get; set; } // must be unique later

        [Required]
        public int SupplierId { get; set; }
        public Supplier Supplier { get; set; }

        public DateTime OrderDate { get; set; }

        [Required]
        public string Status { get; set; }
        // Ordered, Partial, Complete, Cancelled

        public DateTime? DateCompleted { get; set; }
        public DateTime? DateCancelled { get; set; }
        public string CancellationReason { get; set; }

        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }
}