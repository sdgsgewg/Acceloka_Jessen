using System.ComponentModel.DataAnnotations;

namespace Acceloka.Models
{
    public class BookedTicketDetailModel
    {
        public int BookedTicketDetailId { get; set; }

        [Required]
        public int Quantity { get; set; }

        [Required]
        public int SubtotalPrice { get; set; }

        public int BookedTicketId { get; set; }

        public string TicketCode { get; set; } = string.Empty;
    }
}
