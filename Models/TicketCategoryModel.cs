using System.ComponentModel.DataAnnotations;

namespace Acceloka.Models
{
    public class TicketCategoryModel
    {
        [Required]
        [MinLength(5)]
        [MaxLength(50)]
        public string CategoryName { get; set; } = string.Empty;
    }
}
