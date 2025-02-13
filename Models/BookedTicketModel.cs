using System.ComponentModel.DataAnnotations;

namespace Acceloka.Models
{
    public class BookedTicketModel
    {
        public int BookedTicketId { get; set; }

        [Required]
        public int TotalPrice { get; set; }
    }
}
