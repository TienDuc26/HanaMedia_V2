namespace HanaMedia.Models;

public class BookingKol
{
    public int BookingId { get; set; }
    public int KolId { get; set; }
    public decimal CastAmount { get; set; }
    public Booking Booking { get; set; } = null!;
    public Kol Kol { get; set; } = null!;
}

public class BookingPayment
{
    public int BookingId { get; set; }
    public string Kind { get; set; } = null!;
    public int PayeeId { get; set; }
    public decimal Amount { get; set; }
    public bool IsPaid { get; set; }
    public int UpdatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Booking Booking { get; set; } = null!;
}
