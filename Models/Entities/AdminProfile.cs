using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class AdminProfile
{
    public int UserId { get; set; }

    public string AdminCode { get; set; } = null!;

    public DateOnly? DateOfBirth { get; set; }

    public string? AddressLine1 { get; set; }

    // Optional complex / building / unit name, shown above the street address.
    public string? ComplexName { get; set; }

    // Normalised address: Province -> Town -> Suburb (which carries the postal code).
    public int? SuburbId { get; set; }

    public virtual Suburb? Suburb { get; set; }

    public virtual User User { get; set; } = null!;
}
