using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using Stripe.Checkout;
using System.Net.Mail;

namespace Demo.Controllers;

public class BookingController(DB db, IConfiguration cf, Helper hp) : Controller
{
    // Active bookings = Confirmed + Pending still within the 15-minute hold
    private int ActiveCount(int slotId)
    {
        return db.Bookings.Count(b => b.TimeSlotId == slotId &&
                                      (b.Status == "Confirmed" ||
                                       (b.Status == "Pending" && b.HoldExpiresAt > DateTime.Now)));
    }

    // Return error message, or "" if the slot can be booked
    private string SlotError(TimeSlot? slot)
    {
        var today = DateTime.Today.ToDateOnly();

        if (slot == null)
        {
            return "Time slot not found.";
        }
        else if (slot.Date.ToDateTime(slot.StartTime) <= DateTime.Now)
        {
            return "Time slot has already started.";
        }
        else if (slot.Date < today || slot.Date > today.AddDays(6))
        {
            return "Booking is only allowed up to 7 days ahead.";
        }
        else if (ActiveCount(slot.Id) >= slot.Facility.Capacity)
        {
            return "Time slot is full.";
        }

        return "";
    }

    private TimeSlot? GetSlot(int slotId)
    {
        return db.TimeSlots
                 .Include(s => s.Facility)
                 .FirstOrDefault(s => s.Id == slotId);
    }

    private decimal GetPrice(TimeSlot slot, Member member)
    {
        if (member.StudentStatus == "Approved") return 0;

        return slot.Facility.HourlyRate + (slot.IsPeak ? slot.Facility.PeakSurcharge : 0);
    }

    // Return the booking only if it belongs to the logged-in member
    private Booking? GetBooking(int id)
    {
        return db.Bookings
                 .Include(b => b.TimeSlot)
                 .ThenInclude(s => s.Facility)
                 .Include(b => b.Payment)
                 .FirstOrDefault(b => b.Id == id &&
                                      b.MemberEmail == User.Identity!.Name);
    }

    private SessionService GetSessionService()
    {
        return new SessionService(new Stripe.StripeClient(cf["Stripe:SecretKey"]));
    }

    // QR code image (PNG) that holds the booking id
    private byte[] MakeQr(int id)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(id.ToString(), QRCodeGenerator.ECCLevel.Q);
        using var qr = new PngByteQRCode(data);
        return qr.GetGraphic(10);
    }

    private void SendReceipt(int id)
    {
        var b = db.Bookings
                  .Include(b => b.TimeSlot)
                  .ThenInclude(s => s.Facility)
                  .Include(b => b.Member)
                  .Include(b => b.Payment)
                  .First(b => b.Id == id);
        var member = b.Member;
        var slot = b.TimeSlot;

        var mail = new MailMessage();
        mail.To.Add(new MailAddress(member.Email, member.Name));
        mail.Subject = $"Booking Receipt #{b.Id}";
        mail.IsBodyHtml = true;

        // QR code as an inline attachment, shown by <img src='cid:qr'>
        var att = new Attachment(new MemoryStream(MakeQr(b.Id)), "qr.png", "image/png");
        att.ContentId = "qr";
        att.ContentDisposition!.Inline = true;
        mail.Attachments.Add(att);

        var price = b.Price == 0 ? "Free (Student)" : $"RM {b.Price:F2}";
        var paid = b.Payment?.PaidAt == null ? "" : $"<p>Paid At: {b.Payment.PaidAt:dd MMM yyyy HH:mm}</p>";

        mail.Body = $@"
            <p>Dear {member.Name},</p>
            <p>Your booking is confirmed.</p>
            <p>Booking ID: {b.Id}</p>
            <p>Facility: {slot.Facility.Name}</p>
            <p>Location: {slot.Facility.Location}</p>
            <p>Date: {slot.Date:dd MMM yyyy (ddd)}</p>
            <p>Time: {slot.StartTime:HH:mm} - {slot.EndTime:HH:mm}</p>
            <p>Price: {price}</p>
            {paid}
            <p>Show this QR code at check-in:</p>
            <img src='cid:qr' style='width: 200px; height: 200px'>
            <p>From, Campus Sports Facility Reservation System</p>
        ";

        try
        {
            hp.SendEmail(mail);
        }
        catch
        {
            TempData["Info"] = "Booking confirmed, but the receipt email could not be sent.";
        }
    }

    // GET: Booking/Create?slotId=
    [Authorize(Roles = "Member")]
    public IActionResult Create(int slotId)
    {
        var slot = GetSlot(slotId);
        var error = SlotError(slot);

        if (error != "")
        {
            TempData["Info"] = error;
            return Redirect("/");
        }

        var member = db.Members.Find(User.Identity!.Name)!;

        ViewBag.Slot = slot;
        ViewBag.Price = GetPrice(slot!, member);
        ViewBag.IsStudent = member.StudentStatus == "Approved";

        return View(new BookingVM { SlotId = slotId });
    }

    // POST: Booking/Create
    [Authorize(Roles = "Member")]
    [HttpPost]
    public IActionResult Create(BookingVM vm)
    {
        // Re-check the slot, it may have become full since the page was loaded
        var slot = GetSlot(vm.SlotId);
        var error = ModelState.IsValid ? SlotError(slot) : "Invalid time slot.";

        if (error != "")
        {
            TempData["Info"] = error;
            return Redirect("/");
        }

        var member = db.Members.Find(User.Identity!.Name)!;
        bool isStudent = member.StudentStatus == "Approved";

        var booking = new Booking
        {
            Price = GetPrice(slot!, member),
            Status = isStudent ? "Confirmed" : "Pending",
            HoldExpiresAt = isStudent ? null : DateTime.Now.AddMinutes(15),
            MemberEmail = member.Email,
            TimeSlotId = slot!.Id,
        };
        db.Bookings.Add(booking);
        db.SaveChanges();

        if (isStudent)
        {
            TempData["Info"] = "Booking confirmed.";
            SendReceipt(booking.Id);
            return RedirectToAction("Confirmation", new { booking.Id });
        }

        return RedirectToAction("Pay", new { booking.Id });
    }

    // GET: Booking/Pay/{id}
    [Authorize(Roles = "Member")]
    public IActionResult Pay(int id)
    {
        var booking = GetBooking(id);

        if (booking == null)
        {
            TempData["Info"] = "Booking not found.";
            return Redirect("/");
        }

        if (booking.Status != "Pending")
        {
            TempData["Info"] = "This booking is not waiting for payment.";
            return RedirectToAction("Confirmation", new { id });
        }

        if (booking.HoldExpiresAt <= DateTime.Now)
        {
            booking.Status = "Cancelled";
            db.SaveChanges();

            TempData["Info"] = "Your hold expired. Please book again.";
            return RedirectToAction("Confirmation", new { id });
        }

        var slot = booking.TimeSlot;
        var site = $"{Request.Scheme}://{Request.Host}";

        var options = new SessionCreateOptions
        {
            Mode = "payment",
            ClientReferenceId = booking.Id.ToString(),
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "myr",
                        UnitAmount = (long)(booking.Price * 100),
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"{slot.Facility.Name}, {slot.Date:dd MMM yyyy} {slot.StartTime:HH:mm}",
                        },
                    },
                },
            ],
            // {CHECKOUT_SESSION_ID} is filled in by Stripe
            SuccessUrl = site + "/Booking/Success?session_id={CHECKOUT_SESSION_ID}",
            CancelUrl = site + $"/Booking/Cancel/{booking.Id}",
        };

        var session = GetSessionService().Create(options);

        if (booking.Payment == null)
        {
            booking.Payment = new Payment();
        }
        booking.Payment.Amount = booking.Price;
        booking.Payment.StripeReference = session.Id;
        booking.Payment.Status = "Pending";
        db.SaveChanges();

        return Redirect(session.Url);
    }

    // GET: Booking/Success?session_id=
    [Authorize(Roles = "Member")]
    public IActionResult Success(string session_id)
    {
        Session session;

        try
        {
            session = GetSessionService().Get(session_id);
        }
        catch (Stripe.StripeException)
        {
            TempData["Info"] = "Payment could not be verified.";
            return Redirect("/");
        }

        var booking = GetBooking(int.Parse(session.ClientReferenceId));

        if (booking == null)
        {
            TempData["Info"] = "Booking not found.";
            return Redirect("/");
        }

        // Already handled (e.g. page refreshed)
        if (booking.Status == "Confirmed" || booking.Payment?.Status == "Paid")
        {
            return RedirectToAction("Confirmation", new { booking.Id });
        }

        if (session.PaymentStatus != "paid")
        {
            TempData["Info"] = "Payment not completed.";
            return RedirectToAction("Confirmation", new { booking.Id });
        }

        booking.Payment!.Status = "Paid";
        booking.Payment.PaidAt = DateTime.Now;

        // Paid after the hold expired and someone else took the slot
        if (booking.HoldExpiresAt <= DateTime.Now &&
            ActiveCount(booking.TimeSlotId) >= booking.TimeSlot.Facility.Capacity)
        {
            booking.Status = "Cancelled";
            booking.RefundStatus = "RefundDue";
            TempData["Info"] = "Payment received, but the slot is now full. A refund is due.";
        }
        else
        {
            booking.Status = "Confirmed";
            booking.HoldExpiresAt = null;
            TempData["Info"] = "Payment successful. Booking confirmed.";
        }
        db.SaveChanges();

        if (booking.Status == "Confirmed")
        {
            SendReceipt(booking.Id);
        }

        return RedirectToAction("Confirmation", new { booking.Id });
    }

    // GET: Booking/Cancel/{id} (Stripe's return page when checkout is left)
    [Authorize(Roles = "Member")]
    public IActionResult Cancel(int id)
    {
        var booking = GetBooking(id);

        if (booking == null)
        {
            TempData["Info"] = "Booking not found.";
            return Redirect("/");
        }

        TempData["Info"] = "Payment not completed.";
        return RedirectToAction("Confirmation", new { id });
    }

    // GET: Booking/QrCode/{id}
    [Authorize(Roles = "Member")]
    public IActionResult QrCode(int id)
    {
        var booking = GetBooking(id);

        if (booking == null || booking.Status != "Confirmed")
        {
            return NotFound();
        }

        return File(MakeQr(booking.Id), "image/png");
    }

    // GET: Booking/Confirmation/{id}
    [Authorize(Roles = "Member")]
    public IActionResult Confirmation(int id)
    {
        var booking = GetBooking(id);

        if (booking == null)
        {
            TempData["Info"] = "Booking not found.";
            return Redirect("/");
        }

        return View(booking);
    }
}
