using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace Project.Models.LabManager
{
    public class TestType
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        public int TestCategoryId { get; set; }
        [ValidateNever]
        public TestCategory TestCategory { get; set; }

        [Required]
        public int SampleTypeId { get; set; }
        [ValidateNever]
        public SampleType SampleType { get; set; }

        [Required]
        [StringLength(20)]
        public string Units { get; set; }

        [Required]
        public decimal NormalMin { get; set; }

        [Required]
        public decimal NormalMax { get; set; }

        [Required]
        [Range(1, 100000)]
        public int TurnaroundTime { get; set; }

        // ✔ Archive flag (soft delete)
        public bool IsArchived { get; set; } = false;

        [ValidateNever]
        public ICollection<TestTypeConsumable> TestTypeConsumables { get; set; } = new List<TestTypeConsumable>();
        [ValidateNever]
        public ICollection<TechnicianTestType> TechnicianTestTypes { get; set; } = new List<TechnicianTestType>();
    

}//end of class TestType
}//end of namespace