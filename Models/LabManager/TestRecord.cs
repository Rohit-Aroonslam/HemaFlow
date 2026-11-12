namespace Project.Models.LabManager
{
    public class TestRecord
    {
        public int Id { get; set; }
        public int TestTypeId { get; set; }
        public TestType TestType { get; set; }

        public DateTime DatePerformed { get; set; }

    }//end of class TestRecord
}//end of namespace Project.Models.LabManager
