// ONE shared DbContext for the whole team (Project.Data.ApplicationDbContext).
// Connection string comes from Program.cs / appsettings.json ("HemaFlowDb") -
// do not hard-code one here. Entities live in Models/Entities.
// Add custom mapping in OnModelCreatingPartial (bottom of file) so it is never lost.
using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Project.Models.Entities;

namespace Project.Data;

public partial class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AdminProfile> AdminProfiles { get; set; }

    public virtual DbSet<Allergy> Allergies { get; set; }

    public virtual DbSet<ApprovalStatus> ApprovalStatuses { get; set; }

    public virtual DbSet<ApprovalType> ApprovalTypes { get; set; }

    public virtual DbSet<ArchivedRecord> ArchivedRecords { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<ConsentedTestRequest> ConsentedTestRequests { get; set; }

    public virtual DbSet<Consumable> Consumables { get; set; }

    public virtual DbSet<DoctorProfile> DoctorProfiles { get; set; }

    public virtual DbSet<DomainType> DomainTypes { get; set; }

    public virtual DbSet<LabManagerProfile> LabManagerProfiles { get; set; }

    public virtual DbSet<LogType> LogTypes { get; set; }

    public virtual DbSet<MedicalCondition> MedicalConditions { get; set; }

    public virtual DbSet<Medication> Medications { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderItem> OrderItems { get; set; }

    public virtual DbSet<OrderItemStatus> OrderItemStatuses { get; set; }

    public virtual DbSet<OrderStatus> OrderStatuses { get; set; }

    public virtual DbSet<PasswordResetToken> PasswordResetTokens { get; set; }

    public virtual DbSet<PatientActivityLog> PatientActivityLogs { get; set; }

    public virtual DbSet<PatientAllergy> PatientAllergies { get; set; }

    public virtual DbSet<PatientCondition> PatientConditions { get; set; }

    public virtual DbSet<PatientDoctorConsent> PatientDoctorConsents { get; set; }

    public virtual DbSet<PatientMedication> PatientMedications { get; set; }

    public virtual DbSet<PatientProfile> PatientProfiles { get; set; }

    public virtual DbSet<PendingApproval> PendingApprovals { get; set; }

    public virtual DbSet<RequestSample> RequestSamples { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<SampleType> SampleTypes { get; set; }

    public virtual DbSet<StaffActivityLog> StaffActivityLogs { get; set; }

    public virtual DbSet<Supplier> Suppliers { get; set; }

    public virtual DbSet<TechnicianProfile> TechnicianProfiles { get; set; }

    public virtual DbSet<TechnicianTestType> TechnicianTestTypes { get; set; }

    public virtual DbSet<TestCategory> TestCategories { get; set; }

    public virtual DbSet<TestConsumable> TestConsumables { get; set; }

    public virtual DbSet<TestRequest> TestRequests { get; set; }

    public virtual DbSet<TestRequestItem> TestRequestItems { get; set; }

    public virtual DbSet<TestRequestItemStatus> TestRequestItemStatuses { get; set; }

    public virtual DbSet<TestRequestStatus> TestRequestStatuses { get; set; }

    public virtual DbSet<TestResult> TestResults { get; set; }

    public virtual DbSet<TestType> TestTypes { get; set; }

    public virtual DbSet<Urgency> Urgencies { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserBackupCode> UserBackupCodes { get; set; }

    public virtual DbSet<UserPasskey> UserPasskeys { get; set; }

    public virtual DbSet<UserTwoFactor> UserTwoFactors { get; set; }

    public virtual DbSet<Verification> Verifications { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AdminProfile>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__AdminPro__1788CC4C73325A07");

            entity.ToTable("AdminProfile");

            entity.HasIndex(e => e.AdminCode, "UQ__AdminPro__08846349BFA40C80").IsUnique();

            entity.Property(e => e.UserId).ValueGeneratedNever();
            entity.Property(e => e.AddressLine1).HasMaxLength(150);
            entity.Property(e => e.ComplexName).HasMaxLength(100);
            entity.Property(e => e.AdminCode)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.User).WithOne(p => p.AdminProfile)
                .HasForeignKey<AdminProfile>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__AdminProf__UserI__02084FDA");
        });

        modelBuilder.Entity<Allergy>(entity =>
        {
            entity.HasKey(e => e.AllergyId).HasName("PK__Allergy__A49EBE42D6880E84");

            entity.ToTable("Allergy");

            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.TypicalReaction).HasMaxLength(300);

            entity.HasOne(d => d.Category).WithMany(p => p.Allergies)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Allergy__Categor__42ACE4D4");
        });

        modelBuilder.Entity<ApprovalStatus>(entity =>
        {
            entity.HasKey(e => e.ApprovalStatusId).HasName("PK__Approval__08E527F8B4ADFC55");

            entity.ToTable("ApprovalStatus");

            entity.HasIndex(e => e.Code, "UQ__Approval__A25C5AA751C2ABD5").IsUnique();

            entity.Property(e => e.Code)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ApprovalType>(entity =>
        {
            entity.HasKey(e => e.ApprovalTypeId).HasName("PK__Approval__CAF9CBEFF0773D53");

            entity.ToTable("ApprovalType");

            entity.HasIndex(e => e.Code, "UQ__Approval__A25C5AA7C11F08E8").IsUnique();

            entity.Property(e => e.Code)
                .HasMaxLength(30)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ArchivedRecord>(entity =>
        {
            entity.HasKey(e => e.ArchivedRecordId).HasName("PK__Archived__8D0A19772EC03776");

            entity.ToTable("ArchivedRecord");

            entity.HasIndex(e => new { e.EntityType, e.ArchivedOn }, "IX_ArchivedRecord_EntityType").IsDescending(false, true);

            entity.Property(e => e.ArchivedOn).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.DisplayName).HasMaxLength(200);
            entity.Property(e => e.EntityType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.Reference).HasMaxLength(200);

            entity.HasOne(d => d.ArchivedByUser).WithMany(p => p.ArchivedRecords)
                .HasForeignKey(d => d.ArchivedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ArchivedR__Archi__1F63A897");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.CategoryId).HasName("PK__Category__19093A0B1F404BF9");

            entity.ToTable("Category");

            entity.HasIndex(e => new { e.SysCode, e.DomainType }, "UQ_Category_SysCode_Domain").IsUnique();

            entity.Property(e => e.DomainType).HasMaxLength(20);
            entity.Property(e => e.Name).HasMaxLength(150);
            entity.Property(e => e.SysCode)
                .HasMaxLength(10)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ConsentedTestRequest>(entity =>
        {
            entity.HasKey(e => e.ConsentedTestRequestId).HasName("PK__Consente__FE2118C17F2406C0");

            entity.ToTable("ConsentedTestRequest");

            entity.HasIndex(e => e.TestRequestItemId, "IX_ConsentedTestRequest_TestRequestItemId");

            entity.HasIndex(e => new { e.PatientDoctorConsentId, e.TestRequestItemId }, "UQ_ConsentedTestRequest").IsUnique();

            entity.Property(e => e.IsConsented).HasDefaultValue(true);

            entity.HasOne(d => d.PatientDoctorConsent).WithMany(p => p.ConsentedTestRequests)
                .HasForeignKey(d => d.PatientDoctorConsentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Consented__Patie__078C1F06");

            entity.HasOne(d => d.TestRequestItem).WithMany(p => p.ConsentedTestRequests)
                .HasForeignKey(d => d.TestRequestItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Consented__TestR__0880433F");
        });

        modelBuilder.Entity<Consumable>(entity =>
        {
            entity.HasKey(e => e.ConsumableId).HasName("PK__Consumab__50EE12F69BE6823D");

            entity.ToTable("Consumable");

            entity.HasIndex(e => e.Name, "UQ_Consumable_Name").IsUnique(); // Lab Manager: spec - name unique

            entity.HasIndex(e => new { e.QuantityOnHand, e.ReorderLevel }, "IX_Consumable_ReorderCheck").HasFilter("([IsArchived]=(0))");

            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.UnitOfMeasure).HasMaxLength(30);

            entity.HasOne(d => d.Supplier).WithMany(p => p.Consumables)
                .HasForeignKey(d => d.SupplierId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Consumabl__Suppl__4A8310C6");
        });

        modelBuilder.Entity<DoctorProfile>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__DoctorPr__1788CC4CACFDF9C7");

            entity.ToTable("DoctorProfile");

            entity.HasIndex(e => e.DoctorCode, "UQ__DoctorPr__2BDC63EBA310AB4C").IsUnique();

            entity.HasIndex(e => e.HpcsaNumber, "UQ__DoctorPr__8AE390CA8BD8E508").IsUnique();

            entity.Property(e => e.UserId).ValueGeneratedNever();
            entity.Property(e => e.DoctorCode)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.HpcsaNumber)
                .HasMaxLength(9)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.Specialization).HasMaxLength(100);

            entity.HasOne(d => d.User).WithOne(p => p.DoctorProfile)
                .HasForeignKey<DoctorProfile>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__DoctorPro__UserI__06CD04F7");
        });

        modelBuilder.Entity<DomainType>(entity =>
        {
            entity.HasKey(e => e.DomainTypeId).HasName("PK__DomainTy__962C588821107431");

            entity.ToTable("DomainType");

            entity.HasIndex(e => e.Code, "UQ__DomainTy__A25C5AA7529C58E4").IsUnique();

            entity.Property(e => e.Code)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Description)
                .HasMaxLength(100)
                .IsUnicode(false);
        });

        modelBuilder.Entity<LabManagerProfile>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__LabManag__1788CC4CE05A0335");

            entity.ToTable("LabManagerProfile");

            entity.HasIndex(e => e.ManagerCode, "UQ__LabManag__634BE7D17A59B377").IsUnique();

            entity.Property(e => e.UserId).ValueGeneratedNever();
            entity.Property(e => e.ManagerCode)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.User).WithOne(p => p.LabManagerProfile)
                .HasForeignKey<LabManagerProfile>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__LabManage__UserI__0B91BA14");
        });

        modelBuilder.Entity<LogType>(entity =>
        {
            entity.HasKey(e => e.LogTypeId).HasName("PK__LogType__AE957494A4E9C1BB");

            entity.ToTable("LogType");

            entity.HasIndex(e => e.Code, "UQ__LogType__A25C5AA75EE50D7C").IsUnique();

            entity.Property(e => e.Code)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<MedicalCondition>(entity =>
        {
            entity.HasKey(e => e.ConditionId).HasName("PK__MedicalC__37F5C0CFB8DF5920");

            entity.ToTable("MedicalCondition");

            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Icd10Code)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Name).HasMaxLength(200);

            entity.HasOne(d => d.Category).WithMany(p => p.MedicalConditions)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__MedicalCo__Categ__3EDC53F0");
        });

        modelBuilder.Entity<Medication>(entity =>
        {
            entity.HasKey(e => e.MedicationId).HasName("PK__Medicati__62EC1AFAC615BDF3");

            entity.ToTable("Medication");

            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Notes).HasMaxLength(1000);

            entity.HasOne(d => d.Category).WithMany(p => p.Medications)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Medicatio__Categ__467D75B8");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.OrderId).HasName("PK__Order__C3905BCF188F2006");

            entity.ToTable("Order");

            entity.HasIndex(e => new { e.SupplierId, e.OrderStatusId }, "IX_Order_SupplierId_Status");

            entity.Property(e => e.PlacedOn).HasDefaultValueSql("(sysutcdatetime())");

            // Lab Manager: order number + cancellation
            entity.HasIndex(e => e.OrderNumber, "UQ_Order_OrderNumber")
                .IsUnique()
                .HasFilter("([OrderNumber] IS NOT NULL)");
            entity.Property(e => e.OrderNumber)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.CancelReason).HasMaxLength(500);

            entity.HasOne(d => d.OrderStatus).WithMany(p => p.Orders)
                .HasForeignKey(d => d.OrderStatusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Order__OrderStat__56E8E7AB");

            entity.HasOne(d => d.PlacedByUser).WithMany(p => p.Orders)
                .HasForeignKey(d => d.PlacedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Order__PlacedByU__57DD0BE4");

            entity.HasOne(d => d.Supplier).WithMany(p => p.Orders)
                .HasForeignKey(d => d.SupplierId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Order__SupplierI__55F4C372");
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.OrderItemId).HasName("PK__OrderIte__57ED0681A260E284");

            entity.ToTable("OrderItem");

            entity.HasIndex(e => e.OrderId, "IX_OrderItem_OrderId");

            entity.Property(e => e.CancelReason).HasMaxLength(500); // Lab Manager

            entity.HasOne(d => d.Consumable).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.ConsumableId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__OrderItem__Consu__5CA1C101");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__OrderItem__Order__5BAD9CC8");

            entity.HasOne(d => d.OrderItemStatus).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.OrderItemStatusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__OrderItem__Order__5E8A0973");
        });

        modelBuilder.Entity<OrderItemStatus>(entity =>
        {
            entity.HasKey(e => e.OrderItemStatusId).HasName("PK__OrderIte__E0C3AD72CD80477B");

            entity.ToTable("OrderItemStatus");

            entity.HasIndex(e => e.Code, "UQ__OrderIte__A25C5AA773998CA9").IsUnique();

            entity.Property(e => e.Code)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<OrderStatus>(entity =>
        {
            entity.HasKey(e => e.OrderStatusId).HasName("PK__OrderSta__BC674CA1D369731A");

            entity.ToTable("OrderStatus");

            entity.HasIndex(e => e.Code, "UQ__OrderSta__A25C5AA7FF13BB6D").IsUnique();

            entity.Property(e => e.Code)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.HasKey(e => e.TokenId).HasName("PK__Password__658FEEEA200DFD37");

            entity.ToTable("PasswordResetToken");

            entity.HasIndex(e => e.Token, "UQ__Password__1EB4F8179C1316AA").IsUnique();

            entity.Property(e => e.CreatedOn).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Token).HasMaxLength(200);

            entity.HasOne(d => d.User).WithMany(p => p.PasswordResetTokens)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PasswordR__UserI__038683F8");
        });

        modelBuilder.Entity<PatientActivityLog>(entity =>
        {
            entity.HasKey(e => e.LogId).HasName("PK__PatientA__5E548648AB645DFE");

            entity.ToTable("PatientActivityLog");

            entity.HasIndex(e => new { e.PatientUserId, e.Timestamp }, "IX_PatientActivityLog_PatientUserId").IsDescending(false, true);

            entity.Property(e => e.Action).HasMaxLength(150);
            entity.Property(e => e.Detail).HasMaxLength(500);
            entity.Property(e => e.Icon)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Timestamp).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.LogType).WithMany(p => p.PatientActivityLogs)
                .HasForeignKey(d => d.LogTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PatientAc__LogTy__1B9317B3");

            entity.HasOne(d => d.PatientUser).WithMany(p => p.PatientActivityLogs)
                .HasForeignKey(d => d.PatientUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PatientAc__Patie__1A9EF37A");
        });

        modelBuilder.Entity<PatientAllergy>(entity =>
        {
            entity.HasKey(e => e.PatientAllergyId).HasName("PK__PatientA__2844413DEDA144EA");

            entity.ToTable("PatientAllergy");

            entity.HasIndex(e => new { e.PatientUserId, e.AllergyId }, "UQ_PatientAllergy").IsUnique();

            entity.Property(e => e.Notes).HasMaxLength(500);

            entity.HasOne(d => d.Allergy).WithMany(p => p.PatientAllergies)
                .HasForeignKey(d => d.AllergyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PatientAl__Aller__6E8B6712");

            entity.HasOne(d => d.PatientUser).WithMany(p => p.PatientAllergies)
                .HasForeignKey(d => d.PatientUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PatientAl__Patie__6D9742D9");
        });

        modelBuilder.Entity<PatientCondition>(entity =>
        {
            entity.HasKey(e => e.PatientConditionId).HasName("PK__PatientC__6961907E96D51FEA");

            entity.ToTable("PatientCondition");

            entity.HasIndex(e => new { e.PatientUserId, e.ConditionId }, "UQ_PatientCondition").IsUnique();

            entity.Property(e => e.Notes).HasMaxLength(500);

            entity.HasOne(d => d.Condition).WithMany(p => p.PatientConditions)
                .HasForeignKey(d => d.ConditionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PatientCo__Condi__69C6B1F5");

            entity.HasOne(d => d.PatientUser).WithMany(p => p.PatientConditions)
                .HasForeignKey(d => d.PatientUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PatientCo__Patie__68D28DBC");
        });

        modelBuilder.Entity<PatientDoctorConsent>(entity =>
        {
            entity.HasKey(e => e.PatientDoctorConsentId).HasName("PK__PatientD__60B3F41C2380E869");

            entity.ToTable("PatientDoctorConsent");

            entity.HasIndex(e => e.DoctorUserId, "IX_PatientDoctorConsent_DoctorUserId");

            entity.HasIndex(e => e.PatientUserId, "IX_PatientDoctorConsent_PatientUserId");

            entity.HasIndex(e => new { e.PatientUserId, e.DoctorUserId }, "UQ_PatientDoctorConsent").IsUnique();

            entity.Property(e => e.GrantedOn).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.DoctorUser).WithMany(p => p.PatientDoctorConsents)
                .HasForeignKey(d => d.DoctorUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PatientDo__Docto__02C769E9");

            entity.HasOne(d => d.PatientUser).WithMany(p => p.PatientDoctorConsents)
                .HasForeignKey(d => d.PatientUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PatientDo__Patie__01D345B0");
        });

        modelBuilder.Entity<PatientMedication>(entity =>
        {
            entity.HasKey(e => e.PatientMedicationId).HasName("PK__PatientM__B08C929464001054");

            entity.ToTable("PatientMedication");

            entity.HasIndex(e => new { e.PatientUserId, e.MedicationId }, "UQ_PatientMedication").IsUnique();

            entity.Property(e => e.Notes).HasMaxLength(500);

            entity.HasOne(d => d.Medication).WithMany(p => p.PatientMedications)
                .HasForeignKey(d => d.MedicationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PatientMe__Medic__73501C2F");

            entity.HasOne(d => d.PatientUser).WithMany(p => p.PatientMedications)
                .HasForeignKey(d => d.PatientUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PatientMe__Patie__725BF7F6");
        });

        modelBuilder.Entity<PatientProfile>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__PatientP__1788CC4CCDE3D527");

            entity.ToTable("PatientProfile");

            entity.HasIndex(e => e.IdNumber, "UQ__PatientP__62DF8033D940A06C").IsUnique();

            entity.HasIndex(e => e.PatientCode, "UQ__PatientP__B9C66DFE233A52BE").IsUnique();

            entity.Property(e => e.UserId).ValueGeneratedNever();
            entity.Property(e => e.AddressLine1).HasMaxLength(150);
            entity.Property(e => e.ComplexName).HasMaxLength(100);
            entity.Property(e => e.IdNumber)
                .HasMaxLength(13)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.PatientCode)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.User).WithOne(p => p.PatientProfile)
                .HasForeignKey<PatientProfile>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PatientPr__UserI__160F4887");
        });

        modelBuilder.Entity<PendingApproval>(entity =>
        {
            entity.HasKey(e => e.PendingApprovalId).HasName("PK__PendingA__A0BEDEBB3A1D7DC1");

            entity.ToTable("PendingApproval");

            entity.Property(e => e.ApprovalStatusId).HasDefaultValue((byte)1);
            entity.Property(e => e.DenyReason).HasMaxLength(500);
            entity.Property(e => e.ExtraInfo).HasMaxLength(1000);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.SubmittedOn).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.ApprovalStatus).WithMany(p => p.PendingApprovals)
                .HasForeignKey(d => d.ApprovalStatusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PendingAp__Appro__4B422AD5");

            entity.HasOne(d => d.ApprovalType).WithMany(p => p.PendingApprovals)
                .HasForeignKey(d => d.ApprovalTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PendingAp__Appro__4A4E069C");

            entity.HasOne(d => d.Category).WithMany(p => p.PendingApprovals)
                .HasForeignKey(d => d.CategoryId)
                .HasConstraintName("FK__PendingAp__Categ__4D2A7347");

            entity.HasOne(d => d.ReviewedByUser).WithMany(p => p.PendingApprovalReviewedByUsers)
                .HasForeignKey(d => d.ReviewedByUserId)
                .HasConstraintName("FK__PendingAp__Revie__5006DFF2");

            entity.HasOne(d => d.SubmittedByUser).WithMany(p => p.PendingApprovalSubmittedByUsers)
                .HasForeignKey(d => d.SubmittedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PendingAp__Submi__4E1E9780");
        });

        modelBuilder.Entity<RequestSample>(entity =>
        {
            entity.HasKey(e => e.RequestSampleId).HasName("PK__RequestS__EB57C0BEE5409F37");

            entity.ToTable("RequestSample");

            entity.HasIndex(e => e.BarcodeLabel, "UQ__RequestS__C3B9F5810D616F91").IsUnique();

            entity.Property(e => e.BarcodeLabel)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.CollectedOn).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.RejectReason).HasMaxLength(500);

            entity.HasOne(d => d.CollectedByUser).WithMany(p => p.RequestSamples)
                .HasForeignKey(d => d.CollectedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__RequestSa__Colle__719CDDE7");

            entity.HasOne(d => d.RejectedByUser).WithMany()
                .HasForeignKey(d => d.RejectedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RequestSample_RejectedByUser");

            entity.HasOne(d => d.SampleType).WithMany(p => p.RequestSamples)
                .HasForeignKey(d => d.SampleTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__RequestSa__Sampl__70A8B9AE");

            entity.HasOne(d => d.TestRequestItem).WithMany(p => p.RequestSamples)
                .HasForeignKey(d => d.TestRequestItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__RequestSa__TestR__6FB49575");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("PK__Role__8AFACE1A431E32B2");

            entity.ToTable("Role");

            entity.HasIndex(e => e.Code, "UQ__Role__A25C5AA71BEF62E8").IsUnique();

            entity.Property(e => e.Code)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.DisplayName)
                .HasMaxLength(30)
                .IsUnicode(false);
        });

        modelBuilder.Entity<SampleType>(entity =>
        {
            entity.HasKey(e => e.SampleTypeId).HasName("PK__SampleTy__4B9609E903CE0CE8");

            entity.ToTable("SampleType");

            entity.Property(e => e.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<StaffActivityLog>(entity =>
        {
            entity.HasKey(e => e.LogId).HasName("PK__StaffAct__5E548648813455B1");

            entity.ToTable("StaffActivityLog");

            entity.HasIndex(e => new { e.ActorId, e.ActorRole }, "IX_StaffActivityLog_Actor");

            entity.HasIndex(e => e.Timestamp, "IX_StaffActivityLog_Timestamp").IsDescending();

            entity.Property(e => e.Action).HasMaxLength(150);
            entity.Property(e => e.Detail).HasMaxLength(500);
            entity.Property(e => e.Timestamp).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Actor).WithMany(p => p.StaffActivityLogs)
                .HasForeignKey(d => d.ActorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__StaffActi__Actor__14E61A24");

            entity.HasOne(d => d.ActorRoleNavigation).WithMany(p => p.StaffActivityLogs)
                .HasForeignKey(d => d.ActorRole)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__StaffActi__Actor__15DA3E5D");

            entity.HasOne(d => d.LogType).WithMany(p => p.StaffActivityLogs)
                .HasForeignKey(d => d.LogTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__StaffActi__LogTy__16CE6296");
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasKey(e => e.SupplierId).HasName("PK__Supplier__4BE666B43577F334");

            entity.ToTable("Supplier");

            entity.HasIndex(e => e.Name, "UQ_Supplier_Name").IsUnique(); // Lab Manager: spec - name unique

            entity.Property(e => e.ContactEmail).HasMaxLength(256);
            entity.Property(e => e.ContactPhone).HasMaxLength(20);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.ContactPerson).HasMaxLength(150); // Lab Manager
        });

        modelBuilder.Entity<TechnicianProfile>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Technici__1788CC4C52444041");

            entity.ToTable("TechnicianProfile");

            entity.HasIndex(e => e.EmployeeCode, "UQ__Technici__1F64254872541C7B").IsUnique();

            // Lab Manager: SA ID number (spec §7 - unique)
            entity.HasIndex(e => e.SaIdNumber, "UQ_TechnicianProfile_SaIdNumber")
                .IsUnique()
                .HasFilter("([SaIdNumber] IS NOT NULL)");
            entity.Property(e => e.SaIdNumber)
                .HasMaxLength(13)
                .IsUnicode(false)
                .IsFixedLength();

            entity.Property(e => e.UserId).ValueGeneratedNever();
            entity.Property(e => e.Department).HasMaxLength(100);
            entity.Property(e => e.EmployeeCode)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.User).WithOne(p => p.TechnicianProfile)
                .HasForeignKey<TechnicianProfile>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Technicia__UserI__10566F31");
        });

        modelBuilder.Entity<TechnicianTestType>(entity =>
        {
            entity.HasKey(e => e.TechnicianTestTypeId).HasName("PK__Technici__AB94EB81957FB413");

            entity.ToTable("TechnicianTestType");

            entity.HasIndex(e => new { e.TechnicianUserId, e.TestTypeId }, "UQ_TechnicianTestType").IsUnique();

            entity.HasOne(d => d.TechnicianUser).WithMany(p => p.TechnicianTestTypes)
                .HasForeignKey(d => d.TechnicianUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Technicia__Techn__43D61337");

            entity.HasOne(d => d.TestType).WithMany(p => p.TechnicianTestTypes)
                .HasForeignKey(d => d.TestTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Technicia__TestT__44CA3770");
        });

        modelBuilder.Entity<TestCategory>(entity =>
        {
            entity.HasKey(e => e.TestCategoryId).HasName("PK__TestCate__2CE372FD8D7165FF");

            entity.ToTable("TestCategory");

            entity.HasIndex(e => e.Name, "UQ_TestCategory_Name").IsUnique(); // Lab Manager: spec - name unique

            entity.Property(e => e.Name).HasMaxLength(150);
            entity.Property(e => e.Description).HasMaxLength(500); // Lab Manager
        });

        modelBuilder.Entity<TestConsumable>(entity =>
        {
            entity.HasKey(e => e.TestConsumableId).HasName("PK__TestCons__0543764B94CDB446");

            entity.ToTable("TestConsumable");

            entity.HasIndex(e => new { e.TestTypeId, e.ConsumableId }, "UQ_TestConsumable").IsUnique();

            entity.Property(e => e.QuantityPerTest).HasDefaultValue(1);

            entity.HasOne(d => d.Consumable).WithMany(p => p.TestConsumables)
                .HasForeignKey(d => d.ConsumableId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__TestConsu__Consu__5224328E");

            entity.HasOne(d => d.TestType).WithMany(p => p.TestConsumables)
                .HasForeignKey(d => d.TestTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__TestConsu__TestT__51300E55");
        });

        modelBuilder.Entity<TestRequest>(entity =>
        {
            entity.HasKey(e => e.TestRequestId).HasName("PK__TestRequ__5E3562864B98E38F");

            entity.ToTable("TestRequest");

            entity.HasIndex(e => new { e.DoctorUserId, e.RequestedOn }, "IX_TestRequest_DoctorUserId").IsDescending(false, true);

            entity.HasIndex(e => new { e.PatientUserId, e.RequestedOn }, "IX_TestRequest_PatientUserId").IsDescending(false, true);

            entity.HasIndex(e => e.TestRequestStatusId, "IX_TestRequest_StatusId");

            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.RequestedOn).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.DoctorUser).WithMany(p => p.TestRequests)
                .HasForeignKey(d => d.DoctorUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__TestReque__Docto__625A9A57");

            entity.HasOne(d => d.PatientUser).WithMany(p => p.TestRequests)
                .HasForeignKey(d => d.PatientUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__TestReque__Patie__6166761E");

            entity.HasOne(d => d.TestRequestStatus).WithMany(p => p.TestRequests)
                .HasForeignKey(d => d.TestRequestStatusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__TestReque__TestR__634EBE90");

            entity.HasOne(d => d.Urgency).WithMany(p => p.TestRequests)
                .HasForeignKey(d => d.UrgencyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__TestReque__Urgen__6442E2C9");
        });

        modelBuilder.Entity<TestRequestItem>(entity =>
        {
            entity.HasKey(e => e.TestRequestItemId).HasName("PK__TestRequ__0A0EA6F42E50053E");

            entity.ToTable("TestRequestItem");

            entity.HasIndex(e => new { e.AssignedTechnicianUserId, e.TestRequestItemStatusId }, "IX_TestRequestItem_AssignedTechnician").HasFilter("([AssignedTechnicianUserId] IS NOT NULL)");

            entity.HasIndex(e => e.TestRequestId, "IX_TestRequestItem_TestRequestId");

            entity.HasIndex(e => new { e.TestRequestId, e.TestTypeId }, "UQ_TestRequestItem").IsUnique();

            entity.HasOne(d => d.AssignedTechnicianUser).WithMany(p => p.TestRequestItems)
                .HasForeignKey(d => d.AssignedTechnicianUserId)
                .HasConstraintName("FK__TestReque__Assig__6BE40491");

            entity.HasOne(d => d.TestRequest).WithMany(p => p.TestRequestItems)
                .HasForeignKey(d => d.TestRequestId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__TestReque__TestR__690797E6");

            entity.HasOne(d => d.TestRequestItemStatus).WithMany(p => p.TestRequestItems)
                .HasForeignKey(d => d.TestRequestItemStatusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__TestReque__TestR__6AEFE058");

            entity.HasOne(d => d.TestType).WithMany(p => p.TestRequestItems)
                .HasForeignKey(d => d.TestTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__TestReque__TestT__69FBBC1F");
        });

        modelBuilder.Entity<TestRequestItemStatus>(entity =>
        {
            entity.HasKey(e => e.TestRequestItemStatusId).HasName("PK__TestRequ__59E9AB5C5C8FE560");

            entity.ToTable("TestRequestItemStatus");

            entity.HasIndex(e => e.Code, "UQ__TestRequ__A25C5AA72C77525B").IsUnique();

            entity.Property(e => e.Code)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<TestRequestStatus>(entity =>
        {
            entity.HasKey(e => e.TestRequestStatusId).HasName("PK__TestRequ__CD309628CF284684");

            entity.ToTable("TestRequestStatus");

            entity.HasIndex(e => e.Code, "UQ__TestRequ__A25C5AA714D6135C").IsUnique();

            entity.Property(e => e.Code)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<TestResult>(entity =>
        {
            entity.HasKey(e => e.TestResultId).HasName("PK__TestResu__E2463587B55AB71E");

            entity.ToTable("TestResult");

            entity.HasIndex(e => e.RecordedByUserId, "IX_TestResult_RecordedByUserId");

            entity.HasIndex(e => e.TestRequestItemId, "UQ__TestResu__0A0EA6F51554BEFF").IsUnique();

            entity.Property(e => e.NormalMaxAtTest).HasColumnType("decimal(10, 3)");
            entity.Property(e => e.NormalMinAtTest).HasColumnType("decimal(10, 3)");
            entity.Property(e => e.RecordedOn).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.ResultValue).HasColumnType("decimal(10, 3)");
            entity.Property(e => e.Unit).HasMaxLength(20);

            entity.HasOne(d => d.RecordedByUser).WithMany(p => p.TestResults)
                .HasForeignKey(d => d.RecordedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__TestResul__Recor__7755B73D");

            entity.HasOne(d => d.TestRequestItem).WithOne(p => p.TestResult)
                .HasForeignKey<TestResult>(d => d.TestRequestItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__TestResul__TestR__76619304");
        });

        modelBuilder.Entity<TestType>(entity =>
        {
            entity.HasKey(e => e.TestTypeId).HasName("PK__TestType__9BB876A61E5272B1");

            entity.ToTable("TestType");

            entity.HasIndex(e => e.Name, "UQ_TestType_Name").IsUnique(); // Lab Manager: spec - name unique

            entity.Property(e => e.Name).HasMaxLength(150);
            entity.Property(e => e.NormalMax).HasColumnType("decimal(10, 3)");
            entity.Property(e => e.NormalMin).HasColumnType("decimal(10, 3)");
            entity.Property(e => e.Unit).HasMaxLength(20);

            entity.HasOne(d => d.SampleType).WithMany(p => p.TestTypes)
                .HasForeignKey(d => d.SampleTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__TestType__Sample__3F115E1A");

            entity.HasOne(d => d.TestCategory).WithMany(p => p.TestTypes)
                .HasForeignKey(d => d.TestCategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__TestType__TestCa__3E1D39E1");
        });

        modelBuilder.Entity<Urgency>(entity =>
        {
            entity.HasKey(e => e.UrgencyId).HasName("PK__Urgency__7A92285AC1E15E80");

            entity.ToTable("Urgency");

            entity.HasIndex(e => e.Code, "UQ__Urgency__A25C5AA7F3C220EE").IsUnique();

            entity.Property(e => e.Code)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Users__1788CC4C880ACAE2");

            entity.HasIndex(e => e.IsArchived, "IX_Users_IsArchived").HasFilter("([IsArchived]=(0))");

            entity.HasIndex(e => e.RoleId, "IX_Users_RoleId");

            entity.HasIndex(e => e.Email, "UQ_Users_Email").IsUnique();

            entity.HasIndex(e => e.UserCode, "UQ_Users_UserCode").IsUnique();

            entity.Property(e => e.CreatedOn).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.Identifier).HasMaxLength(50);
            entity.Property(e => e.PasswordHash).HasMaxLength(256);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Active");
            entity.Property(e => e.Surname).HasMaxLength(100);
            entity.Property(e => e.UserCode)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("");

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Users__RoleId__7A672E12");
        });

        modelBuilder.Entity<UserBackupCode>(entity =>
        {
            entity.HasKey(e => e.BackupCodeId).HasName("PK__UserBack__E8855BF8BA559020");

            entity.ToTable("UserBackupCode");

            entity.Property(e => e.CodeHash).HasMaxLength(200);
            entity.Property(e => e.CreatedOn).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.User).WithMany(p => p.UserBackupCodes)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__UserBacku__UserI__79FD19BE");
        });

        modelBuilder.Entity<UserPasskey>(entity =>
        {
            entity.HasKey(e => e.PasskeyId).HasName("PK__UserPass__D39B1F5DFCF3DBC2");

            entity.ToTable("UserPasskey");

            entity.Property(e => e.AddedOn).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.DeviceName).HasMaxLength(200);
            entity.Property(e => e.DeviceType)
                .HasMaxLength(30)
                .HasDefaultValue("device");

            entity.HasOne(d => d.User).WithMany(p => p.UserPasskeys)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__UserPassk__UserI__7DCDAAA2");
        });

        modelBuilder.Entity<UserTwoFactor>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__UserTwoF__1788CC4C72D8202B");

            entity.ToTable("UserTwoFactor");

            entity.Property(e => e.UserId).ValueGeneratedNever();
            entity.Property(e => e.SecretKey).HasMaxLength(64);

            entity.HasOne(d => d.User).WithOne(p => p.UserTwoFactor)
                .HasForeignKey<UserTwoFactor>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__UserTwoFa__UserI__762C88DA");
        });

        modelBuilder.Entity<Verification>(entity =>
        {
            entity.HasKey(e => e.VerificationId).HasName("PK__Verifica__306D4907EEF9F1C1");

            entity.ToTable("Verification");

            entity.HasIndex(e => new { e.TestResultId, e.VerifiedOn }, "IX_Verification_TestResult");

            entity.Property(e => e.Comment).HasMaxLength(500);
            entity.Property(e => e.Outcome).HasMaxLength(10).IsUnicode(false).HasDefaultValue("Verified");
            entity.Property(e => e.ResultValue).HasColumnType("decimal(10, 3)");
            entity.Property(e => e.VerifiedOn).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.TestResult).WithMany(p => p.Verifications)
                .HasForeignKey(d => d.TestResultId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Verificat__TestR__7C1A6C5A");

            entity.HasOne(d => d.VerifiedByUser).WithMany(p => p.Verifications)
                .HasForeignKey(d => d.VerifiedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Verificat__Verif__7D0E9093");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
