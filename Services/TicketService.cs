using Acceloka.Entities;
using Acceloka.Models;
using Azure.Core;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Net;
using System.IO;
using Microsoft.Extensions.Logging;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace Acceloka.Services
{
    public class TicketService
    {
        private readonly AccelokaContext _db;
        private readonly ILogger<TicketService> _logger;

        public TicketService(AccelokaContext db, ILogger<TicketService> logger)
        {
            _db = db;
            _logger = logger;
        }
        
        public async Task<object> GetAvailableTickets(
            string? categoryName,
            string? ticketCode,
            string? ticketName,
            int? maxPrice,
            DateTimeOffset? minEventDate,
            DateTimeOffset? maxEventDate,
            string orderBy = "TicketCode",
            string orderState = "asc",
            int pageNumber = 1,
            int ticketsPerPage = 10)
        {
            _logger.LogInformation("Fetching available tickets with parameters: categoryName={CategoryName}, ticketCode={TicketCode}, ticketName={TicketName}, maxPrice={MaxPrice}, minEventDate={MinEventDate}, maxEventDate={MaxEventDate}, orderBy={OrderBy}, orderState={OrderState}, pageNumber={PageNumber}, ticketsPerPage={TicketsPerPage}",
                categoryName, ticketCode, ticketName, maxPrice, minEventDate, maxEventDate, orderBy, orderState, pageNumber, ticketsPerPage);

            try
            {
                var query = _db.Tickets
                .Include(t => t.TicketCategory)
                .Where(t => t.Quota > 0) // Hanya mengambil tiket dengan quota tersedia
                .AsQueryable();

                // Error Handling
                var totalTickets = await query.CountAsync();
                if (totalTickets == 0)
                {
                    // Mengembalikan pesan error jika query-nya kosong
                    return new
                    {
                        type = "https://example.com/probs/ticket-not-found",
                        title = "Ticket Not Found",
                        status = 404,
                        detail = $"No ticket found based on the provided parameter.",
                        instance = $"/api/v1/get-available-ticket"
                    };
                }

                // Filtering
                //if (!string.IsNullOrEmpty(categoryName))
                //{
                //    query = query.Where(t => t.TicketCategory.CategoryName.Contains(categoryName));
                //}

                //if (!string.IsNullOrEmpty(ticketCode))
                //{
                //    query = query.Where(t => t.TicketCode.Contains(ticketCode));
                //}

                //if (!string.IsNullOrEmpty(ticketName))
                //{
                //    query = query.Where(t => t.TicketName.Contains(ticketName));
                //}

                if (!string.IsNullOrEmpty(categoryName) || !string.IsNullOrEmpty(ticketName) || !string.IsNullOrEmpty(ticketCode))
                {
                    query = query.Where(t =>
                        (!string.IsNullOrEmpty(categoryName) && t.TicketCategory.CategoryName.Contains(categoryName)) ||
                        (!string.IsNullOrEmpty(ticketName) && t.TicketName.Contains(ticketName)) ||
                        (!string.IsNullOrEmpty(ticketCode) && t.TicketCode.Contains(ticketCode))
                    );
                }

                if (maxPrice.HasValue)
                {
                    query = query.Where(t => t.Price <= maxPrice.Value);
                }

                if (minEventDate.HasValue)
                {
                    query = query.Where(t => t.EventDate >= minEventDate.Value);
                }

                if (maxEventDate.HasValue)
                {
                    query = query.Where(t => t.EventDate <= maxEventDate.Value);
                }

                // Order By
                switch (orderBy.ToLower())
                {
                    case "eventdate":
                        query = orderState.ToLower() == "desc" ? query.OrderByDescending(t => t.EventDate) : query.OrderBy(t => t.EventDate);
                        break;
                    case "price":
                        query = orderState.ToLower() == "desc" ? query.OrderByDescending(t => t.Price) : query.OrderBy(t => t.Price);
                        break;
                    case "ticketname":
                        query = orderState.ToLower() == "desc" ? query.OrderByDescending(t => t.TicketName) : query.OrderBy(t => t.TicketName);
                        break;
                    case "categoryname":
                        query = orderState.ToLower() == "desc" ? query.OrderByDescending(t => t.TicketCategory.CategoryName) : query.OrderBy(t => t.TicketCategory.CategoryName);
                        break;
                    default: // Default: Sort by ticket code
                        query = orderState.ToLower() == "desc" ? query.OrderByDescending(t => t.TicketCode) : query.OrderBy(t => t.TicketCode);
                        break;
                }

                // Pagination
                var tickets = await query
                    .Skip((pageNumber - 1) * ticketsPerPage)
                    .Take(ticketsPerPage)
                    .Select(Q => new TicketModel
                    {
                        EventDate = Q.EventDate,
                        Quota = Q.Quota,
                        TicketCode = Q.TicketCode,
                        TicketName = Q.TicketName,
                        CategoryName = Q.TicketCategory.CategoryName,
                        Price = Q.Price
                    }).ToListAsync();

                _logger.LogInformation("Successfully retrieved {TotalTickets} tickets.", tickets.Count);

                return new 
                { 
                    Tickets = tickets, 
                    TotalTickets = totalTickets, 
                    PageNumber = pageNumber, 
                    ItemsPerPage = ticketsPerPage 
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching available tickets.");
                return new 
                { 
                    status = 500, 
                    message = "Internal server error occurred while fetching tickets." 
                };
            }
        }

        public async Task<object> GetAvailableTicketsByCategory()
        {
            _logger.LogInformation("Fetching available tickets by category name.");

            try
            {
                var tickets = await _db.Tickets
                .Include(t => t.TicketCategory)
                .Where(t => t.Quota > 0)
                .ToListAsync();

                // Mengelompokkan setiap tiket berdasarkan kategorinya masing-masing
                var groupedByCategory = tickets
                    .GroupBy(t => t.TicketCategory.CategoryName)
                    .Where(g => g.Any()) // untuk menghinadari kategori kosong
                    .Select(g => new
                    {
                        CategoryName = g.Key,
                        Tickets = g.Select(t => new
                        {
                            EventDate = t.EventDate,
                            Quota = t.Quota,
                            TicketCode = t.TicketCode,
                            TicketName = t.TicketName,
                            CategoryName = t.TicketCategory.CategoryName,
                            Price = t.Price
                        }).ToList()
                    }).ToList();

                _logger.LogInformation("Successfully retrieved tickets by category name.");

                // Mengembalikan jumlah harga keseluruhan tiket dan output yang dikelompokkan berdasarkan nama kategori
                return new
                {
                    TicketsPerCategories = groupedByCategory
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching available tickets.");
                return new
                {
                    status = 500,
                    message = "Internal server error occurred while fetching tickets."
                };
            }
        }


        public async Task<object> GetSingleTicket(string ticketCode)
        {
            _logger.LogInformation("Fetching available tickets with code: {TicketCode}", ticketCode);

            try
            {
                var query = _db.Tickets
                .Where(t => t.TicketCode == ticketCode)
                .AsQueryable();

                // Error Handling
                var totalTickets = await query.CountAsync();
                if (totalTickets == 0)
                {
                    // Mengembalikan pesan error jika query-nya kosong
                    return new
                    {
                        type = "https://example.com/probs/ticket-not-found",
                        title = "Ticket Not Found",
                        status = 404,
                        detail = $"No ticket found based on the provided ticket code.",
                        instance = $"/api/v1/get-single-ticket"
                    };
                }

                // Pagination
                var ticket = await query
                    .Select(Q => new TicketModel
                    {
                        EventDate = Q.EventDate,
                        Quota = Q.Quota,
                        TicketCode = Q.TicketCode,
                        TicketName = Q.TicketName,
                        CategoryName = Q.TicketCategory.CategoryName,
                        Price = Q.Price
                    }).FirstOrDefaultAsync();

                _logger.LogInformation("Successfully retrieved ticket with code: {TicketCode}.", ticketCode);

                return new
                {
                    Ticket = ticket,
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching available tickets.");
                return new
                {
                    status = 500,
                    message = "Internal server error occurred while fetching tickets."
                };
            }
        }

        public async Task<object> BookTickets(BookTicketRequest request)
        {
            _logger.LogInformation("Received booking request for {TicketCount} tickets.", request.Tickets.Count);

            var today = DateTimeOffset.UtcNow;

            var ticketCodes = request.Tickets.Select(t => t.TicketCode.ToUpper()).ToList();
            var tickets = await _db.Tickets
                .Include(t => t.TicketCategory)
                .Where(t => ticketCodes.Contains(t.TicketCode.ToUpper()))
                .ToListAsync();

            // Error Handling
            var errors = new List<string>();

            foreach (var booking in request.Tickets)
            {
                var ticket = tickets.FirstOrDefault(t => t.TicketCode.ToUpper() == booking.TicketCode.ToUpper());

                if (ticket == null)
                {
                    _logger.LogWarning("Ticket code '{TicketCode}' not found.", booking.TicketCode);
                    errors.Add($"Ticket Code '{booking.TicketCode}' is not registered.");
                    continue;
                }

                if (ticket.Quota <= 0)
                {
                    _logger.LogWarning("Ticket code '{TicketCode}' is out of quota.", booking.TicketCode);
                    errors.Add($"Ticket Code '{booking.TicketCode}' is out of quota.");
                    continue;
                }

                if (booking.Quantity == 0)
                {
                    _logger.LogWarning("Booking quantity must be greater than zero.");
                    errors.Add("Booking quantity must be greater than zero");
                    continue;
                }

                if (booking.Quantity > ticket.Quota)
                {
                    _logger.LogWarning("Booking quantity ({Quantity}) exceeds available quota ({Quota}) for ticket '{TicketCode}'.", booking.Quantity, ticket.Quota, booking.TicketCode);
                    errors.Add($"The quantity of ticket with code '{booking.TicketCode}' exceed the remaining quota.");
                    continue;
                }

                if (ticket.EventDate <= today)
                {
                    _logger.LogWarning("Ticket '{TicketCode}' cannot be booked as the event date has passed.", booking.TicketCode);
                    errors.Add($"Ticket code '{booking.TicketCode}' cannot be booked as the event date has passed.");
                    continue;
                }
            }

            if (errors.Any())
            {
                _logger.LogError("Booking validation failed: {ErrorCount} errors found.", errors.Count);
                return new
                {
                    type = "https://example.com/probs/booked-ticket-not-found",
                    Title = "One or more validation errors occurred.",
                    Status = (int)HttpStatusCode.BadRequest,
                    Errors = errors
                };
            }

            // Buat record untuk tabel 'BookedTicket'
            var bookedTicket = new Acceloka.Entities.BookedTicket
            {
                TotalPrice = 0 // Akan dihitung di bawah
            };

            await _db.BookedTickets.AddAsync(bookedTicket);
            await _db.SaveChangesAsync();

            foreach (var booking in request.Tickets)
            {
                var ticket = tickets.First(t => t.TicketCode.ToUpper() == booking.TicketCode.ToUpper());
                var subtotal = booking.Quantity * ticket.Price;

                // Buat record untuk detail setiap booking
                var bookedTicketDetail = new Acceloka.Entities.BookedTicketDetail
                {
                    TicketCode = ticket.TicketCode,
                    Quantity = booking.Quantity,
                    SubtotalPrice = subtotal,
                    BookedTicketId = bookedTicket.BookedTicketId
                };

                // Tambahkan data ke tabel Booking Detail
                await _db.BookedTicketDetails.AddAsync(bookedTicketDetail);

                // Kurangi quota tiket dengan qty tiket yang dipesan
                ticket.Quota -= booking.Quantity;

                // Hitung total price dari suatu booking
                bookedTicket.TotalPrice += subtotal;
            }

            await _db.SaveChangesAsync();
            _logger.LogInformation("Booking completed successfully. Total Price: {TotalPrice}", bookedTicket.TotalPrice);

            // Mengelompokkan setiap tiket berdasarkan kategorinya masing-masing
            var groupedByCategory = tickets
                .GroupBy(t => t.TicketCategory.CategoryName)
                .Where(g => g.Any()) // untuk menghinadari kategori kosong
                .Select(g => new
                {
                    CategoryName = g.Key,
                    SummaryPrice = g.Sum(t => t.Price * (request.Tickets.FirstOrDefault(r => r.TicketCode == t.TicketCode)?.Quantity ?? 0)),
                    Tickets = g.Select(t => new
                    {
                        TicketCode = t.TicketCode,
                        TicketName = t.TicketName,
                        Price = t.Price
                    }).ToList()
                }).ToList();

            var totalPrice = groupedByCategory.Sum(c => c.SummaryPrice);
            _logger.LogInformation("Returning booking response with total price {TotalPrice}", totalPrice);

            // Mengembalikan jumlah harga keseluruhan tiket dan output yang dikelompokkan berdasarkan nama kategori
            return new
            {
                PriceSummary = totalPrice,
                TicketsPerCategories = groupedByCategory
            };
        }

        public async Task<object> GetAllBookedTickets(
            int pageNumber = 1,
            int bookingsPerPage = 10)
        {
            _logger.LogInformation("Fetching all booked tickets");

            try
            {
                var allBookedTickets = await _db.BookedTickets.ToListAsync();

                // Pagination
                var bookedTickets = await _db.BookedTickets
                    .OrderBy(bt => bt.BookedTicketId)
                    .Skip((pageNumber - 1) * bookingsPerPage)
                    .Take(bookingsPerPage)
                    .ToListAsync();

                // Error Handling
                var totalBookings = allBookedTickets.Count();
                if (totalBookings == 0)
                {
                    _logger.LogWarning("Booked tickets not found.");
                    return new
                    {
                        type = "https://example.com/probs/booked-tickets-not-found",
                        title = "Booked Ticket Not Found",
                        status = 404,
                        detail = "No booked tickets can be found.",
                        instance = "/api/v1/get-available-booked-tickets"
                    };
                }

                _logger.LogInformation("All booked tickets found. Fetching details...");

                var bookedTicketDetails = await _db.BookedTicketDetails
                    .Include(t => t.TicketCodeNavigation) // Include Tabel Ticket
                    .ThenInclude(tc => tc.TicketCategory) // Include Tabel TicketCategory
                    .Where(detail => bookedTickets
                        .Select(b => b.BookedTicketId).Contains(detail.BookedTicketId)
                        )
                    .ToListAsync();

                var bookings = bookedTickets
                    .Select(ticket => new
                    {
                        BookedTicketId = ticket.BookedTicketId,
                        TotalPrice = ticket.TotalPrice,
                        BookingDate = ticket.CreatedAt,
                        Details = bookedTicketDetails
                            .Where(detail => detail.BookedTicketId == ticket.BookedTicketId)
                            .GroupBy(detail => detail.TicketCodeNavigation?.TicketCategory.CategoryName)
                            .Select(g => new
                            {
                                CategoryName = g.Key,
                                QtyPerCategory = g.Sum(d => d.Quantity),
                                Tickets = g.Select(d => new
                                {
                                    TicketCode = d.TicketCode,
                                    TicketName = d.TicketCodeNavigation?.TicketName,
                                    EventDate = d.TicketCodeNavigation?.EventDate,
                                    Quantity = d.Quantity,
                                }).ToList()
                            }).ToList()
                    });

                _logger.LogInformation("Successfully fetched booked tickets with details.");

                return new
                {
                    Bookings = bookings,
                    TotalBookings = totalBookings,
                    PageNumber = pageNumber,
                    ItemsPerPage = bookingsPerPage
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching booked tickets.");
                return new
                {
                    status = 500,
                    message = "Internal server error occurred while fetching booked tickets."
                };
            }

        }

        public async Task<object> GetBookedTicket(int bookedTicketId)
        {
            _logger.LogInformation("Fetching booked ticket with ID: {BookedTicketId}", bookedTicketId);

            var bookedTicket = await _db.BookedTickets
                .Where(bt => bt.BookedTicketId == bookedTicketId)
                .FirstOrDefaultAsync();

            if (bookedTicket == null)
            {
                // Mengembalikan pesan error jika id booking tidak ditemukan
                _logger.LogWarning("Booked ticket with ID {BookedTicketId} not found.", bookedTicketId);
                return new
                {
                    type = "https://example.com/probs/booked-ticket-not-found",
                    title = "Booked Ticket Not Found",
                    status = 404,
                    detail = $"BookedTicketId {bookedTicketId} is not registered.",
                    instance = $"/api/v1/get-booked-ticket/{bookedTicketId}"
                };
            }

            _logger.LogInformation("Booked ticket {BookedTicketId} found. Fetching details...", bookedTicketId);

            // Ambil data untuk detail dari satu pemesanan (booking)
            var bookedTicketDetails = await _db.BookedTicketDetails
                .Include(t => t.TicketCodeNavigation)
                .ThenInclude(tc => tc.TicketCategory) 
                .Where(detail => detail.BookedTicketId == bookedTicketId)
                .ToListAsync();

            _logger.LogInformation("Fetched {Count} booked ticket details for BookedTicketId {BookedTicketId}", bookedTicketDetails.Count, bookedTicketId);

            // Mengelompokkan hasil query berdasarkan nama kategori
            var groupedByCategory = bookedTicketDetails
                .GroupBy(t => t.TicketCodeNavigation?.TicketCategory.CategoryName)
                .Select(g => new
                {
                    QtyPerCategory = g.Sum(t => t.Quantity),
                    CategoryName = g.Key,
                    Tickets = g.Select(t => new
                    {
                        TicketCode = t.TicketCode,
                        TicketName = t.TicketCodeNavigation?.TicketName,
                        EventDate = t.TicketCodeNavigation?.EventDate,
                        Quantity = t.Quantity,
                    }).ToList()
                });

            _logger.LogInformation("Successfully grouped booked ticket details by category name.");

            return groupedByCategory;
        }

        public async Task<object> RevokeTicket(int bookedTicketId, string ticketCode, int quantity)
        {
            _logger.LogInformation($"Starting revoke ticket process for BookedTicketId: {bookedTicketId}, TicketCode: {ticketCode}, Quantity: {quantity}");


            var bookedTicket = await _db.BookedTickets
                .Where(bt => bt.BookedTicketId == bookedTicketId)
                .FirstOrDefaultAsync();

            if (bookedTicket == null)
            {
                // Mengembalikan pesan error jika id booking tidak ditemukan
                _logger.LogWarning($"Booked Ticket with Id {bookedTicketId} not found.");
                return new
                {
                    type = "https://example.com/probs/booked-ticket-not-found",
                    title = "Booked Ticket Not Found",
                    status = 404,
                    detail = $"BookedTicketId {bookedTicketId} is not registered.",
                    instance = $"/api/v1/revoke-ticket/{bookedTicketId}/{ticketCode}/{quantity}"
                };
            }

            // Ambil satu detail dari suatu pemesanan (booking) yang mau dihapus qty-nya
            var bookedTicketDetail = await _db.BookedTicketDetails
                .Include(t => t.TicketCodeNavigation)
                .ThenInclude(tc => tc.TicketCategory)
                .Where(d => d.BookedTicketId == bookedTicketId)
                .Where(d => d.TicketCode == ticketCode)
                .FirstOrDefaultAsync();

            // ERROR HANDLING
            if (bookedTicketDetail == null)
            {
                // Mengembalikan pesan error jika kode tiket tidak terdaftar
                _logger.LogWarning($"TicketCode {ticketCode} is not found on BookedTicketId {bookedTicketId}.");
                return new
                {
                    type = "https://example.com/probs/ticket-code-not-found",
                    title = "Ticket Code Not Found",
                    status = 404,
                    detail = $"TicketCode {ticketCode} is not registered.",
                    instance = $"/api/v1/revoke-ticket/{bookedTicketId}/{ticketCode}/{quantity}"
                };
            }

            if (quantity > bookedTicketDetail.Quantity)
            {
                // Mengembalikan pesan error quantity > qty tiket yang telah dipesan
                _logger.LogError($"The amout of tickets requested ({quantity}) exceed the amount of booked ticket ({bookedTicketDetail.Quantity}).");
                return new
                {
                    type = "https://example.com/probs/quantity-exceeds-booked",
                    title = "Quantity Exceeds Booked Tickets",
                    status = 400,
                    detail = $"The amout of tickets requested ({quantity}) exceed the amount of booked ticket ({bookedTicketDetail.Quantity}).",
                    instance = $"/api/v1/revoke-ticket/{bookedTicketId}/{ticketCode}/{quantity}"
                };
            }

            // Update kolom qty pada tiket yang di revoke dan update quota dari instance Ticket
            var ticket = await _db.Tickets
                .Where(t => t.TicketCode == bookedTicketDetail.TicketCode)
                .FirstOrDefaultAsync();

            if (bookedTicketDetail.Quantity - quantity == 0)
            {
                _logger.LogInformation($"Remove ticket {ticketCode} from BookedTicketId {bookedTicketId} because its quantity become 0.");

                if (ticket != null)
                {
                    _logger.LogInformation("Added the quota of ticket with code {TicketCode} by {Quantity}.", ticket.TicketCode, quantity);
                    ticket.Quota += quantity;
                    await _db.SaveChangesAsync();
                }
                else
                {
                    _logger.LogWarning("Ticket with code {TicketCode} not found.", bookedTicketDetail.TicketCode);
                }

                _db.BookedTicketDetails.Remove(bookedTicketDetail);
            }
            else
            {
                if (ticket != null)
                {
                    _logger.LogInformation("Added the quota of ticket with code {TicketCode} by {Quantity}.", ticket.TicketCode, quantity);
                    ticket.Quota += quantity;
                    await _db.SaveChangesAsync();
                }
                else
                {
                    _logger.LogWarning("Ticket with code {TicketCode} not found.", bookedTicketDetail.TicketCode);
                }

                bookedTicketDetail.Quantity -= quantity;
                _logger.LogInformation($"Decreasing the quantity of ticket {ticketCode} in BookedTicketId {bookedTicketId}. Remaining quantity: {bookedTicketDetail.Quantity}");
            }
            await _db.SaveChangesAsync();

            // Hapus semua row booking dan booking detail jika semua quantity tiket yang dipesan menjadi 0
            // Ambil data untuk detail dari satu pemesanan (booking)
            var bookedTicketDetails = await _db.BookedTicketDetails
                .Include(t => t.TicketCodeNavigation)
                .ThenInclude(tc => tc.TicketCategory)
                .Where(detail => detail.BookedTicketId == bookedTicketId)
                .ToListAsync();

            if (!bookedTicketDetails.Any())
            {
                _logger.LogInformation($"Remove BookedTicketId {bookedTicketId} because all tickets have been revoked.");
                _db.BookedTickets.Remove(bookedTicket);
                await _db.SaveChangesAsync();
            }

            // Mengembalikan setiap detail dari suatu booking
            var details = bookedTicketDetails
                .Select(Q => new
                {
                    TicketCode = Q.TicketCode,
                    TicketName = Q.TicketCodeNavigation.TicketName,
                    Quantity = Q.Quantity,
                    CategoryName = Q.TicketCodeNavigation.TicketCategory.CategoryName
                });

            _logger.LogInformation($"Revoke tiket finished for BookedTicketId {bookedTicketId}. Remaining tickets: {details.Count()}");

            return details;
        }

        public async Task<object> UpdateBookedTicket(int bookedTicketId, BookTicketRequest request)
        {
            _logger.LogInformation($"Starting ticket update process for BookedTicketId: {bookedTicketId}");

            var bookedTicket = await _db.BookedTickets
                .Where(bt => bt.BookedTicketId == bookedTicketId)
                .FirstOrDefaultAsync();

            if (bookedTicket == null)
            {
                _logger.LogWarning($"BookedTicketId {bookedTicketId} not found.");
                return new
                {
                    type = "https://example.com/probs/booked-ticket-not-found",
                    title = "Booked Ticket Not Found",
                    status = 404,
                    detail = $"BookedTicketId {bookedTicketId} is not registered.",
                    instance = $"/api/v1/edit-booked-ticket/{bookedTicketId}"
                };
            }

            var today = DateTimeOffset.UtcNow;

            var ticketCodes = request.Tickets.Select(t => t.TicketCode.ToUpper()).ToList();
            var bookedTicketDetails = await _db.BookedTicketDetails
                .Include(t => t.TicketCodeNavigation)
                .ThenInclude(tc => tc.TicketCategory)
                .Where(btd => btd.BookedTicketId == bookedTicketId)
                .Where(btd => ticketCodes.Contains(btd.TicketCode.ToUpper()))
                .ToListAsync();

            var errors = new List<string>();

            foreach (var booking in request.Tickets)
            {
                var bookedTicketDetail = bookedTicketDetails.FirstOrDefault(btd => btd.TicketCode.ToUpper() == booking.TicketCode.ToUpper());

                if (bookedTicketDetail == null)
                {
                    _logger.LogWarning($"Ticket code '{booking.TicketCode}' not found in the booked tickets.");
                    errors.Add($"Ticket code '{booking.TicketCode}' is not registered.");
                    continue;
                }

                if (booking.Quantity > bookedTicketDetail.TicketCodeNavigation.Quota)
                {
                    _logger.LogError($"Requested quantity for ticket '{booking.TicketCode}' exceeds the available quota.");
                    errors.Add($"Quantity for ticket code '{booking.TicketCode}' exceeds the available quota.");
                    continue;
                }

                if (booking.Quantity < 1)
                {
                    _logger.LogError($"Quantity for ticket '{booking.TicketCode}' must be at least 1.");
                    errors.Add($"Quantity for ticket code '{booking.TicketCode}' must be at least 1.");
                    continue;
                }
            }

            if (errors.Any())
            {
                _logger.LogWarning($"Validation errors occurred while updating BookedTicketId {bookedTicketId}.");
                return new
                {
                    type = "https://example.com/probs/validation-error",
                    title = "One or more validation errors occurred.",
                    status = (int)HttpStatusCode.BadRequest,
                    errors = errors
                };
            }

            foreach (var booking in request.Tickets)
            {
                var bookedTicketDetail = bookedTicketDetails.FirstOrDefault(btd => btd.TicketCode.ToUpper() == booking.TicketCode.ToUpper());

                if (bookedTicketDetail == null)
                {
                    continue;
                }
                else
                {
                    // Update kolom qty pada tiket yang di revoke dan update quota dari instance Ticket
                    var ticket = await _db.Tickets
                        .Where(t => t.TicketCode == booking.TicketCode)
                        .FirstOrDefaultAsync();

                    if (ticket != null)
                    {
                        var qtyDiff = booking.Quantity - bookedTicketDetail.Quantity;

                        if (qtyDiff >= 0) 
                        {
                            _logger.LogInformation("Minus the quota of ticket with code {TicketCode} by {QtyDiff}.", ticket.TicketCode, qtyDiff);
                            ticket.Quota -= qtyDiff;
                        } 
                        else
                        {
                            _logger.LogInformation("Add the quota of ticket with code {TicketCode} by {QtyDiff}.", ticket.TicketCode, qtyDiff);
                            ticket.Quota += qtyDiff;
                        }

                        await _db.SaveChangesAsync();
                    }
                    else
                    {
                        _logger.LogWarning("Ticket with code {TicketCode} not found.", bookedTicketDetail.TicketCode);
                    }

                    _logger.LogInformation($"Updating quantity for ticket code '{booking.TicketCode}' in BookedTicketId {bookedTicketId} to {booking.Quantity}.");
                    bookedTicketDetail.Quantity = booking.Quantity;
                    await _db.SaveChangesAsync();
                }
            }

            var details = bookedTicketDetails
                .Select(Q => new
                {
                    TicketCode = Q.TicketCode,
                    TicketName = Q.TicketCodeNavigation.TicketName,
                    Quantity = Q.Quantity,
                    CategoryName = Q.TicketCodeNavigation.TicketCategory.CategoryName
                });

            _logger.LogInformation($"Ticket update process completed for BookedTicketId {bookedTicketId}.");

            return details;
        }

    }
}
