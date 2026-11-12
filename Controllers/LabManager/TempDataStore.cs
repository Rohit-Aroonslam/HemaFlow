using Project.Models.LabManager;

namespace Project.Models.LabManager
{
    /*
     =========================================================
     🟢 TEMP: Shared in-memory store for UI testing.
            All controllers pull from here so data stays
            consistent across the whole subsystem.
     🔴 DB:  Delete this entire file. Inject ApplicationDbContext
            into each controller instead.
     =========================================================
    */
    public static class TempDataStore
    {
        // ── CATEGORIES ──────────────────────────────────────
        public static List<TestCategory> Categories { get; set; } = new List<TestCategory>
        {
            new TestCategory { Id = 1, Name = "Full Blood Count",    Description = "Routine blood cell count",       IsArchived = false },
            new TestCategory { Id = 2, Name = "Coagulation Studies", Description = "Blood clotting tests",           IsArchived = false },
            new TestCategory { Id = 3, Name = "Differential Count",  Description = "White cell differential tests",  IsArchived = false }
        };

        // ── SAMPLE TYPES ─────────────────────────────────────
        public static List<SampleType> SampleTypes { get; set; } = new List<SampleType>
        {
            new SampleType { Id = 1, Name = "Whole Blood" },
            new SampleType { Id = 2, Name = "Plasma" },
            new SampleType { Id = 3, Name = "Bone Marrow" },
            new SampleType { Id = 4, Name = "Serum" }
        };

        // ── SUPPLIERS ────────────────────────────────────────
        public static List<Supplier> Suppliers { get; set; } = new List<Supplier>
        {
            new Supplier { Id = 1, Name = "Lab Supplies Co",  ContactPerson = "John Smith", Email = "john@labsupplies.co.za",  IsArchived = false },
            new Supplier { Id = 2, Name = "MedTech Supplies", ContactPerson = "Sarah Lee",  Email = "sarah@medtech.co.za",     IsArchived = false },
            new Supplier { Id = 3, Name = "BioPath Reagents", ContactPerson = "Esther Breda",  Email = "Esther@biopath.co.za",     IsArchived = false }
        };

        // ── CONSUMABLES ──────────────────────────────────────
        public static List<Consumable> Consumables { get; set; } = new List<Consumable>
        {
            new Consumable { Id = 1, Name = "Test Tube",   QuantityOnHand = 50, ReorderLevel = 20, SupplierId = 1, IsArchived = false },
            new Consumable { Id = 2, Name = "Reagent A",   QuantityOnHand = 10, ReorderLevel = 15, SupplierId = 2, IsArchived = false },
            new Consumable { Id = 3, Name = "EDTA Tube",   QuantityOnHand = 80, ReorderLevel = 30, SupplierId = 1, IsArchived = false },
            new Consumable { Id = 4, Name = "Citrate Tube",   QuantityOnHand = 10, ReorderLevel = 40, SupplierId = 3, IsArchived = false },
            new Consumable { Id = 5, Name = "Staining Solution",   QuantityOnHand = 5, ReorderLevel = 10, SupplierId = 3, IsArchived = false }
        };

        // ── TEST TYPES ───────────────────────────────────────
        public static List<TestType> TestTypes { get; set; } = new List<TestType>
        {
            new TestType {
                Id = 1, Name = "Full Blood Count",  TestCategoryId = 1, SampleTypeId = 1, Units = "x10³/µL", NormalMin = 4.0m,  NormalMax = 11.0m, TurnaroundTime = 60,  IsArchived = false,
                TestTypeConsumables = new List<TestTypeConsumable>
                {
                    new TestTypeConsumable { TestTypeId = 1, ConsumableId = 3 }, // EDTA Tube - fine
                    new TestTypeConsumable { TestTypeId = 1, ConsumableId = 1 }, // Test Tube - fine
                }
            },
            new TestType {
                Id = 2, Name = "Prothrombin Time",  TestCategoryId = 2, SampleTypeId = 2, Units = "seconds",  NormalMin = 11.0m, NormalMax = 13.5m, TurnaroundTime = 90,  IsArchived = false,
                TestTypeConsumables = new List<TestTypeConsumable>
                {
                    new TestTypeConsumable { TestTypeId = 2, ConsumableId = 3 }, // EDTA Tube - fine
                }
            },
            new TestType {
                Id = 3, Name = "Platelet Count",    TestCategoryId = 1, SampleTypeId = 1, Units = "x10³/µL", NormalMin = 150m,  NormalMax = 400m,  TurnaroundTime = 45,  IsArchived = false,
                TestTypeConsumables = new List<TestTypeConsumable>
                {
                    new TestTypeConsumable { TestTypeId = 3, ConsumableId = 1 }, // Test Tube - fine
                    new TestTypeConsumable { TestTypeId = 3, ConsumableId = 2 }, // Reagent A - LOW STOCK (10 on hand, reorder at 15)
                }
            }
        };


        // ── ORDERS ───────────────────────────────────────────
        // Covers all five filter states: Ordered, Partial, Complete, Cancelled
        // plus a second Partial and a second Ordered to show the filter grouping well
        public static List<Order> Orders { get; set; } = new List<Order>
        {
            // ORD1001 - Partial (one received, one still ordered)
            new Order
            {
                Id = 1, OrderNumber = "ORD1001", SupplierId = 1,
                Supplier  = new Supplier { Id = 1, Name = "Lab Supplies Co" },
                Status    = "Partial",
                OrderDate = DateTime.Now.AddDays(-10),
                OrderItems = new List<OrderItem>
                {
                    new OrderItem { Id = 1,  ConsumableId = 1, Consumable = new Consumable { Name = "Test Tube" },        QuantityOrdered = 100, Status = "Received", DateReceived = DateTime.Now.AddDays(-8) },
                    new OrderItem { Id = 2,  ConsumableId = 2, Consumable = new Consumable { Name = "Reagent A" },        QuantityOrdered = 50,  Status = "Ordered" }
                }
            },

            // ORD1002 - Complete (all items received)
            new Order
            {
                Id = 2, OrderNumber = "ORD1002", SupplierId = 2,
                Supplier      = new Supplier { Id = 2, Name = "MedTech Supplies" },
                Status        = "Complete",
                OrderDate     = DateTime.Now.AddDays(-20),
                DateCompleted = DateTime.Now.AddDays(-15),
                OrderItems = new List<OrderItem>
                {
                    new OrderItem { Id = 3,  ConsumableId = 3, Consumable = new Consumable { Name = "EDTA Tube" },        QuantityOrdered = 200, Status = "Received", DateReceived = DateTime.Now.AddDays(-15) },
                    new OrderItem { Id = 4,  ConsumableId = 5, Consumable = new Consumable { Name = "Staining Solution" },QuantityOrdered = 10,  Status = "Received", DateReceived = DateTime.Now.AddDays(-15) }
                }
            },

            // ORD1003 - Cancelled (full order cancelled with reason)
            new Order
            {
                Id = 3, OrderNumber = "ORD1003", SupplierId = 3,
                Supplier           = new Supplier { Id = 3, Name = "BioPath Reagents" },
                Status             = "Cancelled",
                OrderDate          = DateTime.Now.AddDays(-30),
                DateCancelled      = DateTime.Now.AddDays(-28),
                CancellationReason = "Supplier unable to fulfil order - out of stock",
                OrderItems = new List<OrderItem>
                {
                    new OrderItem { Id = 5,  ConsumableId = 4, Consumable = new Consumable { Name = "Citrate Tube" },     QuantityOrdered = 75,  Status = "Cancelled", CancellationReason = "Supplier unable to fulfil order - out of stock", DateCancelled = DateTime.Now.AddDays(-28) }
                }
            },

            // ORD1004 - Ordered (freshly placed, nothing received yet)
            new Order
            {
                Id = 4, OrderNumber = "ORD1004", SupplierId = 1,
                Supplier  = new Supplier { Id = 1, Name = "Lab Supplies Co" },
                Status    = "Ordered",
                OrderDate = DateTime.Now.AddDays(-2),
                OrderItems = new List<OrderItem>
                {
                    new OrderItem { Id = 6,  ConsumableId = 2, Consumable = new Consumable { Name = "Reagent A" },        QuantityOrdered = 60,  Status = "Ordered" },
                    new OrderItem { Id = 7,  ConsumableId = 4, Consumable = new Consumable { Name = "Citrate Tube" },     QuantityOrdered = 40,  Status = "Ordered" }
                }
            },

            // ORD1005 - Ordered (second fresh order from different supplier)
            new Order
            {
                Id = 5, OrderNumber = "ORD1005", SupplierId = 3,
                Supplier  = new Supplier { Id = 3, Name = "BioPath Reagents" },
                Status    = "Ordered",
                OrderDate = DateTime.Now.AddDays(-1),
                OrderItems = new List<OrderItem>
                {
                    new OrderItem { Id = 8,  ConsumableId = 5, Consumable = new Consumable { Name = "Staining Solution" },QuantityOrdered = 35,  Status = "Ordered" }
                }
            },

            // ORD1006 - Partial (second partial to bulk out that filter)
            new Order
            {
                Id = 6, OrderNumber = "ORD1006", SupplierId = 2,
                Supplier  = new Supplier { Id = 2, Name = "MedTech Supplies" },
                Status    = "Partial",
                OrderDate = DateTime.Now.AddDays(-5),
                OrderItems = new List<OrderItem>
                {
                    new OrderItem { Id = 9,  ConsumableId = 1, Consumable = new Consumable { Name = "Test Tube" },        QuantityOrdered = 150, Status = "Received", DateReceived = DateTime.Now.AddDays(-3) },
                    new OrderItem { Id = 10, ConsumableId = 3, Consumable = new Consumable { Name = "EDTA Tube" },        QuantityOrdered = 80,  Status = "Ordered" },
                    new OrderItem { Id = 11, ConsumableId = 5, Consumable = new Consumable { Name = "Staining Solution" },QuantityOrdered = 20,  Status = "Cancelled", CancellationReason = "No longer required", DateCancelled = DateTime.Now.AddDays(-4) }
                }
            },

            // ORD1007 - Complete (second complete)
            new Order
            {
                Id = 7, OrderNumber = "ORD1007", SupplierId = 1,
                Supplier      = new Supplier { Id = 1, Name = "Lab Supplies Co" },
                Status        = "Complete",
                OrderDate     = DateTime.Now.AddDays(-45),
                DateCompleted = DateTime.Now.AddDays(-40),
                OrderItems = new List<OrderItem>
                {
                    new OrderItem { Id = 12, ConsumableId = 1, Consumable = new Consumable { Name = "Test Tube" },        QuantityOrdered = 500, Status = "Received", DateReceived = DateTime.Now.AddDays(-40) }
                }
            }
        };

        // ── DOCTORS ──────────────────────────────────────────
        public static List<Doctor> Doctors { get; set; } = new List<Doctor>
        {
            new Doctor
            {
                Id = 1,
                Name = "James",
                Surname = "Hartley",
                HPCSANumber = "MP0234871",
                Email = "j.hartley@nmbhdl.co.za",
                ContactNumber = "0412345678",
                Password = "Hx7@mKpQ",
                MustChangePassword = false,
                IsArchived = false
            },
            new Doctor
            {
                Id = 2,
                Name = "Priya",
                Surname = "Naidoo",
                HPCSANumber = "MP0198234",
                Email = "p.naidoo@nmbhdl.co.za",
                ContactNumber = "0829876543",
                Password = "Np3!wVzR",
                MustChangePassword = false,
                IsArchived = false
            },
            new Doctor
            {
                Id = 3,
                Name = "Lindiwe",
                Surname = "Dlamini",
                HPCSANumber = "MP0312456",
                Email = "l.dlamini@nmbhdl.co.za",
                ContactNumber = "0761234567",
                Password = "Dl9#kTmW",
                MustChangePassword = true,
                IsArchived = false
            },
            new Doctor
            {
                Id = 4,
                Name = "Stefan",
                Surname = "van der Berg",
                HPCSANumber = "MP0445123",
                Email = "s.vanderberg@nmbhdl.co.za",
                ContactNumber = "0833456789",
                Password = "Vb2$nQxL",
                MustChangePassword = true,
                IsArchived = false
            }
        };

        // ── TECHNICIANS ──────────────────────────────────────
        // SA ID numbers verified to pass Luhn algorithm
        public static List<Technician> Technicians { get; set; } = new List<Technician>
        {
            new Technician
            {
                Id = 1, Name = "Ayanda", Surname = "Mokoena",
                IdNumber = "9202204720082",   // ✔ Luhn verified
                EmployeeNumber = "EMP0041",
                Email = "a.mokoena@nmbhdl.co.za", ContactNumber = "0712345678",
                Password = "Mk5@rTpN", MustChangePassword = false, IsArchived = false,
                TechnicianTestTypes = new List<TechnicianTestType>
                {
                    new TechnicianTestType { TechnicianId = 1, TestTypeId = 1 },
                    new TechnicianTestType { TechnicianId = 1, TestTypeId = 3 }
                }
            },
            new Technician
            {
                Id = 2, Name = "Riaan", Surname = "Oosthuizen",
                IdNumber = "8807225433103",   // ✔ Luhn verified
                EmployeeNumber = "EMP0042",
                Email = "r.oosthuizen@nmbhdl.co.za", ContactNumber = "0834567890",
                Password = "Oo8#wBzK", MustChangePassword = false, IsArchived = false,
                TechnicianTestTypes = new List<TechnicianTestType>
                {
                    new TechnicianTestType { TechnicianId = 2, TestTypeId = 2 }
                }
            },
            new Technician
            {
                Id = 3, Name = "Fatima", Surname = "Essop",
                IdNumber = "9501184800086",   // ✔ Luhn verified
                EmployeeNumber = "EMP0043",
                Email = "f.essop@nmbhdl.co.za", ContactNumber = "0769876543",
                Password = "Es4!qMvP", MustChangePassword = true, IsArchived = false,
                TechnicianTestTypes = new List<TechnicianTestType>
                {
                    new TechnicianTestType { TechnicianId = 3, TestTypeId = 1 },
                    new TechnicianTestType { TechnicianId = 3, TestTypeId = 2 },
                    new TechnicianTestType { TechnicianId = 3, TestTypeId = 3 }
                }
            },
            new Technician
            {
                Id = 4, Name = "Thabo", Surname = "Sithole",
                IdNumber = "9111105067083",   // ✔ Luhn verified
                EmployeeNumber = "EMP0044",
                Email = "t.sithole@nmbhdl.co.za", ContactNumber = "0821234567",
                Password = "St6@jNwX", MustChangePassword = true, IsArchived = false,
                TechnicianTestTypes = new List<TechnicianTestType>
                {
                    new TechnicianTestType { TechnicianId = 4, TestTypeId = 1 }
                }
            },
            // Matches the real DB account (Users/TechnicianProfile, LT-009) created for
            // live testing - LabTechController resolves CurrentTechnicianId by matching
            // the signed-in user's e-mail against this list (see CurrentTechnicianId),
            // so this is who you actually see as yourself in the LabTech portal.
            new Technician
            {
                Id = 5, Name = "Bantu-Bethu", Surname = "Beya",
                IdNumber = "9805145080084",   // ✔ Luhn verified (matches TechnicianProfile.SaIdNumber)
                EmployeeNumber = "EMP0099",
                Email = "b.beya@hemaflow.co.za", ContactNumber = "0820000000",
                Password = "LabTech#2026", MustChangePassword = false, IsArchived = false,
                TechnicianTestTypes = new List<TechnicianTestType>
                {
                    new TechnicianTestType { TechnicianId = 5, TestTypeId = 1 },
                    new TechnicianTestType { TechnicianId = 5, TestTypeId = 3 }
                }
            }
        };

        // ── LAB MANAGER PROFILE ──────────────────────────────
        // 🟢 TEMP: Single manager account for UI testing
        // 🔴 DB: Pull from Users/Identity table via session
        public static ManagerProfile Profile { get; set; } = new ManagerProfile
        {
            Name = "Marcellos",
            Surname = "Von Buchenroder",
            Email = "m.vonbuchenroder@nmbhdl.co.za",
            ContactNumber = "0413456789",
            Password = "Admin@1234"
        };

        // ── ACTIVITY LOG ─────────────────────────────────────
        public static List<ActivityLog> ActivityLogs { get; set; } = new List<ActivityLog>
        {
            new ActivityLog { Id = 1,  Timestamp = DateTime.Now.AddDays(-10), Action = "OrderPlaced",    EntityType = "Order",        EntityName = "ORD1001", PerformedBy = "Lab Manager", Notes = "Order placed with Lab Supplies Co" },
            new ActivityLog { Id = 2,  Timestamp = DateTime.Now.AddDays(-8),  Action = "ItemReceived",   EntityType = "Order",        EntityName = "ORD1001", PerformedBy = "Lab Manager", Notes = "Test Tube (100 units) received - stock updated" },
            new ActivityLog { Id = 3,  Timestamp = DateTime.Now.AddDays(-20), Action = "OrderPlaced",    EntityType = "Order",        EntityName = "ORD1002", PerformedBy = "Lab Manager", Notes = "Order placed with MedTech Supplies" },
            new ActivityLog { Id = 4,  Timestamp = DateTime.Now.AddDays(-15), Action = "OrderCompleted", EntityType = "Order",        EntityName = "ORD1002", PerformedBy = "Lab Manager", Notes = "All items received - order marked complete" },
            new ActivityLog { Id = 5,  Timestamp = DateTime.Now.AddDays(-30), Action = "OrderPlaced",    EntityType = "Order",        EntityName = "ORD1003", PerformedBy = "Lab Manager", Notes = "Order placed with BioPath Reagents" },
            new ActivityLog { Id = 6,  Timestamp = DateTime.Now.AddDays(-28), Action = "OrderCancelled", EntityType = "Order",        EntityName = "ORD1003", PerformedBy = "Lab Manager", Notes = "Supplier unable to fulfil - out of stock" },
            new ActivityLog { Id = 7,  Timestamp = DateTime.Now.AddDays(-14), Action = "Created",        EntityType = "Doctor",       EntityName = "Dr James Hartley",      PerformedBy = "Lab Manager", Notes = "Account created - credentials emailed" },
            new ActivityLog { Id = 8,  Timestamp = DateTime.Now.AddDays(-12), Action = "Created",        EntityType = "Doctor",       EntityName = "Dr Priya Naidoo",       PerformedBy = "Lab Manager", Notes = "Account created - credentials emailed" },
            new ActivityLog { Id = 9,  Timestamp = DateTime.Now.AddDays(-10), Action = "Created",        EntityType = "Technician",   EntityName = "Ayanda Mokoena",        PerformedBy = "Lab Manager", Notes = "Account created - credentials emailed" },
            new ActivityLog { Id = 10, Timestamp = DateTime.Now.AddDays(-10), Action = "Created",        EntityType = "Technician",   EntityName = "Riaan Oosthuizen",      PerformedBy = "Lab Manager", Notes = "Account created - credentials emailed" },
            new ActivityLog { Id = 11, Timestamp = DateTime.Now.AddDays(-7),  Action = "StockAdjusted",  EntityType = "Consumable",   EntityName = "Reagent A",             PerformedBy = "Lab Manager", Notes = "Stock decreased by 10 - quantity now 10" },
            new ActivityLog { Id = 12, Timestamp = DateTime.Now.AddDays(-5),  Action = "Created",        EntityType = "TestCategory", EntityName = "Differential Count",    PerformedBy = "Lab Manager", Notes = "" },
            new ActivityLog { Id = 13, Timestamp = DateTime.Now.AddDays(-4),  Action = "Archived",       EntityType = "Supplier",     EntityName = "Old Supplies Ltd",      PerformedBy = "Lab Manager", Notes = "Supplier no longer active" },
            new ActivityLog { Id = 14, Timestamp = DateTime.Now.AddDays(-3),  Action = "Created",        EntityType = "TestType",     EntityName = "Platelet Count",        PerformedBy = "Lab Manager", Notes = "" },
            new ActivityLog { Id = 15, Timestamp = DateTime.Now.AddDays(-2),  Action = "OrderPlaced",    EntityType = "Order",        EntityName = "ORD1004", PerformedBy = "Lab Manager", Notes = "Order placed with Lab Supplies Co" },
            new ActivityLog { Id = 16, Timestamp = DateTime.Now.AddDays(-1),  Action = "OrderPlaced",    EntityType = "Order",        EntityName = "ORD1005", PerformedBy = "Lab Manager", Notes = "Order placed with BioPath Reagents" },
            new ActivityLog { Id = 17, Timestamp = DateTime.Now.AddHours(-6), Action = "Updated",        EntityType = "Doctor",       EntityName = "Dr Lindiwe Dlamini",    PerformedBy = "Lab Manager", Notes = "Contact number updated" },
            new ActivityLog { Id = 18, Timestamp = DateTime.Now.AddHours(-2), Action = "Archived",       EntityType = "TestCategory", EntityName = "Obsolete Panel",        PerformedBy = "Lab Manager", Notes = "" },
            new ActivityLog { Id = 19, Timestamp = DateTime.Now.AddHours(-1), Action = "Restored",       EntityType = "TestCategory", EntityName = "Obsolete Panel",        PerformedBy = "Lab Manager", Notes = "Reinstated after review" },
            new ActivityLog { Id = 20, Timestamp = DateTime.Now,              Action = "StockAdjusted",  EntityType = "Consumable",   EntityName = "Citrate Tube",          PerformedBy = "Lab Manager", Notes = "Stock set to 12 - manual correction" }
        };

        // =========================================================
        // 🟢 TEMP: Activity logger helper
        //    Call this from any controller action that changes data.
        //    Usage: TempDataStore.Log("Archived", "Consumable", item.Name, "reason here");
        //
        // 🔴 DB: Replace with a service that writes to the ActivityLogs table.
        // =========================================================
        public static void Log(string action, string entityType, string entityName, string notes = "")
        {
            ActivityLogs.Add(new ActivityLog
            {
                Id = ActivityLogs.Count > 0 ? ActivityLogs.Max(l => l.Id) + 1 : 1,
                Timestamp = DateTime.Now,
                Action = action,
                EntityType = entityType,
                EntityName = entityName,
                PerformedBy = "Lab Manager",    // 🔴 DB: replace with session username
                Notes = notes
            });
        }

    }//end of class TempDataStore
}//end of namespace