# BMIT2023 Campus Sports Facility Reservation System

ASP.NET Core MVC web system for a TARUMT group assignment (3 students). Built in Visual Studio 2026 Community on Windows. The tutor has approved the title and the team size.

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
- `SixLabors.ImageSharp` **3.2.0** for photo crop and resize. The course says stay on this version because 4.0.0+ needs a licence. The build may warn "No Six Labors license found": ignore it, build and run in Debug, and do not upgrade.
- `X.PagedList.Mvc.Core` for paging.
- Bootstrap: use whatever version the course template project ships.

## Hard rules from the assignment brief

- No ASP.NET Core Identity. Write cookie authentication by hand, with Name and Role claims.
- Hash passwords with the built-in `PasswordHasher`. Never store or log plain-text passwords.
- Data annotations only. No Fluent API.
- Code-first with migrations. Never edit migration files or the `.mdf` by hand; create a new migration instead. Claude Code does not run migration commands. When a migration is needed, say so, and the student runs `Add-Migration <Name>` then `Update-Database` in Visual Studio's Package Manager Console (Tools > NuGet Package Manager > Package Manager Console).
- Strict MVC. Every model and view model has full validation attributes, checked on both client and server.
- Every controller action has the correct `[Authorize]` or `[Authorize(Roles = "...")]`. A logged-out or wrong-role user typing the URL must be blocked.
- The system must be a facility reservation system, not a plain online shop.

## Roles

- `SuperAdmin`: one seeded account. Only this role creates, edits, blocks or deletes admin accounts. The seeded account cannot be edited or deleted in the UI.
- `Admin`: manages members, facilities, time slots and bookings.
- Visitor (not logged in): can browse the facility catalog and facility details, like a shopping site. Booking requires login.
- `Member`: browses facilities, books, pays and views own booking history. Every member starts as a Public member and can apply for student status from their profile with a student ID. An admin approves or rejects once, and a rejected member can re-apply. Student status is a student ID field and a status field (None, Pending, Approved, Rejected) on Member, not a separate role, subclass or table, unless the prompt says otherwise. Approved students book for free and skip payment.

## Business rules

- Fixed one-hour time slots. Each facility has a capacity (courts 1, gym higher). A slot is full when its active bookings reach the capacity.
- Members can book up to 7 days ahead.
- Price: each facility has an hourly rate, and peak slots add a surcharge. The booking stores the price that applied when it was made.
- Public bookings stay pending until Stripe payment (test mode) succeeds. A pending booking holds its capacity for 15 minutes. Check this when loading availability. Do not add a background job.
- Cancellation: Public members up to 24 hours before the slot. The booking is marked refund due, and an admin refunds outside the system and marks it refunded. Students can cancel any time.
- QR e-ticket: the QR code holds the booking ID. An admin check-in page reports valid, wrong date or already used, and marks the booking checked in.

## Entities

User (parent) with Admin and Member children, Category, Facility, FacilityPhoto, TimeSlot, Booking, Payment. Fields are agreed by all three members in the first session. Do not add entities unless the prompt says so.

## Modules and owners

Replace "Member 1-3" with real names.

| Owner | Core modules | Additional features |
|---|---|---|
| Member 1 | Security; Admin and Member Maintenance; Booking History (Member) | Login blocking after 3 failed attempts; email verification; password reset; captcha |
| Member 2 | Booking and Payment; Booking Maintenance (Admin) | Stripe payment (test mode); e-receipt email; QR code e-ticket with admin check-in page; booking and revenue charts |
| Member 3 | Facility Management and Catalog; Time Slot and Pricing | Interactive slot selection on facility detail page; batch time slot generation; AJAX search, sort and paging on facilities; multiple facility photos with crop and resize |

- Work only in your own module. Other members' code is read-only unless the prompt says otherwise.
- Modules link by URL, not by calling each other's code. The facility detail page (Member 3) links to the booking page (Member 2) with the slot id in the URL, using the route the team agreed (proposed: `/Booking/Create?slotId=`).
- Entity classes and sample data are shared and agreed by all three. After they are frozen, a change needs a pull request all three approve.

## Commands

Run from the solution folder (where the `.sln` is).

- Build: `dotnet build`
- Run: `dotnet run --project <ProjectName>`

Migrations are run by the student in the Package Manager Console, not by Claude Code. Do not run a build while the app is being debugged in Visual Studio, because it locks the output files.

## Git

- One branch per member. Merge through a pull request that another member has run.
- Review rotation: Member 1 reviews Member 2, Member 2 reviews Member 3, Member 3 reviews Member 1.
- Do not commit `bin/`, `obj/`, secrets, real SMTP passwords or live payment keys. Use placeholders in `appsettings.json` and test keys only.

## Reference docs

Read these only when a prompt touches requirements, marking or scope. Do not load them otherwise.

- `docs/assignment-brief.md`
- `docs/marking-rubric.md`
- `docs/module-split.md`