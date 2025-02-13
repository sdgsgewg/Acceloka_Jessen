using System.ComponentModel.DataAnnotations;

namespace Acceloka.Models
{
    public class TicketModel
    {
        [Required]
        public DateTimeOffset EventDate { get; set; }

        [Required]
        public int Quota { get; set; }

        [Required]
        public string TicketCode { get; set; } = string.Empty;

        [Required]
        [MinLength(5)]
        [MaxLength(50)]
        public string TicketName { get; set; } = string.Empty;

        [Required]
        [Range(0, int.MaxValue, ErrorMessage = "Harga tiket tidak boleh negatif.")]
        public int Price { get; set; }

        public string CategoryName { get; set; } = string.Empty;
    }
}
