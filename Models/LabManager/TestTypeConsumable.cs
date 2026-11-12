namespace Project.Models.LabManager
{
    public class TestTypeConsumable //Linking table
    {
        public int TestTypeId { get; set; } //Fk
        public TestType TestType { get; set; }

        public int ConsumableId { get; set; } //Fk
        public Consumable Consumable { get; set; }

        // quantity of this consumable needed per test
        public int QuantityRequired { get; set; } = 1;

    }//end of class TestTypeConsumable
}//end of namespace Project.Models.LabManager