-- Sample data for the Campus Sports Facility Reservation System
-- Re-runnable: clears all tables, reseeds identities, then inserts sample rows.
-- All users have password 123456.

-- 1. Delete all rows (child tables first)
DELETE FROM Payments;
DELETE FROM Bookings;
DELETE FROM TimeSlots;
DELETE FROM FacilityPhotos;
DELETE FROM Facilities;
DELETE FROM Categories;
DELETE FROM Users;

-- 2. Reseed auto-increment tables
DBCC CHECKIDENT ('Payments', RESEED, 0);
DBCC CHECKIDENT ('Bookings', RESEED, 0);
DBCC CHECKIDENT ('TimeSlots', RESEED, 0);
DBCC CHECKIDENT ('FacilityPhotos', RESEED, 0);
DBCC CHECKIDENT ('Facilities', RESEED, 0);
DBCC CHECKIDENT ('Categories', RESEED, 0);

-- 3. Users (Discriminator = Admin or Member)
DECLARE @hash NVARCHAR(100) = 'AQAAAAIAAYagAAAAEGyBGBzs6LQiyNF3QVuXc6Cz0yVIGikGDI6GdsXMFG6eDzkN0617JG54Tz/Zp/V35g==';

INSERT INTO Users (Email, Hash, Name, FailedAttempts, IsBlocked, EmailVerified, Discriminator, IsSuperAdmin, PhotoURL, StudentId, StudentStatus)
VALUES
('sa@gmail.com', @hash, 'Super Admin',    0, 0, 1, 'Admin',  1,    NULL,     NULL,        NULL),
('a1@gmail.com', @hash, 'Jennie Kim',     0, 0, 1, 'Admin',  0,    NULL,     NULL,        NULL),
('a2@gmail.com', @hash, 'Kim Ji-Soo',     0, 0, 1, 'Admin',  0,    NULL,     NULL,        NULL),
('m1@gmail.com', @hash, 'Lalisa Manoban', 0, 0, 1, 'Member', NULL, 'm1.jpg', NULL,        'None'),
('m2@gmail.com', @hash, 'Roseanne Park',  0, 0, 1, 'Member', NULL, 'm2.jpg', '24WMR00001', 'Approved'),
('m3@gmail.com', @hash, 'Tan Wei Ming',   0, 0, 1, 'Member', NULL, 'photo.jpg', '24WMR00002', 'Pending');

-- 4. Categories
DECLARE @badminton INT, @futsal INT, @gym INT;

INSERT INTO Categories (Name) VALUES ('Badminton');
SET @badminton = SCOPE_IDENTITY();
INSERT INTO Categories (Name) VALUES ('Futsal');
SET @futsal = SCOPE_IDENTITY();
INSERT INTO Categories (Name) VALUES ('Gym');
SET @gym = SCOPE_IDENTITY();

-- 5. Facilities (placeholder rates and peak surcharges)
DECLARE @court1 INT, @court2 INT, @futsalCourt INT, @arenaGym INT;

INSERT INTO Facilities (Name, Location, Capacity, HourlyRate, PeakSurcharge, CategoryId)
VALUES ('Badminton Court 1', 'Sports Complex, Level 1', 1, 10.00, 5.00, @badminton);
SET @court1 = SCOPE_IDENTITY();

INSERT INTO Facilities (Name, Location, Capacity, HourlyRate, PeakSurcharge, CategoryId)
VALUES ('Badminton Court 2', 'Sports Complex, Level 1', 1, 10.00, 5.00, @badminton);
SET @court2 = SCOPE_IDENTITY();

INSERT INTO Facilities (Name, Location, Capacity, HourlyRate, PeakSurcharge, CategoryId)
VALUES ('Futsal Court', 'Outdoor Field', 1, 60.00, 20.00, @futsal);
SET @futsalCourt = SCOPE_IDENTITY();

INSERT INTO Facilities (Name, Location, Capacity, HourlyRate, PeakSurcharge, CategoryId)
VALUES ('Arena Gym', 'Sports Complex, Level 2', 30, 5.00, 2.00, @gym);
SET @arenaGym = SCOPE_IDENTITY();

-- 6. Time slots: one-hour slots 08:00-20:00, today plus the next 6 days, every facility
--    Slots starting at 18:00 or later are peak
DECLARE @today DATE = CAST(GETDATE() AS DATE);
DECLARE @day INT = 0, @hour INT;

WHILE @day <= 6
BEGIN
    SET @hour = 8;
    WHILE @hour < 20
    BEGIN
        INSERT INTO TimeSlots (Date, StartTime, EndTime, IsPeak, FacilityId)
        SELECT DATEADD(DAY, @day, @today),
               TIMEFROMPARTS(@hour, 0, 0, 0, 0),
               TIMEFROMPARTS(@hour + 1, 0, 0, 0, 0),
               CASE WHEN @hour >= 18 THEN 1 ELSE 0 END,
               Id
        FROM Facilities;

        SET @hour = @hour + 1;
    END;
    SET @day = @day + 1;
END;

-- 7. Bookings and payments
DECLARE @slot INT, @booking INT;

-- Student confirmed booking (free, no payment): Court 1, tomorrow 10:00
SELECT @slot = Id FROM TimeSlots
WHERE FacilityId = @court1 AND Date = DATEADD(DAY, 1, @today) AND StartTime = '10:00';

INSERT INTO Bookings (Price, Status, HoldExpiresAt, RefundStatus, CheckedInAt, MemberEmail, TimeSlotId)
VALUES (0.00, 'Confirmed', NULL, 'None', NULL, 'm2@gmail.com', @slot);

-- Public confirmed booking with paid payment: Court 1, tomorrow 19:00 (peak, 10 + 5)
SELECT @slot = Id FROM TimeSlots
WHERE FacilityId = @court1 AND Date = DATEADD(DAY, 1, @today) AND StartTime = '19:00';

INSERT INTO Bookings (Price, Status, HoldExpiresAt, RefundStatus, CheckedInAt, MemberEmail, TimeSlotId)
VALUES (15.00, 'Confirmed', NULL, 'None', NULL, 'm1@gmail.com', @slot);
SET @booking = SCOPE_IDENTITY();

INSERT INTO Payments (Amount, StripeReference, Status, PaidAt, BookingId)
VALUES (15.00, 'cs_test_sample_0001', 'Paid', GETDATE(), @booking);

-- Public pending booking, holds the slot for 15 minutes: Futsal Court, tomorrow 18:00 (peak, 60 + 20)
SELECT @slot = Id FROM TimeSlots
WHERE FacilityId = @futsalCourt AND Date = DATEADD(DAY, 1, @today) AND StartTime = '18:00';

INSERT INTO Bookings (Price, Status, HoldExpiresAt, RefundStatus, CheckedInAt, MemberEmail, TimeSlotId)
VALUES (80.00, 'Pending', DATEADD(MINUTE, 15, GETDATE()), 'None', NULL, 'm1@gmail.com', @slot);

-- Public cancelled booking, refund due, with payment: Court 2, in 3 days 09:00
SELECT @slot = Id FROM TimeSlots
WHERE FacilityId = @court2 AND Date = DATEADD(DAY, 3, @today) AND StartTime = '09:00';

INSERT INTO Bookings (Price, Status, HoldExpiresAt, RefundStatus, CheckedInAt, MemberEmail, TimeSlotId)
VALUES (10.00, 'Cancelled', NULL, 'RefundDue', NULL, 'm1@gmail.com', @slot);
SET @booking = SCOPE_IDENTITY();

INSERT INTO Payments (Amount, StripeReference, Status, PaidAt, BookingId)
VALUES (10.00, 'cs_test_sample_0002', 'Paid', DATEADD(DAY, -1, GETDATE()), @booking);
