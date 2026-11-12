namespace Project.Models
{
    // One row of the "what is in my results" summary on the Reports page.
    public class ReportCategoryInfo
    {
        public string Category { get; set; } = "";
        public int Count { get; set; }
        public List<string> Tests { get; set; } = new();
        public string First { get; set; } = "";
        public string Last { get; set; } = "";
    }
}
