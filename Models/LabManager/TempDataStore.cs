//using Project.Models.LabManager;

//namespace Project.Models.LabManager
//{
//    /*
//     =========================================================
//     🟢 TEMP: Shared in-memory store for UI testing.
//     🔴 DB:  Delete this entire file. Inject ApplicationDbContext
//            into each controller instead.
//     =========================================================
//    */
//    public static class TempDataStore
//    {
//        // ── CATEGORIES ──────────────────────────────────────
//        public static List<TestCategory> Categories { get; set; } = new List<TestCategory>
//        {
//            new TestCategory { Id = 1, Name = "Full Blood Count",    Description = "Routine blood cell count",      IsArchived = false },
//            new TestCategory { Id = 2, Name = "Coagulation Studies", Description = "Blood clotting tests",          IsArchived = false },
//            new TestCategory { Id = 3, Name = "Differential Count",  Description = "White cell differential tests", IsArchived = false }
//        };

//        // ── SAMPLE TYPES ─────────────────────────────────────
//        public static List<SampleType> SampleTypes { get; set; } = new List<SampleType>
//        {
//            new SampleType { Id = 1, Name = "Whole Blood" },
//            new SampleType { Id = 2, Name = "Plasma" },
//            new SampleType { Id = 3, Name = "Bone Marrow" },
//            new SampleType { Id = 4, Name = "Serum" }
//        };

//        // ── SUPPLIERS ────────────────────────────────────────
//        public static List<Supplier> Suppliers { get; set; } = new List<Supplier>
//        {
//            new Supplier { Id = 1, Name = "Lab Supplies Co",  ContactPerson = "John Smith",     Email = "john@labsupplies.co.za",  IsArchived = false },
//            new Supplier { Id = 2, Name = "MedTech Supplies", ContactPerson = "Sarah Lee",      Email = "sarah@medtech.co.za",     IsArchived = false },
//            new Supplier { Id = 3, Name = "BioPath Reagents", ContactPerson = "Themba Khumalo", Email = "t.khumalo@biopath.co.za", IsArchived = false }
//        };

//        // ── CONSUMABLES ──────────────────────────────────────
//        public static List<Consumable> Consumables { get; set; } = new List<Consumable>
//        {
//            new Consumable { Id = 1, Name = "Test Tube",         QuantityOnHand = 50,  ReorderLevel = 20, SupplierId = 1, IsArchived = false },
//            new Consumable { Id = 2, Name = "Reagent A",         QuantityOnHand = 10,  ReorderLevel = 15, SupplierId = 2, IsArchived = false },
//            new Consumable { Id = 3, Name = "EDTA Tube",         QuantityOnHand = 80,  ReorderLevel = 30, SupplierId = 1, IsArchived = false },
//            new Consumable { Id = 4, Name = "Citrate Tube",      QuantityOnHand = 12,  ReorderLevel = 25, SupplierId = 3, IsArchived = false },
//            new Consumable { Id = 5, Name = "Staining Solution", QuantityOnHand = 200, ReorderLevel = 50, SupplierId = 3, IsArchived = false }
//        };

//        // ── TEST TYPES ───────────────────────────────────────
//        public static List<TestType> TestTypes { get; set; } = new List<TestType>
//        {
//            new TestType { Id = 1, Name = "Full Blood Count", TestCategoryId = 1, SampleTypeId = 1, Units = "x10³/µL", NormalMin = 4.0m,  NormalMax = 11.0m, TurnaroundTime = 60, IsArchived = false },
//            new TestType { Id = 2, Name = "Prothrombin Time", TestCategoryId = 2, SampleTypeId = 2, Units = "seconds",  NormalMin = 11.0m, NormalMax = 13.5m, TurnaroundTime = 90, IsArchived = false },
//            new TestType { Id = 3, Name = "Platelet Count",   TestCategoryId = 1, SampleTypeId = 1, Units = "x10³/µL", NormalMin = 150m,  NormalMax = 400m,  TurnaroundTime = 45, IsArchived = false }
//        };

//        // ── ORDERS ───────────────────────────────────────────
//        public static List<Order> Orders { get; set; } = new List<Order>
//        {
//            new Order
//            {
//                Id = 1, OrderNumber = "ORD1001", SupplierId = 1,
//                Supplier  = new Supplier { Id = 1, Name = "Lab Supplies Co" },
//                Status = "Partial", OrderDate = DateTime.Now.AddDays(-10),
//                OrderItems = new List<OrderItem>
//                {
//                    new OrderItem { Id = 1, ConsumableId = 1, Consumable = new Consumable { Name = "Test Tube" },         QuantityOrdered = 100, Status = "Received", DateReceived = DateTime.Now.AddDays(-8) },
//                    new OrderItem { Id = 2, ConsumableId = 2, Consumable = new Consumable { Name = "Reagent A" },         QuantityOrdered = 50,  Status = "Ordered" }
//                }
//            },
//            new Order
//            {
//                Id = 2, OrderNumber = "ORD1002", SupplierId = 2,
//                Supplier = new Supplier { Id = 2, Name = "MedTech Supplies" },
//                Status = "Complete", OrderDate = DateTime.Now.AddDays(-20), DateCompleted = DateTime.Now.AddDays(-15),
//                OrderItems = new List<OrderItem>
//                {
//                    new OrderItem { Id = 3, ConsumableId = 3, Consumable = new Consumable { Name = "EDTA Tube" },         QuantityOrdered = 200, Status = "Received", DateReceived = DateTime.Now.AddDays(-15) },
//                    new OrderItem { Id = 4, ConsumableId = 5, Consumable = new Consumable { Name = "Staining Solution" }, QuantityOrdered = 10,  Status = "Received", DateReceived = DateTime.Now.AddDays(-15) }
//                }
//            },
//            new Order
//            {
//                Id = 3, OrderNumber = "ORD1003", SupplierId = 3,
//                Supplier = new Supplier { Id = 3, Name = "BioPath Reagents" },
//                Status = "Cancelled", OrderDate = DateTime.Now.AddDays(-30),
//                DateCancelled = DateTime.Now.AddDays(-28),
//                CancellationReason = "Supplier unable to fulfil - out of stock",
//                OrderItems = new List<OrderItem>
//                {
//                    new OrderItem { Id = 5, ConsumableId = 4, Consumable = new Consumable { Name = "Citrate Tube" }, QuantityOrdered = 75, Status = "Cancelled", CancellationReason = "Supplier unable to fulfil - out of stock", DateCancelled = DateTime.Now.AddDays(-28) }
//                }
//            },
//            new Order
//            {
//                Id = 4, OrderNumber = "ORD1004", SupplierId = 1,
//                Supplier  = new Supplier { Id = 1, Name = "Lab Supplies Co" },
//                Status = "Ordered", OrderDate = DateTime.Now.AddDays(-2),
//                OrderItems = new List<OrderItem>
//                {
//                    new OrderItem { Id = 6,  ConsumableId = 2, Consumable = new Consumable { Name = "Reagent A" },    QuantityOrdered = 60, Status = "Ordered" },
//                    new OrderItem { Id = 7,  ConsumableId = 4, Consumable = new Consumable { Name = "Citrate Tube" }, QuantityOrdered = 40, Status = "Ordered" }
//                }
//            },
//            new Order
//            {
//                Id = 5, OrderNumber = "ORD1005", SupplierId = 3,
//                Supplier  = new Supplier { Id = 3, Name = "BioPath Reagents" },
//                Status = "Ordered", OrderDate = DateTime.Now.AddDays(-1),
//                OrderItems = new List<OrderItem>
//                {
//                    new OrderItem { Id = 8, ConsumableId = 5, Consumable = new Consumable { Name = "Staining Solution" }, QuantityOrdered = 30, Status = "Ordered" }
//                }
//            },
//            new Order
//            {
//                Id = 6, OrderNumber = "ORD1006", SupplierId = 2,
//                Supplier  = new Supplier { Id = 2, Name = "MedTech Supplies" },
//                Status = "Partial", OrderDate = DateTime.Now.AddDays(-5),
//                OrderItems = new List<OrderItem>
//                {
//                    new OrderItem { Id = 9,  ConsumableId = 1, Consumable = new Consumable { Name = "Test Tube" },         QuantityOrdered = 150, Status = "Received",  DateReceived = DateTime.Now.AddDays(-3) },
//                    new OrderItem { Id = 10, ConsumableId = 3, Consumable = new Consumable { Name = "EDTA Tube" },         QuantityOrdered = 80,  Status = "Ordered" },
//                    new OrderItem { Id = 11, ConsumableId = 5, Consumable = new Consumable { Name = "Staining Solution" }, QuantityOrdered = 20,  Status = "Cancelled",  CancellationReason = "No longer required", DateCancelled = DateTime.Now.AddDays(-4) }
//                }
//            },
//            new Order
//            {
//                Id = 7, OrderNumber = "ORD1007", SupplierId = 1,
//                Supplier = new Supplier { Id = 1, Name = "Lab Supplies Co" },
//                Status = "Complete", OrderDate = DateTime.Now.AddDays(-45), DateCompleted = DateTime.Now.AddDays(-40),
//                OrderItems = new List<OrderItem>
//                {
//                    new OrderItem { Id = 12, ConsumableId = 1, Consumable = new Consumable { Name = "Test Tube" }, QuantityOrdered = 500, Status = "Received", DateReceived = DateTime.Now.AddDays(-40) }
//                }
//            }
//        };

//        // ── DOCTORS ──────────────────────────────────────────
//        public static List<Doctor> Doctors { get; set; } = new List<Doctor>
//        {
//            new Doctor { Id = 1, Name = "James",   Surname = "Hartley",      HPCSANumber = "MP0234871", Email = "j.hartley@nmbhdl.co.za",    ContactNumber = "0412345678", Password = "Hx7@mKpQ", MustChangePassword = false, IsArchived = false },
//            new Doctor { Id = 2, Name = "Priya",   Surname = "Naidoo",       HPCSANumber = "MP0198234", Email = "p.naidoo@nmbhdl.co.za",     ContactNumber = "0829876543", Password = "Np3!wVzR", MustChangePassword = false, IsArchived = false },
//            new Doctor { Id = 3, Name = "Lindiwe", Surname = "Dlamini",      HPCSANumber = "MP0312456", Email = "l.dlamini@nmbhdl.co.za",    ContactNumber = "0761234567", Password = "Dl9#kTmW", MustChangePassword = true,  IsArchived = false },
//            new Doctor { Id = 4, Name = "Stefan",  Surname = "van der Berg", HPCSANumber = "MP0445123", Email = "s.vanderberg@nmbhdl.co.za", ContactNumber = "0833456789", Password = "Vb2$nQxL", MustChangePassword = true,  IsArchived = false }
//        };

//        // ── TECHNICIANS ──────────────────────────────────────
//        public static List<Technician> Technicians { get; set; } = new List<Technician>
//        {
//            new Technician
//            {
//                Id = 1, Name = "Ayanda", Surname = "Mokoena",
//                IdNumber = "9202204720082", EmployeeNumber = "EMP0041",
//                Email = "a.mokoena@nmbhdl.co.za", ContactNumber = "0712345678",
//                Password = "Mk5@rTpN", MustChangePassword = false, IsArchived = false,
//                TechnicianTestTypes = new List<TechnicianTestType>
//                {
//                    new TechnicianTestType { TechnicianId = 1, TestTypeId = 1 },
//                    new TechnicianTestType { TechnicianId = 1, TestTypeId = 3 }
//                }
//            },
//            new Technician
//            {
//                Id = 2, Name = "Riaan", Surname = "Oosthuizen",
//                IdNumber = "8807225433103", EmployeeNumber = "EMP0042",
//                Email = "r.oosthuizen@nmbhdl.co.za", ContactNumber = "0834567890",
//                Password = "Oo8#wBzK", MustChangePassword = false, IsArchived = false,
//                TechnicianTestTypes = new List<TechnicianTestType>
//                {
//                    new TechnicianTestType { TechnicianId = 2, TestTypeId = 2 }
//                }
//            },
//            new Technician
//            {
//                Id = 3, Name = "Fatima", Surname = "Essop",
//                IdNumber = "9501184800086", EmployeeNumber = "EMP0043",
//                Email = "f.essop@nmbhdl.co.za", ContactNumber = "0769876543",
//                Password = "Es4!qMvP", MustChangePassword = true, IsArchived = false,
//                TechnicianTestTypes = new List<TechnicianTestType>
//                {
//                    new TechnicianTestType { TechnicianId = 3, TestTypeId = 1 },
//                    new TechnicianTestType { TechnicianId = 3, TestTypeId = 2 },
//                    new TechnicianTestType { TechnicianId = 3, TestTypeId = 3 }
//                }
//            },
//            new Technician
//            {
//                Id = 4, Name = "Thabo", Surname = "Sithole",
//                IdNumber = "9111105067083", EmployeeNumber = "EMP0044",
//                Email = "t.sithole@nmbhdl.co.za", ContactNumber = "0821234567",
//                Password = "St6@jNwX", MustChangePassword = true, IsArchived = false,
//                TechnicianTestTypes = new List<TechnicianTestType>
//                {
//                    new TechnicianTestType { TechnicianId = 4, TestTypeId = 1 }
//                }
//            }
//        };

//        // ── LAB MANAGER PROFILE ──────────────────────────────
//        // 🟢 TEMP: Single manager account for UI testing
//        // 🔴 DB: Pull from Users/Identity table via session
//        public static ManagerProfile Profile { get; set; } = new ManagerProfile
//        {
//            Name = "Nicole",
//            Surname = "von Buchenroder",
//            Email = "n.vonbuchenroder@nmbhdl.co.za",
//            ContactNumber = "0413456789",
//            Password = "Admin@1234"
//        };

//        // ── ACTIVITY LOG ─────────────────────────────────────
//        public static List<ActivityLog> ActivityLogs { get; set; } = new List<ActivityLog>
//        {
//            new ActivityLog { Id = 1,  Timestamp = DateTime.Now.AddDays(-10), Action = "OrderPlaced",    EntityType = "Order",        EntityName = "ORD1001", PerformedBy = "Lab Manager", Notes = "Order placed with Lab Supplies Co" },
//            new ActivityLog { Id = 2,  Timestamp = DateTime.Now.AddDays(-8),  Action = "ItemReceived",   EntityType = "Order",        EntityName = "ORD1001", PerformedBy = "Lab Manager", Notes = "Test Tube (100 units) received - stock updated" },
//            new ActivityLog { Id = 3,  Timestamp = DateTime.Now.AddDays(-20), Action = "OrderPlaced",    EntityType = "Order",        EntityName = "ORD1002", PerformedBy = "Lab Manager", Notes = "Order placed with MedTech Supplies" },
//            new ActivityLog { Id = 4,  Timestamp = DateTime.Now.AddDays(-15), Action = "OrderCompleted", EntityType = "Order",        EntityName = "ORD1002", PerformedBy = "Lab Manager", Notes = "All items received - order marked complete" },
//            new ActivityLog { Id = 5,  Timestamp = DateTime.Now.AddDays(-30), Action = "OrderPlaced",    EntityType = "Order",        EntityName = "ORD1003", PerformedBy = "Lab Manager", Notes = "Order placed with BioPath Reagents" },
//            new ActivityLog { Id = 6,  Timestamp = DateTime.Now.AddDays(-28), Action = "OrderCancelled", EntityType = "Order",        EntityName = "ORD1003", PerformedBy = "Lab Manager", Notes = "Supplier unable to fulfil - out of stock" },
//            new ActivityLog { Id = 7,  Timestamp = DateTime.Now.AddDays(-14), Action = "Created",        EntityType = "Doctor",       EntityName = "Dr James Hartley",      PerformedBy = "Lab Manager", Notes = "Account created - credentials emailed" },
//            new ActivityLog { Id = 8,  Timestamp = DateTime.Now.AddDays(-12), Action = "Created",        EntityType = "Doctor",       EntityName = "Dr Priya Naidoo",       PerformedBy = "Lab Manager", Notes = "Account created - credentials emailed" },
//            new ActivityLog { Id = 9,  Timestamp = DateTime.Now.AddDays(-10), Action = "Created",        EntityType = "Technician",   EntityName = "Ayanda Mokoena",        PerformedBy = "Lab Manager", Notes = "Account created - credentials emailed" },
//            new ActivityLog { Id = 10, Timestamp = DateTime.Now.AddDays(-10), Action = "Created",        EntityType = "Technician",   EntityName = "Riaan Oosthuizen",      PerformedBy = "Lab Manager", Notes = "Account created - credentials emailed" },
//            new ActivityLog { Id = 11, Timestamp = DateTime.Now.AddDays(-7),  Action = "StockAdjusted",  EntityType = "Consumable",   EntityName = "Reagent A",             PerformedBy = "Lab Manager", Notes = "Stock decreased by 10 - quantity now 10" },
//            new ActivityLog { Id = 12, Timestamp = DateTime.Now.AddDays(-5),  Action = "Created",        EntityType = "TestCategory", EntityName = "Differential Count",    PerformedBy = "Lab Manager", Notes = "" },
//            new ActivityLog { Id = 13, Timestamp = DateTime.Now.AddDays(-4),  Action = "Archived",       EntityType = "Supplier",     EntityName = "Old Supplies Ltd",      PerformedBy = "Lab Manager", Notes = "Supplier no longer active" },
//            new ActivityLog { Id = 14, Timestamp = DateTime.Now.AddDays(-3),  Action = "Created",        EntityType = "TestType",     EntityName = "Platelet Count",        PerformedBy = "Lab Manager", Notes = "" },
//            new ActivityLog { Id = 15, Timestamp = DateTime.Now.AddDays(-2),  Action = "OrderPlaced",    EntityType = "Order",        EntityName = "ORD1004", PerformedBy = "Lab Manager", Notes = "Order placed with Lab Supplies Co" },
//            new ActivityLog { Id = 16, Timestamp = DateTime.Now.AddDays(-1),  Action = "OrderPlaced",    EntityType = "Order",        EntityName = "ORD1005", PerformedBy = "Lab Manager", Notes = "Order placed with BioPath Reagents" },
//            new ActivityLog { Id = 17, Timestamp = DateTime.Now.AddHours(-6), Action = "Updated",        EntityType = "Doctor",       EntityName = "Dr Lindiwe Dlamini",    PerformedBy = "Lab Manager", Notes = "Contact number updated" },
//            new ActivityLog { Id = 18, Timestamp = DateTime.Now.AddHours(-2), Action = "Archived",       EntityType = "TestCategory", EntityName = "Obsolete Panel",        PerformedBy = "Lab Manager", Notes = "" },
//            new ActivityLog { Id = 19, Timestamp = DateTime.Now.AddHours(-1), Action = "Restored",       EntityType = "TestCategory", EntityName = "Obsolete Panel",        PerformedBy = "Lab Manager", Notes = "Reinstated after review" },
//            new ActivityLog { Id = 20, Timestamp = DateTime.Now,              Action = "StockAdjusted",  EntityType = "Consumable",   EntityName = "Citrate Tube",          PerformedBy = "Lab Manager", Notes = "Stock set to 12 - manual correction" }
//        };

//        // =========================================================
//        // 🟢 TEMP: Activity logger helper
//        //    Call this from any controller action that changes data.
//        //    Usage: TempDataStore.Log("Archived", "Consumable", item.Name, "reason here");
//        //
//        // 🔴 DB: Replace with a service that writes to the ActivityLogs table.
//        // =========================================================
//        public static void Log(string action, string entityType, string entityName, string notes = "")
//        {
//            ActivityLogs.Add(new ActivityLog
//            {
//                Id = ActivityLogs.Count > 0 ? ActivityLogs.Max(l => l.Id) + 1 : 1,
//                Timestamp = DateTime.Now,
//                Action = action,
//                EntityType = entityType,
//                EntityName = entityName,
//                PerformedBy = "Lab Manager",    // 🔴 DB: replace with session username
//                Notes = notes
//            });
//        }

//    }//end of class TempDataStore
//}//end of namespace