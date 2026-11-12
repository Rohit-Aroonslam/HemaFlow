using System.ComponentModel.DataAnnotations;

namespace Project.Models.LabManager
{
    public class TestCategory
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        public string Description { get; set; }

        // ✔ Archive flag (soft delete)
        public bool IsArchived { get; set; } = false;

        public ICollection<TestType> TestTypes { get; set; } = new List<TestType>();

    }//end of class TestCategory
}//end of namespace