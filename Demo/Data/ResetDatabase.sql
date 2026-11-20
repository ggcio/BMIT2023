-- WARNING: This permanently deletes the entire Demo database and all its data.
-- Run this script from a connection to the master database, not from DB itself.
-- Close the application and any other connections to DB before running it.
-- This schema is based directly on Demo/Models/DB.cs.

USE [master];
GO

IF DB_ID(N'DB') IS NOT NULL
BEGIN
	ALTER DATABASE [DB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
	DROP DATABASE [DB];
END;
GO

CREATE DATABASE [DB]
ON PRIMARY
(
	NAME = N'DB',
	FILENAME = N'C:\Users\User\Documents\Y2S2\Web&Mobile\Assignment\BMIT2023\Demo\DB.mdf'
)
LOG ON
(
	NAME = N'DB_log',
	FILENAME = N'C:\Users\User\Documents\Y2S2\Web&Mobile\Assignment\BMIT2023\Demo\DB_log.ldf'
);
GO

USE [DB];
GO

CREATE TABLE [Categories]
(
	[Id] int IDENTITY(1,1) NOT NULL,
	[Name] nvarchar(100) NOT NULL,
	CONSTRAINT [PK_Categories] PRIMARY KEY ([Id])
);

CREATE TABLE [Users]
(
	[Email] nvarchar(100) NOT NULL,
	[Password] nvarchar(100) NOT NULL,
	[Name] nvarchar(100) NOT NULL,
	[Role] int NOT NULL,
	[PhotoURL] nvarchar(100) NULL,
	[FailedAttempts] int NOT NULL,
	[LockedUntil] datetime2 NULL,
	[IsBlocked] bit NOT NULL,
	[EmailVerified] bit NOT NULL,
	[VerifyToken] nvarchar(100) NULL,
	[VerifyExpiresAt] datetime2 NULL,
	[ResetToken] nvarchar(100) NULL,
	[ResetExpiresAt] datetime2 NULL,
	CONSTRAINT [PK_Users] PRIMARY KEY ([Email])
);

CREATE TABLE [Facilities]
(
	[Id] int IDENTITY(1,1) NOT NULL,
	[Name] nvarchar(100) NOT NULL,
	[Location] nvarchar(100) NOT NULL,
	[Capacity] int NOT NULL,
	[HourlyRate] decimal(6,2) NOT NULL,
	[PeakSurcharge] decimal(6,2) NOT NULL,
	[CategoryId] int NOT NULL,
	CONSTRAINT [PK_Facilities] PRIMARY KEY ([Id]),
	CONSTRAINT [FK_Facilities_Categories_CategoryId]
		FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [FacilityPhotos]
(
	[Id] int IDENTITY(1,1) NOT NULL,
	[FileName] nvarchar(100) NOT NULL,
	[FacilityId] int NOT NULL,
	CONSTRAINT [PK_FacilityPhotos] PRIMARY KEY ([Id]),
	CONSTRAINT [FK_FacilityPhotos_Facilities_FacilityId]
		FOREIGN KEY ([FacilityId]) REFERENCES [Facilities] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [TimeSlots]
(
	[Id] int IDENTITY(1,1) NOT NULL,
	[Date] date NOT NULL,
	[StartTime] time NOT NULL,
	[EndTime] time NOT NULL,
	[IsPeak] bit NOT NULL,
	[FacilityId] int NOT NULL,
	CONSTRAINT [PK_TimeSlots] PRIMARY KEY ([Id]),
	CONSTRAINT [FK_TimeSlots_Facilities_FacilityId]
		FOREIGN KEY ([FacilityId]) REFERENCES [Facilities] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Bookings]
(
	[Id] int IDENTITY(1,1) NOT NULL,
	[Price] decimal(6,2) NOT NULL,
	[Status] nvarchar(10) NOT NULL,
	[HoldExpiresAt] datetime2 NULL,
	[RefundStatus] nvarchar(10) NOT NULL,
	[CheckedInAt] datetime2 NULL,
	[MemberEmail] nvarchar(100) NOT NULL,
	[TimeSlotId] int NOT NULL,
	CONSTRAINT [PK_Bookings] PRIMARY KEY ([Id]),
	CONSTRAINT [FK_Bookings_TimeSlots_TimeSlotId]
		FOREIGN KEY ([TimeSlotId]) REFERENCES [TimeSlots] ([Id]) ON DELETE CASCADE,
	CONSTRAINT [FK_Bookings_Users_MemberEmail]
		FOREIGN KEY ([MemberEmail]) REFERENCES [Users] ([Email]) ON DELETE CASCADE
);

CREATE TABLE [Payments]
(
	[Id] int IDENTITY(1,1) NOT NULL,
	[Amount] decimal(6,2) NOT NULL,
	[StripeReference] nvarchar(200) NOT NULL,
	[Status] nvarchar(10) NOT NULL,
	[PaidAt] datetime2 NULL,
	[BookingId] int NOT NULL,
	CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
	CONSTRAINT [FK_Payments_Bookings_BookingId]
		FOREIGN KEY ([BookingId]) REFERENCES [Bookings] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_Bookings_MemberEmail] ON [Bookings] ([MemberEmail]);
CREATE INDEX [IX_Bookings_TimeSlotId] ON [Bookings] ([TimeSlotId]);
CREATE INDEX [IX_Facilities_CategoryId] ON [Facilities] ([CategoryId]);
CREATE INDEX [IX_FacilityPhotos_FacilityId] ON [FacilityPhotos] ([FacilityId]);
CREATE UNIQUE INDEX [IX_Payments_BookingId] ON [Payments] ([BookingId]);
CREATE INDEX [IX_TimeSlots_FacilityId] ON [TimeSlots] ([FacilityId]);

-- Keep EF Core's migration history aligned with the current schema.
CREATE TABLE [__EFMigrationsHistory]
(
	[MigrationId] nvarchar(150) NOT NULL,
	[ProductVersion] nvarchar(32) NOT NULL,
	CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES
	(N'20261120064317_CreateDB', N'10.0.14'),
	(N'20261120070508_MakeUserPhotoUrlOptional', N'10.0.14');
GO
