namespace Acceloka.Models
{
    public class BookTicketRequest
    {
        public List<TicketBookingModel> Tickets { get; set; } = new List<TicketBookingModel>();
    }
}
