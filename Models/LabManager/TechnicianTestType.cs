namespace Project.Models.LabManager
{
    public class TechnicianTestType //Linking Table
    {
        public int TechnicianId { get; set; } //Fk
        public Technician Technician { get; set; }

        public int TestTypeId { get; set; } //Fk
        public TestType TestType { get; set; }

    }//end of class TechnicianTestType
}//end of namespace Project.Models.LabManager
