# BMIT2023 Campus Sports Facility Reservation System

ASP.NET Core MVC web system for a TARUMT group assignment (3 students). Built in Visual Studio 2026 Community on Windows. The tutor has approved the title and the team size.

Repo layout: this file lives at the repo root and the project is in `Demo/`. Paths in prompts such as `Models/DB.cs` are relative to `Demo/`. The main branch is `master`.

## How to work (read first)

These rules override your defaults. Follow the prompt exactly and keep the code simple.

- Do only what the prompt asks. No extra features, no "while I'm here" fixes, no unrequested cleanup or renaming.
- Make the smallest change that satisfies the prompt. Prefer editing existing code over adding new files.
- Do not add layers or patterns the course has not taught: no repository pattern, service layer, generic base classes, interfaces for single classes, MediatR, AutoMapper, or extra dependency injection setups, unless the prompt asks for them.
- Do not add or upgrade NuGet or JS packages unless the prompt names the package.
- Copy the style of the course practicals (folder layout, naming, controller and view patterns) instead of inventing new ones.
- Do not create tests, docs, README files or comments beyond a short comment where logic is not obvious, unless asked.
- Write code a student can explain line by line at the demo. If a simpler version exists, use it.
- Stay inside the module the prompt names. Touch shared files (entity classes, database context, `Program.cs`, shared layout, shared helper) only when the prompt says so.
- If the prompt is ambiguous or conflicts with these rules, ask one short question before changing anything. Do not guess.
- When done, reply with a few lines: which files changed and what changed. No long recap.

## Stack and versions

- .NET 10, ASP.NET Core MVC, C#.
- Entity Framework Core 10.x (`Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Tools`), code-first with migrations.
- SQL Server Express, file-based database (`.mdf`).
- jQuery **3.7.1** (not 4.x, it breaks the unobtrusive libraries), plus jQuery Validate, jQuery Validation Unobtrusive and jQuery AJAX Unobtrusive, installed through LibMan from CDNJS in that import order.
- `SixLabors.ImageSharp` **3.2.0** for photo crop and resize. Stay on this version because 4.0.0+ needs a licence. The build may warn "No Six Labors license found": ignore it, build and run in Debug, and do not upgrade.
- `X.PagedList.Mvc.Core` for paging.
- Bootstrap: use whatever version the course template project ships.

## Hard rules from the assignment brief

- No ASP.NET Core Identity. Write cookie authentication by hand, with Name and Role claims.
- Hash passwords with the built-in `PasswordHasher`. Never store or log plain-text passwords.
- Data annotations only. No Fluent API.
- Code-first with migrations. Never edit migration files or the `.mdf` by hand; create a new migration instead. Claude Code does not run migration commands. When a migration is needed, say so, and the student runs `Add-Migration <Name>` then `Update-Database` in Visual Studio's Package Manager Console (Tools > NuGet Package Manager > Package Manager Console).
- Strict MVC. Every model and view model has full validation attributes, checked on both client and server. Range and date rules that attributes cannot express go in the controller action with `ModelState.AddModelError`, as in the practicals.
- Every controller action has the correct `[Authorize]` or `[Authorize(Roles = "...")]`. A logged-out or wrong-role user typing the URL must be blocked.
- The system must be a facility reservation system, not a plain online shop.

## Naming (follow master, do not rename)

- One `User` class (key `Email`, fields `Password`, `Name`, `Role`, `PhotoURL`, login and verification fields). There are no `Admin` or `Member` subclasses and no separate student fields.
- `UserRole` enum: `student`, `public`, `admin` (lowercase). The cookie Role claim is the enum name, so role strings in `[Authorize(Roles = "...")]` are lowercase too: `"admin"`, `"student,public"`.
- `Booking` has `MemberEmail` (foreign key) and the navigation property `User`. Keep the name `MemberEmail`. Use `User`, not `Member`, everywhere else in code.

## Roles

- `admin`: manages users, facilities, time slots and bookings. A `SuperAdmin` role is postponed. Do not add it unless a prompt says so.
- Visitor (not logged in): can browse the facility catalog and facility details, like a shopping site. Booking requires login.
- `student` and `public`: browse, book, pay (students do not pay) and view own booking history.
- Student status comes from the email domain at registration (`StudentEmailDomain` in `AccountController`). There is no apply or approve step. A student email gives role `student`; any other email gives `public`.

## Business rules

- Opening hours are fixed at 08:00 to 20:00. Time slots are one hour each, starting 08:00 to 19:00. An admin creates a slot for a facility on a date and ticks `IsPeak` per slot.
- Each facility has a capacity (courts 1, gym higher). A slot is full when its active bookings reach the capacity.
- Active booking everywhere (availability, capacity, in-use checks): status `Confirmed`, or `Pending` with `HoldExpiresAt` later than now. `Cancelled` never counts.
- Slots in the past cannot be created. A slot that has any booking cannot be edited or deleted.
- Price: the facility's hourly rate, plus its peak surcharge when the slot is peak. The booking stores the price that applied when it was made. Students pay 0.
- Users can book up to 7 days ahead.
- Public bookings stay pending until Stripe payment (test mode) succeeds. A pending booking holds its capacity for 15 minutes. Check this when loading availability. Do not add a background job.
- Cancellation: public users up to 24 hours before the slot. The booking is marked refund due, and an admin refunds outside the system and marks it refunded. Students can cancel any time.
- QR e-ticket: the QR code holds the booking ID. An admin check-in page reports valid, wrong date or already used, and marks the booking checked in.

## Entities

`User`, `Category`, `Facility`, `FacilityPhoto`, `TimeSlot`, `Booking`, `Payment`. Do not add entities or fields unless the prompt says so.

## Modules and owners

Replace "Member 1-3" with real names. "Member" in a module name means student and public users.

| Owner | Core modules | Additional features |
|---|---|---|
| Member 1 | Security; Admin and Member Maintenance; Booking History (Member) | Login blocking after 3 failed attempts; email verification; password reset; captcha |
| Member 2 | Booking and Payment; Booking Maintenance (Admin) | Stripe payment (test mode); e-receipt email; QR code e-ticket with admin check-in page; booking and revenue charts |
| Member 3 | Facility Management and Catalog; Time Slot and Pricing | Interactive slot selection on facility detail page; batch time slot generation; AJAX search, sort and paging on facilities; multiple facility photos with crop and resize |

- Work only in your own module. Other members' code is read-only unless the prompt says otherwise.
- Modules link by URL, not by calling each other's code. The facility detail page (Member 3) links to the booking page (Member 2) with the slot id in the URL: `/Booking/Create?slotId=`.
- Entity classes (`Models/DB.cs`) and migrations are shared. A change needs a pull request all three approve, and everyone must then run `Update-Database`.

## Commands

Run from the solution folder (where the `.sln` is).

- Build: `dotnet build`
- Run: `dotnet run --project Demo`

Migrations are run by the student in the Package Manager Console, not by Claude Code. Do not run a build while the app is being debugged in Visual Studio, because it locks the output files.

Database: `*.mdf` and `*.ldf` are git-ignored. To reset, run `Data/ResetDatabase.sql` (edit the file paths to your machine) or re-attach an empty `DB.mdf`, then `Update-Database`, then run `Data/SampleData.sql` (twice, so Ids start from 1). `SampleData.sql` generates slots only for today and the next 6 days, so run it again when they expire. Sample logins use password `123456`.

## Git

- One branch per member. Merge through a pull request that another member has run.
- Review rotation: Member 1 reviews Member 2, Member 2 reviews Member 3, Member 3 reviews Member 1.
- Before merging master into your branch, run `git fetch origin` first. Merging without fetching uses an out-of-date `origin/master`.
- Build with 0 errors before you push.
- Do not commit `bin/`, `obj/`, secrets, real SMTP passwords or live payment keys. Use placeholders in `appsettings.json` and test keys only.

## Reference docs

Read these only when a prompt needs them. Do not load them otherwise. If a document disagrees with this file, this file wins.

- The final plan is in `Demo/Coding_context/` (it is older than this file on roles and student status).
- Practical slides are in `Demo/docs/practicals/`. They are local only and git-ignored, so never commit them. When a prompt says "follow Practical N", read that PDF first. If it is missing, stop and ask.
- The assignment brief and rubric are not in the repo. Do not assume what they say.
