namespace Project.Models.LabManager
{
    public class ActivityLog
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string Action { get; set; }  // Created, Updated, Archived, Restored, StockAdjusted, OrderPlaced, OrderCancelled, ItemReceived, ItemCancelled
        public string EntityType { get; set; }  // TestCategory, TestType, Consumable, Supplier, Order, Doctor, Technician
        public string EntityName { get; set; }  // The name/number of the affected record
        public string PerformedBy { get; set; }  // 🟢 TEMP: "Lab Manager" | 🔴 DB: pull from session/identity
        public string Notes { get; set; }  // Optional extra detail

    }//end of class ActivityLog
}//end of namespace