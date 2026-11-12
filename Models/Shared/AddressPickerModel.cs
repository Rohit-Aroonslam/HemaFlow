namespace Project.Models
{
    // What the _AddressPicker partial needs to pre-fill an existing address.
    public class AddressPickerModel
    {
        public int? SuburbId { get; set; }
        public string? City { get; set; }
        public string? Suburb { get; set; }
        public string? PostalCode { get; set; }
    }
}
