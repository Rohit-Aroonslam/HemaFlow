using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class UserPasskey
{
    public int PasskeyId { get; set; }

    public int UserId { get; set; }

    public string DeviceName { get; set; } = null!;

    public string DeviceType { get; set; } = null!;

    public DateTime AddedOn { get; set; }

    public DateTime? LastUsedOn { get; set; }

    public virtual User User { get; set; } = null!;
}
