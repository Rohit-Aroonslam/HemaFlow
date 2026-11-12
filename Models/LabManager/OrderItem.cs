using System.ComponentModel.DataAnnotations;

namespace Project.Models.LabManager
{
    public class OrderItem
    {
        public int Id { get; set; }

        public int OrderId { get; set; }
        public Order Order { get; set; }

        public int ConsumableId { get; set; }
        public Consumable Consumable { get; set; }

        [Required]
        public int QuantityOrdered { get; set; }

        [Required]
        public string Status { get; set; }
        // Ordered, Received, Cancelled

        public DateTime? DateReceived { get; set; }
        public DateTime? DateCancelled { get; set; }
        public string CancellationReason { get; set; }

    }//end of class OrderItem
}//end of namespace Project.Models.LabManager