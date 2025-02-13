using Acceloka.Models;
using Acceloka.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Acceloka.Controllers
{
    [Route("api/v1")]
    [ApiController]
    public class TicketController : ControllerBase
    {
        private readonly TicketService _service;
        private readonly ILogger<TicketController> _logger;

        public TicketController(TicketService service, ILogger<TicketController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet("get-available-ticket")]
        public async Task<IActionResult> GetAvailableTickets(
            [FromQuery] string? categoryName,
            [FromQuery] string? ticketCode,
            [FromQuery] string? ticketName,
            [FromQuery] int? maxPrice,
            [FromQuery] DateTimeOffset? minEventDate,
            [FromQuery] DateTimeOffset? maxEventDate,
            [FromQuery] string orderBy = "TicketCode",
            [FromQuery] string orderState = "asc",
            [FromQuery] int pageNumber = 1,
            [FromQuery] int ticketsPerPage = 10)
        {
            _logger.LogInformation("Fetching available tickets with filters: Category={CategoryName}, TicketCode={TicketCode}, TicketName={TicketName}, MaxPrice={MaxPrice}, MinEventDate={MinEventDate}, MaxEventDate={MaxEventDate}, OrderBy={OrderBy}, OrderState={OrderState}, Page={PageNumber}, PerPage={TicketsPerPage}", categoryName, ticketCode, ticketName, maxPrice, minEventDate, maxEventDate, orderBy, orderState, pageNumber, ticketsPerPage);

            try
            {
                var result = await _service.GetAvailableTickets(categoryName, ticketCode, ticketName, maxPrice, minEventDate, maxEventDate, orderBy, orderState, pageNumber, ticketsPerPage);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching available tickets");
                return StatusCode(500, "Internal Server Error");
            }
        }

        [HttpPost("book-ticket")]
        public async Task<IActionResult> BookTickets([FromBody] BookTicketRequest request)
        {
            _logger.LogInformation("Booking ticket with request: {@Request}", request);

            try
            {
                var result = await _service.BookTickets(request);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error booking ticket");
                return StatusCode(500, "Internal Server Error");
            }
        }

        [HttpGet("get-booked-ticket/{bookedTicketId}")]
        public async Task<IActionResult> GetBookedTicket(int bookedTicketId)
        {
            _logger.LogInformation("Fetching booked ticket with ID={BookedTicketId}", bookedTicketId);

            try
            {
                var result = await _service.GetBookedTicket(bookedTicketId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching booked ticket with ID={BookedTicketId}", bookedTicketId);
                return StatusCode(500, "Internal Server Error");
            }
        }

        [HttpDelete("revoke-ticket/{bookedTicketId}/{ticketCode}/{qty}")]
        public async Task<IActionResult> RevokeTicket(int bookedTicketId, string ticketCode, int qty)
        {
            _logger.LogInformation("Revoking ticket: BookedTicketId={BookedTicketId}, TicketCode={TicketCode}, Quantity={Quantity}", bookedTicketId, ticketCode, qty);

            try
            {
                var result = await _service.RevokeTicket(bookedTicketId, ticketCode, qty);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking ticket: BookedTicketId={BookedTicketId}, TicketCode={TicketCode}, Quantity={Quantity}", bookedTicketId, ticketCode, qty);
                return StatusCode(500, "Internal Server Error");
            }
        }

        [HttpPut("edit-booked-ticket/{bookedTicketId}")]
        public async Task<IActionResult> UpdateBookedTicket(int bookedTicketId, [FromBody] BookTicketRequest request)
        {
            _logger.LogInformation("Updating booked ticket with ID={BookedTicketId} and request: {@Request}", bookedTicketId, request);

            try
            {
                var result = await _service.UpdateBookedTicket(bookedTicketId, request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating booked ticket with ID={BookedTicketId}", bookedTicketId);
                return StatusCode(500, "Internal Server Error");
            }
        }
    }
}
