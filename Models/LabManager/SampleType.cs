using System.ComponentModel.DataAnnotations;
namespace Project.Models.LabManager
{
    public class SampleType
    {
        public int Id { get; set; }
        [Required]
        [StringLength(50)]
        public string Name { get; set; } // e.g. Whole Blood, Plasma

        // ✔ Archive flag (soft delete) - matches TestType/Technician pattern
        public bool IsArchived { get; set; } = false;

        // navigation
        public ICollection<TestType> TestTypes { get; set; } = new List<TestType>();
    }//end of class SampleType
}//end of namespace Project.Models.LabManager