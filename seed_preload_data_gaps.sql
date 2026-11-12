/* ============================================================================
   DATABASE USE: run this script once to preload sample data into the HemaFlow
   database (the one set in appsettings.json > ConnectionStrings:HemaFlowDb).

     SSMS:    connect to your server, select your database, open this file, F5.
     sqlcmd:  sqlcmd -S <server> -d <database> -U <user> -P <password> -C -i seed_preload_data_gaps.sql

   Replace <server>, <database>, <user> and <password> with your own values.
   ============================================================================ */

/* ============================================================================
   HEMA FLOW (ONP400) — preload-data gap-fill script
   Generated 2026-10-23 from the live-DB audit against
   "ONP400 Preload Data Requirements.pdf". Run this once, in order, against
   your database. All inserts are additive — nothing existing is deleted or
   overwritten except the 3 stock-quantity UPDATEs in STAGE E (clearly marked).

   Run the whole file in one go (SSMS: F5, or `sqlcmd -S <server>
   -d <database> -U <user> -P <password> -C -i seed_preload_data_gaps.sql`).
   Each stage prints its own confirmation; a final summary block reproduces
   the key preload-checklist counts so you can see pass/fail immediately.
   ============================================================================ */

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

BEGIN TRY
BEGIN TRANSACTION;

DECLARE @now DATETIME2 = SYSUTCDATETIME();
DECLARE @hash NVARCHAR(200) = (SELECT TOP 1 PasswordHash FROM Users WHERE RoleId = 5 ORDER BY UserId DESC);
DECLARE @bc INT = 9000; -- running counter for guaranteed-unique seed barcodes ('SEEDB-9001', ...)

/* ============================================================================
   STAGE A — grow the patient pool from 17 to 42 (need >= 40)
   ============================================================================ */
PRINT '== STAGE A: patients ==';

DECLARE @startCode INT = (SELECT ISNULL(MAX(CAST(SUBSTRING(PatientCode,5,10) AS INT)),0) FROM PatientProfile) + 1;

DECLARE @names TABLE (n INT IDENTITY(1,1), FirstName NVARCHAR(60), Surname NVARCHAR(60), Dob DATE, Phone VARCHAR(15), Addr NVARCHAR(100));
INSERT INTO @names (FirstName, Surname, Dob, Phone, Addr) VALUES
('Lindiwe','Zulu','1985-03-11','0821110001','12 Kingfisher Ave'),
('Pieter','Botha','1979-07-22','0821110002','45 Protea Street'),
('Naledi','Moloi','1992-11-02','0821110003','8 Jacaranda Close'),
('Farhana','Ismail','1988-05-19','0821110004','21 Rose Lane'),
('Thabo','Sithole','1975-01-30','0821110005','5 Oak Avenue'),
('Charlene','Fortuin','1990-09-14','0821110006','67 Milnerton Road'),
('Mpho','Tlhapi','1983-06-08','0821110007','14 Baobab Street'),
('Riaan','Kruger','1970-12-25','0821110008','3 Vineyard Road'),
('Zinhle','Buthelezi','1995-02-17','0821110009','19 Sunset Blvd'),
('Werner','Nel','1982-08-03','0821110010','27 Church Street'),
('Amahle','Dube','1998-04-21','0821110011','9 Lily Avenue'),
('Imraan','Cassim','1977-10-11','0821110012','33 Market Street'),
('Suzette','Human','1986-03-29','0821110013','41 Beach Road'),
('Kagiso','Mofokeng','1993-07-07','0821110014','16 Hill Crescent'),
('Daniel','Visagie','1965-05-05','0821110015','52 Central Avenue'),
('Palesa','Seabe','1991-01-13','0821110016','7 Orchid Street'),
('Yusuf','Adams','1980-09-09','0821110017','23 Harbour View'),
('Michelle','Daniels','1996-12-02','0821110018','11 Forest Drive'),
('Bongani','Khumalo','1973-11-27','0821110019','38 Riverside Road'),
('Elzabe','Coetzee','1989-02-14','0821110020','6 Garden Way'),
('Tumi','Mahlatsi','1994-06-30','0821110021','29 Palm Street'),
('Faizel','Omar','1984-03-03','0821110022','44 Marina Drive'),
('Candice','Abrahams','1978-08-18','0821110023','15 Vista Lane'),
('Sanele','Ngcobo','1999-05-24','0821110024','2 Spring Avenue'),
('Herman','Fourie','1968-10-05','0821110025','58 Lakeview Road');

DECLARE @i INT = 1, @n INT = (SELECT COUNT(*) FROM @names);
DECLARE @first NVARCHAR(60), @last NVARCHAR(60), @dob DATE, @phone VARCHAR(15), @addr NVARCHAR(100);
DECLARE @userId INT, @code VARCHAR(20), @idnum CHAR(13), @suburb INT;

WHILE @i <= @n
BEGIN
    SELECT @first=FirstName, @last=Surname, @dob=Dob, @phone=Phone, @addr=Addr FROM @names WHERE n=@i;
    SET @code = 'PAT-' + RIGHT('0000' + CAST(@startCode + @i - 1 AS VARCHAR(10)), 4);
    SET @idnum = FORMAT(@dob,'yyMMdd') + RIGHT('0000' + CAST(1000 + @i AS VARCHAR(10)),4) + '085';
    SET @suburb = 1094 + (@i * 97) % 3877;

    INSERT INTO Users (RoleId, FirstName, Surname, Email, Phone, PasswordHash, MustChangePassword, TwoFactorEnabled, IsArchived, CreatedOn, UserCode, Identifier, Status)
    VALUES (5, @first, @last, LOWER(@first + '.' + @last + @code) + '@example.com', @phone, @hash, 0, 0, 0, @now, @code, @idnum, 'Active');
    SET @userId = SCOPE_IDENTITY();

    INSERT INTO PatientProfile (UserId, PatientCode, IdNumber, DateOfBirth, AddressLine1, IsArchived, SuburbId, ComplexName)
    VALUES (@userId, @code, @idnum, @dob, @addr, 0, @suburb, NULL);

    SET @i = @i + 1;
END

PRINT 'Patients added: ' + CAST(@n AS VARCHAR(10));

/* ============================================================================
   STAGE B — Dr Kiara Brown (DoctorUserId 17): fix status-count gaps and make
   every one of her requests multi-item ("each consisting of multiple tests").
   Reuses her existing patients (35/36) for the new requests.
   ============================================================================ */
PRINT '== STAGE B: Dr Kiara Brown (UserId 17) ==';

-- B1: give every one of her 14 single-item requests a second TestRequestItem,
-- chosen to keep each request's existing status logically consistent.
DECLARE @fix TABLE (ReqId INT, NewStatus TINYINT, NewTestType INT, Captured BIT);
INSERT INTO @fix (ReqId, NewStatus, NewTestType, Captured) VALUES
(78, 1, 25, 0),   -- Pending -> Awaiting
(85, 1, 25, 0),
(99, 1, 25, 0),
(224,1, 25, 0),
(86, 1, 25, 0),   -- Cancelled -> sibling stays Awaiting (same pattern as its existing item)
(100,2, 25, 0),   -- SamplesReceived -> SampleCollected
(64, 3, 25, 0),   -- InProgress -> Testing (keeps request validly InProgress)
(101,3, 25, 0),
(102,3, 25, 0),
(225,3, 25, 0),
(226,3, 25, 0),
(227,3, 25, 0),
(103,6, 25, 1),   -- Completed -> new item also Completed (captured), keeps request Completed
(228,6, 25, 1);

DECLARE @fReq INT, @fStatus TINYINT, @fType INT, @fCaptured BIT, @fItemId INT, @fSampleType INT;
DECLARE fix_cur CURSOR FOR SELECT ReqId, NewStatus, NewTestType, Captured FROM @fix;
OPEN fix_cur; FETCH NEXT FROM fix_cur INTO @fReq, @fStatus, @fType, @fCaptured;
WHILE @@FETCH_STATUS = 0
BEGIN
    SELECT @fSampleType = SampleTypeId FROM TestType WHERE TestTypeId = @fType;

    INSERT INTO TestRequestItem (TestRequestId, TestTypeId, TestRequestItemStatusId, AssignedTechnicianUserId, StartedOn, CompletedOn)
    VALUES (@fReq, @fType, @fStatus,
            CASE WHEN @fStatus >= 3 THEN 9 ELSE NULL END,
            CASE WHEN @fStatus >= 3 THEN DATEADD(DAY,-3,@now) ELSE NULL END,
            CASE WHEN @fCaptured = 1 THEN DATEADD(DAY,-2,@now) ELSE NULL END);
    SET @fItemId = SCOPE_IDENTITY();

    SET @bc = @bc + 1;
    INSERT INTO RequestSample (TestRequestItemId, SampleTypeId, BarcodeLabel, CollectedByUserId, CollectedOn, ReceivedOn)
    VALUES (@fItemId, @fSampleType, 'SEEDB-' + CAST(@bc AS VARCHAR(10)),
            CASE WHEN @fStatus >= 2 THEN 9 ELSE NULL END,
            @now,
            CASE WHEN @fStatus >= 2 THEN DATEADD(DAY,-3,@now) ELSE NULL END);

    IF @fCaptured = 1
    BEGIN
        INSERT INTO TestResult (TestRequestItemId, ResultValue, Unit, NormalMinAtTest, NormalMaxAtTest, RecordedByUserId, RecordedOn, Notes)
        VALUES (@fItemId, 6.5, 'x10?/L', 4.000, 11.000, 9, DATEADD(DAY,-2,@now), NULL);
    END

    FETCH NEXT FROM fix_cur INTO @fReq, @fStatus, @fType, @fCaptured;
END
CLOSE fix_cur; DEALLOCATE fix_cur;
PRINT 'Second item added to all 14 single-item requests.';

-- B2: +4 new "SamplesReceived" requests (2 to 6) and +1 new "Cancelled" request (1 to 2), each multi-item.
DECLARE @k INT = 1;
WHILE @k <= 4
BEGIN
    DECLARE @reqB2 INT;
    INSERT INTO TestRequest (PatientUserId, DoctorUserId, TestRequestStatusId, UrgencyId, RequestedOn, Notes)
    VALUES (CASE WHEN @k % 2 = 0 THEN 35 ELSE 36 END, 17, 5, 1, DATEADD(DAY,-4,@now), 'Seed: samples received');
    SET @reqB2 = SCOPE_IDENTITY();

    DECLARE @itB2a INT, @itB2b INT;
    INSERT INTO TestRequestItem (TestRequestId, TestTypeId, TestRequestItemStatusId) VALUES (@reqB2, 1, 2);
    SET @itB2a = SCOPE_IDENTITY();
    INSERT INTO TestRequestItem (TestRequestId, TestTypeId, TestRequestItemStatusId) VALUES (@reqB2, 24, 2);
    SET @itB2b = SCOPE_IDENTITY();

    SET @bc = @bc + 1;
    INSERT INTO RequestSample (TestRequestItemId, SampleTypeId, BarcodeLabel, CollectedByUserId, CollectedOn, ReceivedOn)
    VALUES (@itB2a, (SELECT SampleTypeId FROM TestType WHERE TestTypeId=1), 'SEEDB-' + CAST(@bc AS VARCHAR(10)), 9, @now, DATEADD(DAY,-4,@now));
    SET @bc = @bc + 1;
    INSERT INTO RequestSample (TestRequestItemId, SampleTypeId, BarcodeLabel, CollectedByUserId, CollectedOn, ReceivedOn)
    VALUES (@itB2b, (SELECT SampleTypeId FROM TestType WHERE TestTypeId=24), 'SEEDB-' + CAST(@bc AS VARCHAR(10)), 9, @now, DATEADD(DAY,-4,@now));

    SET @k = @k + 1;
END
PRINT '4 new SamplesReceived requests added for doctor 17.';

DECLARE @reqCancel INT;
INSERT INTO TestRequest (PatientUserId, DoctorUserId, TestRequestStatusId, UrgencyId, RequestedOn, CancelReason, CancelledOn, Notes)
VALUES (35, 17, 4, 1, DATEADD(DAY,-6,@now), 'Seed: patient withdrew consent for these tests', DATEADD(DAY,-5,@now), 'Seed: cancelled');
SET @reqCancel = SCOPE_IDENTITY();
DECLARE @itCancelA INT, @itCancelB INT;
INSERT INTO TestRequestItem (TestRequestId, TestTypeId, TestRequestItemStatusId) VALUES (@reqCancel, 6, 1);
SET @itCancelA = SCOPE_IDENTITY();
INSERT INTO TestRequestItem (TestRequestId, TestTypeId, TestRequestItemStatusId) VALUES (@reqCancel, 7, 1);
SET @itCancelB = SCOPE_IDENTITY();
SET @bc = @bc + 1;
INSERT INTO RequestSample (TestRequestItemId, SampleTypeId, BarcodeLabel) VALUES (@itCancelA, (SELECT SampleTypeId FROM TestType WHERE TestTypeId=6), 'SEEDB-' + CAST(@bc AS VARCHAR(10)));
SET @bc = @bc + 1;
INSERT INTO RequestSample (TestRequestItemId, SampleTypeId, BarcodeLabel) VALUES (@itCancelB, (SELECT SampleTypeId FROM TestType WHERE TestTypeId=7), 'SEEDB-' + CAST(@bc AS VARCHAR(10)));
PRINT '1 new Cancelled request added for doctor 17.';

/* ============================================================================
   STAGE C — give every test type at least 2 assigned technicians
   ============================================================================ */
PRINT '== STAGE C: technician coverage per test type ==';

DECLARE @techs TABLE (n INT IDENTITY(1,1), TechId INT);
INSERT INTO @techs (TechId) VALUES (9),(10),(13),(14),(15),(56),(57),(45);

DECLARE @gaps TABLE (TestTypeId INT, NeedCount INT);
INSERT INTO @gaps (TestTypeId, NeedCount) VALUES
(6,2),(7,2),(8,2),(9,2),(10,2),(11,2),(14,2),(15,2),(17,2),(18,2),(19,2),(20,2),(21,2),(23,2),(24,2),(25,2),(26,2),
(12,1),(16,1);

DECLARE @ttId INT, @need INT, @techPick INT, @techIdx INT = 1, @done INT;
DECLARE gap_cur CURSOR FOR SELECT TestTypeId, NeedCount FROM @gaps;
OPEN gap_cur; FETCH NEXT FROM gap_cur INTO @ttId, @need;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @done = 0;
    WHILE @done < @need
    BEGIN
        SELECT @techPick = TechId FROM @techs WHERE n = ((@techIdx - 1) % 8) + 1;
        SET @techIdx = @techIdx + 1;
        IF NOT EXISTS (SELECT 1 FROM TechnicianTestType WHERE TechnicianUserId=@techPick AND TestTypeId=@ttId)
        BEGIN
            INSERT INTO TechnicianTestType (TechnicianUserId, TestTypeId) VALUES (@techPick, @ttId);
            SET @done = @done + 1;
        END
    END
    FETCH NEXT FROM gap_cur INTO @ttId, @need;
END
CLOSE gap_cur; DEALLOCATE gap_cur;
PRINT '19 under-assigned test types now have >= 2 technicians each.';

/* ============================================================================
   STAGE D — one more "Placed" order (3 total) with multiple items
   ============================================================================ */
PRINT '== STAGE D: extra Placed order ==';

DECLARE @orderId INT;
INSERT INTO [Order] (SupplierId, OrderStatusId, PlacedByUserId, PlacedOn, OrderNumber)
VALUES (1, 2, 24, DATEADD(DAY,-1,@now), 'ORD1011');
SET @orderId = SCOPE_IDENTITY();
INSERT INTO OrderItem (OrderId, ConsumableId, QuantityOrdered, QuantityReceived, OrderItemStatusId) VALUES (@orderId, 1, 100, 0, 1);
INSERT INTO OrderItem (OrderId, ConsumableId, QuantityOrdered, QuantityReceived, OrderItemStatusId) VALUES (@orderId, 8, 50, 0, 1);
PRINT 'Order ORD1011 (Placed, 2 items) added.';

/* ============================================================================
   STAGE E — a 2nd low-stock consumable per supplier 1, 2, 4
   (the only UPDATEs in this file — small, reversible quantity changes)
   ============================================================================ */
PRINT '== STAGE E: low-stock padding ==';

UPDATE Consumable SET QuantityOnHand = 150 WHERE ConsumableId = 28; -- Serum Separator Tube, supplier 1, reorder 150
UPDATE Consumable SET QuantityOnHand = 6   WHERE ConsumableId = 19; -- Haematology Control (3-level), supplier 2, reorder 6
UPDATE Consumable SET QuantityOnHand = 3   WHERE ConsumableId = 17; -- Folate Assay Kit, supplier 4, reorder 3
PRINT 'Suppliers 1, 2 and 4 each now have 2 low-stock consumables.';

/* ============================================================================
   STAGE F — patient medical-history depth
   Patient 37: -> 3 conditions / 2 allergies / 10 medications (the named
               "Patient" scenario; also one of Dr Kiara's patients).
   Patient 38: -> 2 conditions / 2 allergies / 2 medications (Dr Kiara's 2nd
               patient with "multiple" of each).
   ============================================================================ */
PRINT '== STAGE F: patient medical history ==';

INSERT INTO PatientCondition (PatientUserId, ConditionId) VALUES (37,2),(37,3);
INSERT INTO PatientAllergy  (PatientUserId, AllergyId, Severity) VALUES (37,1,'Moderate'),(37,2,'Severe');
INSERT INTO PatientMedication (PatientUserId, MedicationId, Frequency) VALUES
(37,1,'Once daily'),(37,2,'Once daily'),(37,3,'Twice daily'),(37,4,'Once daily'),(37,5,'As needed'),
(37,6,'Once daily'),(37,7,'Once daily'),(37,8,'Twice daily'),(37,9,'Once daily'),(37,10,'Monthly injection');

INSERT INTO PatientCondition (PatientUserId, ConditionId) VALUES (38,4),(38,5);
INSERT INTO PatientAllergy  (PatientUserId, AllergyId, Severity) VALUES (38,3,'Mild');
INSERT INTO PatientMedication (PatientUserId, MedicationId, Frequency) VALUES (38,1,'Once daily');
PRINT 'Patient 37 -> 3 conditions / 2 allergies / 10 medications. Patient 38 -> 2 / 2 / 2.';

/* ============================================================================
   STAGE G — real workload for the named technician Bantu-Bethu Beya
   (UserId 45, b.beya@hemaflow.co.za). All items use PatientUserId 37, which
   already has rich medical history from Stage F, satisfying "all patients
   associated with the technician's tests must have medical history".
   ============================================================================ */
PRINT '== STAGE G: technician workload for UserId 45 ==';

-- G1: 5 "Testing" items (in progress, assigned to 45) on one new InProgress request.
DECLARE @reqG1 INT;
INSERT INTO TestRequest (PatientUserId, DoctorUserId, TestRequestStatusId, UrgencyId, RequestedOn)
VALUES (37, 16, 2, 1, DATEADD(DAY,-2,@now));
SET @reqG1 = SCOPE_IDENTITY();

DECLARE @g1Types TABLE (n INT IDENTITY(1,1), TestTypeId INT);
INSERT INTO @g1Types (TestTypeId) VALUES (12),(13),(16),(28),(29);
DECLARE @g1n INT = 1, @g1Type INT, @g1Item INT, @g1Sample INT;
WHILE @g1n <= 5
BEGIN
    SELECT @g1Type = TestTypeId FROM @g1Types WHERE n = @g1n;
    SELECT @g1Sample = SampleTypeId FROM TestType WHERE TestTypeId = @g1Type;
    INSERT INTO TestRequestItem (TestRequestId, TestTypeId, TestRequestItemStatusId, AssignedTechnicianUserId, StartedOn)
    VALUES (@reqG1, @g1Type, 3, 45, DATEADD(HOUR,-4,@now));
    SET @g1Item = SCOPE_IDENTITY();
    SET @bc = @bc + 1;
    INSERT INTO RequestSample (TestRequestItemId, SampleTypeId, BarcodeLabel, CollectedByUserId, CollectedOn, ReceivedOn)
    VALUES (@g1Item, @g1Sample, 'SEEDB-' + CAST(@bc AS VARCHAR(10)), 45, @now, DATEADD(HOUR,-6,@now));
    SET @g1n = @g1n + 1;
END
PRINT '5 Testing items assigned to technician 45.';

-- G2: 2 "Completed" items captured by 45, awaiting verification - one fresh,
-- one on its 3rd verification attempt (2 prior "Returned" rounds already).
DECLARE @reqG2 INT;
INSERT INTO TestRequest (PatientUserId, DoctorUserId, TestRequestStatusId, UrgencyId, RequestedOn)
VALUES (37, 16, 2, 1, DATEADD(DAY,-3,@now));
SET @reqG2 = SCOPE_IDENTITY();

-- fresh (TestTypeId 2, Platelet Count)
DECLARE @g2ItemA INT, @g2ResA INT;
INSERT INTO TestRequestItem (TestRequestId, TestTypeId, TestRequestItemStatusId, AssignedTechnicianUserId, StartedOn, CompletedOn)
VALUES (@reqG2, 2, 6, 45, DATEADD(DAY,-1,@now), DATEADD(HOUR,-2,@now));
SET @g2ItemA = SCOPE_IDENTITY();
SET @bc = @bc + 1;
INSERT INTO RequestSample (TestRequestItemId, SampleTypeId, BarcodeLabel, CollectedByUserId, CollectedOn, ReceivedOn)
VALUES (@g2ItemA, 5, 'SEEDB-' + CAST(@bc AS VARCHAR(10)), 45, @now, DATEADD(DAY,-1,@now));
INSERT INTO TestResult (TestRequestItemId, ResultValue, Unit, NormalMinAtTest, NormalMaxAtTest, RecordedByUserId, RecordedOn)
VALUES (@g2ItemA, 220, 'x10?/L', 150.000, 400.000, 45, DATEADD(HOUR,-2,@now));
SET @g2ResA = SCOPE_IDENTITY();

-- 3rd-attempt (TestTypeId 4, Haemoglobin) - 2 prior Returned rounds already recorded.
DECLARE @g2ItemB INT, @g2ResB INT;
INSERT INTO TestRequestItem (TestRequestId, TestTypeId, TestRequestItemStatusId, AssignedTechnicianUserId, StartedOn, CompletedOn)
VALUES (@reqG2, 4, 6, 45, DATEADD(DAY,-4,@now), DATEADD(HOUR,-1,@now));
SET @g2ItemB = SCOPE_IDENTITY();
SET @bc = @bc + 1;
INSERT INTO RequestSample (TestRequestItemId, SampleTypeId, BarcodeLabel, CollectedByUserId, CollectedOn, ReceivedOn)
VALUES (@g2ItemB, 5, 'SEEDB-' + CAST(@bc AS VARCHAR(10)), 45, @now, DATEADD(DAY,-4,@now));
INSERT INTO TestResult (TestRequestItemId, ResultValue, Unit, NormalMinAtTest, NormalMaxAtTest, RecordedByUserId, RecordedOn)
VALUES (@g2ItemB, 14.0, 'g/dL', 12.000, 16.500, 45, DATEADD(HOUR,-1,@now));
SET @g2ResB = SCOPE_IDENTITY();
INSERT INTO Verification (TestResultId, VerifiedByUserId, VerifiedOn, Comment, Outcome, ResultValue, ResultRecordedByUserId, ResultRecordedOn)
VALUES (@g2ResB, 9, DATEADD(DAY,-3,@now), 'Please recheck - value looked inconsistent with history', 'Returned', 13.8, 45, DATEADD(DAY,-4,@now));
INSERT INTO Verification (TestResultId, VerifiedByUserId, VerifiedOn, Comment, Outcome, ResultValue, ResultRecordedByUserId, ResultRecordedOn)
VALUES (@g2ResB, 9, DATEADD(DAY,-2,@now), 'Still looks off, please recapture again', 'Returned', 13.9, 45, DATEADD(DAY,-3,@now));
PRINT '2 Completed (awaiting-verification) items for technician 45, one on its 3rd verification attempt.';

-- G3: 2 "ToBeReviewed" items captured by 45, awaiting recapture - one fresh
-- (returned once), one on its 3rd recapture attempt (returned twice already).
DECLARE @reqG3 INT;
INSERT INTO TestRequest (PatientUserId, DoctorUserId, TestRequestStatusId, UrgencyId, RequestedOn)
VALUES (37, 16, 2, 1, DATEADD(DAY,-3,@now));
SET @reqG3 = SCOPE_IDENTITY();

-- fresh (TestTypeId 30, D-Dimer) - returned once so far.
DECLARE @g3ItemA INT, @g3ResA INT;
INSERT INTO TestRequestItem (TestRequestId, TestTypeId, TestRequestItemStatusId, AssignedTechnicianUserId, StartedOn, CompletedOn)
VALUES (@reqG3, 30, 7, 45, DATEADD(DAY,-2,@now), DATEADD(DAY,-1,@now));
SET @g3ItemA = SCOPE_IDENTITY();
SET @bc = @bc + 1;
INSERT INTO RequestSample (TestRequestItemId, SampleTypeId, BarcodeLabel, CollectedByUserId, CollectedOn, ReceivedOn)
VALUES (@g3ItemA, 6, 'SEEDB-' + CAST(@bc AS VARCHAR(10)), 45, @now, DATEADD(DAY,-2,@now));
INSERT INTO TestResult (TestRequestItemId, ResultValue, Unit, NormalMinAtTest, NormalMaxAtTest, RecordedByUserId, RecordedOn)
VALUES (@g3ItemA, 0.8, 'mg/L FEU', 0.000, 0.500, 45, DATEADD(DAY,-1,@now));
SET @g3ResA = SCOPE_IDENTITY();
INSERT INTO Verification (TestResultId, VerifiedByUserId, VerifiedOn, Comment, Outcome, ResultValue, ResultRecordedByUserId, ResultRecordedOn)
VALUES (@g3ResA, 9, DATEADD(HOUR,-12,@now), 'Value seems high for this patient - please recheck', 'Returned', 0.8, 45, DATEADD(DAY,-1,@now));

-- 3rd-attempt (TestTypeId 48, RDW) - returned twice already.
DECLARE @g3ItemB INT, @g3ResB INT;
INSERT INTO TestRequestItem (TestRequestId, TestTypeId, TestRequestItemStatusId, AssignedTechnicianUserId, StartedOn, CompletedOn)
VALUES (@reqG3, 48, 7, 45, DATEADD(DAY,-5,@now), DATEADD(DAY,-1,@now));
SET @g3ItemB = SCOPE_IDENTITY();
SET @bc = @bc + 1;
INSERT INTO RequestSample (TestRequestItemId, SampleTypeId, BarcodeLabel, CollectedByUserId, CollectedOn, ReceivedOn)
VALUES (@g3ItemB, 5, 'SEEDB-' + CAST(@bc AS VARCHAR(10)), 45, @now, DATEADD(DAY,-5,@now));
INSERT INTO TestResult (TestRequestItemId, ResultValue, Unit, NormalMinAtTest, NormalMaxAtTest, RecordedByUserId, RecordedOn)
VALUES (@g3ItemB, 14.0, '%', 11.500, 14.500, 45, DATEADD(DAY,-1,@now));
SET @g3ResB = SCOPE_IDENTITY();
INSERT INTO Verification (TestResultId, VerifiedByUserId, VerifiedOn, Comment, Outcome, ResultValue, ResultRecordedByUserId, ResultRecordedOn)
VALUES (@g3ResB, 9, DATEADD(DAY,-4,@now), 'Recheck requested - borderline value', 'Returned', 14.2, 45, DATEADD(DAY,-5,@now));
INSERT INTO Verification (TestResultId, VerifiedByUserId, VerifiedOn, Comment, Outcome, ResultValue, ResultRecordedByUserId, ResultRecordedOn)
VALUES (@g3ResB, 9, DATEADD(DAY,-2,@now), 'Still borderline, one more recapture please', 'Returned', 14.1, 45, DATEADD(DAY,-4,@now));
PRINT '2 ToBeReviewed (awaiting-recapture) items for technician 45, one on its 3rd recapture attempt.';

-- G4: 5 "SampleCollected" items (available to select), within technician 45's
-- granted test types, two of them STAT/Urgent.
DECLARE @reqG4a INT, @reqG4b INT, @reqG4c INT;
INSERT INTO TestRequest (PatientUserId, DoctorUserId, TestRequestStatusId, UrgencyId, RequestedOn) VALUES (37, 18, 5, 3, DATEADD(HOUR,-6,@now)); -- Stat
SET @reqG4a = SCOPE_IDENTITY();
INSERT INTO TestRequest (PatientUserId, DoctorUserId, TestRequestStatusId, UrgencyId, RequestedOn) VALUES (37, 18, 5, 2, DATEADD(HOUR,-10,@now)); -- Urgent
SET @reqG4b = SCOPE_IDENTITY();
INSERT INTO TestRequest (PatientUserId, DoctorUserId, TestRequestStatusId, UrgencyId, RequestedOn) VALUES (37, 18, 5, 1, DATEADD(HOUR,-14,@now)); -- Routine
SET @reqG4c = SCOPE_IDENTITY();

DECLARE @g4 TABLE (n INT IDENTITY(1,1), ReqId INT, TestTypeId INT);
INSERT INTO @g4 (ReqId, TestTypeId) VALUES (@reqG4a,34),(@reqG4a,40),(@reqG4b,42),(@reqG4b,43),(@reqG4c,47);
DECLARE @g4n INT = 1, @g4Req INT, @g4Type INT, @g4Item INT, @g4Sample INT;
WHILE @g4n <= 5
BEGIN
    SELECT @g4Req = ReqId, @g4Type = TestTypeId FROM @g4 WHERE n = @g4n;
    SELECT @g4Sample = SampleTypeId FROM TestType WHERE TestTypeId = @g4Type;
    INSERT INTO TestRequestItem (TestRequestId, TestTypeId, TestRequestItemStatusId) VALUES (@g4Req, @g4Type, 2);
    SET @g4Item = SCOPE_IDENTITY();
    SET @bc = @bc + 1;
    INSERT INTO RequestSample (TestRequestItemId, SampleTypeId, BarcodeLabel, CollectedByUserId, CollectedOn, ReceivedOn)
    VALUES (@g4Item, @g4Sample, 'SEEDB-' + CAST(@bc AS VARCHAR(10)), 9, @now, DATEADD(HOUR,-3,@now));
    SET @g4n = @g4n + 1;
END
PRINT '5 SampleCollected items added within technician 45''s granted test types (1 STAT, 1 Urgent, 3 Routine).';

COMMIT TRANSACTION;
PRINT 'All stages committed successfully.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    PRINT 'ERROR - transaction rolled back, nothing was changed.';
    PRINT ERROR_MESSAGE();
    THROW;
END CATCH

/* ============================================================================
   SUMMARY — re-run the key preload-checklist counts (only reached if the
   transaction above committed successfully)
   ============================================================================ */
PRINT '== SUMMARY ==';
SELECT (SELECT COUNT(*) FROM Users WHERE RoleId=5 AND IsArchived=0) AS TotalActivePatients;
SELECT tr.DoctorUserId, trs.Code AS StatusCode, COUNT(*) AS Cnt
FROM TestRequest tr JOIN TestRequestStatus trs ON trs.TestRequestStatusId = tr.TestRequestStatusId
WHERE tr.DoctorUserId = 17 GROUP BY tr.DoctorUserId, trs.Code ORDER BY trs.Code;
SELECT tri.AssignedTechnicianUserId, tris.Code AS ItemStatus, COUNT(*) AS Cnt
FROM TestRequestItem tri JOIN TestRequestItemStatus tris ON tris.TestRequestItemStatusId = tri.TestRequestItemStatusId
WHERE tri.AssignedTechnicianUserId = 45 OR (tri.AssignedTechnicianUserId IS NULL AND tri.TestTypeId IN (34,40,42,43,47))
GROUP BY tri.AssignedTechnicianUserId, tris.Code ORDER BY tris.Code;
SELECT COUNT(*) AS TestTypesStillUnder2Techs FROM (
    SELECT tt.TestTypeId FROM TestType tt LEFT JOIN TechnicianTestType ttt ON ttt.TestTypeId = tt.TestTypeId
    WHERE tt.IsArchived = 0 GROUP BY tt.TestTypeId HAVING COUNT(DISTINCT ttt.TechnicianUserId) < 2
) x;
PRINT 'Seed script complete.';
