using Microsoft.EntityFrameworkCore;
using SitaCptTicketApp.Data;
using SitaCptTicketApp.Models;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Globalization;

namespace SitaCptTicketApp.Services
{
    public class IncidentParserService
    {
        private readonly SitaCptTicketAppContext _context;

        public IncidentParserService(SitaCptTicketAppContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            Console.WriteLine($"IncidentParserService: Initialized at {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
        }

        public async Task<(Ticket?, string?)> ParseIncidentText(string incidentText)
        {
            try
            {
                Console.WriteLine($"ParseIncidentText: Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}; TextLength={incidentText?.Length ?? 0}");
                if (string.IsNullOrWhiteSpace(incidentText))
                {
                    Console.WriteLine("ParseIncidentText: IncidentText is empty or whitespace");
                    return (null, "Incident text cannot be empty.");
                }

                var ticket = new Ticket();
                var lines = incidentText.Split(new[] { "\n", "\r\n" }, StringSplitOptions.None);

                // Regex patterns
                var incidentNumberPattern = new Regex(@"^Incident\s*-\s*(\S+)", RegexOptions.IgnoreCase);
                var priorityPattern = new Regex(@"^Priority\s*:\s*(.+)", RegexOptions.IgnoreCase);
                var openTimePattern = new Regex(@"^Open\s+time\s*:\s*(\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2})(\s+GMT)?\s*", RegexOptions.IgnoreCase);
                var closeTimePattern = new Regex(@"^Close\s*time\s*:\s*(\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}(:\d{2})?)", RegexOptions.IgnoreCase);
                var productPattern = new Regex(@"^Product\s*:\s*(.+)", RegexOptions.IgnoreCase);
                var modulePattern = new Regex(@"^Module\s*:\s*(.+)", RegexOptions.IgnoreCase);
                var affectedEndUserPattern = new Regex(@"^Affected\s+end\s+user\s*:\s*(.+)", RegexOptions.IgnoreCase);
                var callerNamePattern = new Regex(@"^Caller\s+Name\s*:\s*(.+)", RegexOptions.IgnoreCase);
                var callerPhonePattern = new Regex(@"^(?:Caller\s+Phone\s*(?:Number\s*)?|Contact\s+Details\s*\(extension\s+number\))\s*:\s*(.+)", RegexOptions.IgnoreCase);
                var shortDescriptionPattern = new Regex(@"^Short\s+description\s*:\s*(.+?)(?:\s*$|\n)", RegexOptions.IgnoreCase);
                var locationPattern = new Regex(@"^(?:Location|CI|Position\s*/\s*Location|Workstation\s*ID\s*/\s*location)\s*:\s*(\S+)", RegexOptions.IgnoreCase);
                var issueDescriptionPattern = new Regex(@"^Issue\s+Description\s*:\s*(.+)", RegexOptions.IgnoreCase);

                string? currentIssueDescription = null;
                string? locationFromLocation = null;
                string? locationFromCI = null;

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    var rawLine = line.Replace("\t", "\\t").Replace("\r", "\\r").Replace("\n", "\\n");
                    Console.WriteLine($"ParseIncidentText: Processing line: Raw='{rawLine}'; Trimmed='{trimmedLine}'");

                    if (incidentNumberPattern.IsMatch(trimmedLine))
                    {
                        ticket.IncidentNumber = incidentNumberPattern.Match(trimmedLine).Groups[1].Value.Trim();
                        Console.WriteLine($"ParseIncidentText: Found IncidentNumber={ticket.IncidentNumber}");
                    }
                    else if (priorityPattern.IsMatch(trimmedLine))
                    {
                        ticket.Priority = priorityPattern.Match(trimmedLine).Groups[1].Value.Trim();
                        Console.WriteLine($"ParseIncidentText: Found Priority={ticket.Priority}");
                    }
                    else if (openTimePattern.IsMatch(trimmedLine))
                    {
                        var openTimeStr = openTimePattern.Match(trimmedLine).Groups[1].Value.Trim();
                        Console.WriteLine($"ParseIncidentText: Captured openTimeStr={openTimeStr}");
                        if (!DateTime.TryParseExact(
                            openTimeStr,
                            "yyyy-MM-dd HH:mm:ss",
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.None,
                            out var openTime))
                        {
                            Console.WriteLine($"ParseIncidentText: Failed to parse OpenTime: {openTimeStr}");
                            ticket.OpenTime = DateTime.UtcNow;
                            Console.WriteLine($"ParseIncidentText: Set default OpenTime={ticket.OpenTime:yyyy-MM-dd HH:mm:ss}");
                        }
                        else
                        {
                            ticket.OpenTime = openTime;
                            Console.WriteLine($"ParseIncidentText: Parsed OpenTime={ticket.OpenTime:yyyy-MM-dd HH:mm:ss}");
                        }
                    }
                    else if (closeTimePattern.IsMatch(trimmedLine))
                    {
                        var closeTimeStr = closeTimePattern.Match(trimmedLine).Groups[1].Value.Trim();
                        if (!string.IsNullOrEmpty(closeTimeStr) && DateTime.TryParse(closeTimeStr, out var closeTime))
                        {
                            ticket.CloseTime = closeTime;
                            Console.WriteLine($"ParseIncidentText: Found CloseTime={ticket.CloseTime:yyyy-MM-dd HH:mm:ss}");
                        }
                    }
                    else if (productPattern.IsMatch(trimmedLine))
                    {
                        ticket.Product = productPattern.Match(trimmedLine).Groups[1].Value.Trim();
                        Console.WriteLine($"ParseIncidentText: Found Product={ticket.Product}");
                    }
                    else if (modulePattern.IsMatch(trimmedLine))
                    {
                        ticket.Module = modulePattern.Match(trimmedLine).Groups[1].Value.Trim();
                        Console.WriteLine($"ParseIncidentText: Found Module={ticket.Module}");
                    }
                    else if (affectedEndUserPattern.IsMatch(trimmedLine))
                    {
                        ticket.AffectedEndUser = affectedEndUserPattern.Match(trimmedLine).Groups[1].Value.Trim();
                        Console.WriteLine($"ParseIncidentText: Found AffectedEndUser={ticket.AffectedEndUser}");
                    }
                    else if (callerNamePattern.IsMatch(trimmedLine))
                    {
                        ticket.CallerName = callerNamePattern.Match(trimmedLine).Groups[1].Value.Trim();
                        Console.WriteLine($"ParseIncidentText: Found CallerName={ticket.CallerName}");
                    }
                    else if (callerPhonePattern.IsMatch(trimmedLine))
                    {
                        ticket.CallerPhone = callerPhonePattern.Match(trimmedLine).Groups[1].Value.Trim();
                        Console.WriteLine($"ParseIncidentText: Found CallerPhone={ticket.CallerPhone}");
                    }
                    else if (shortDescriptionPattern.IsMatch(trimmedLine))
                    {
                        var match = shortDescriptionPattern.Match(trimmedLine);
                        ticket.ShortDescription = match.Groups[1].Value.Trim();
                        Console.WriteLine($"ParseIncidentText: ShortDescription match success={match.Success}; Captured='{ticket.ShortDescription}'");
                    }
                    else if (locationPattern.IsMatch(trimmedLine))
                    {
                        var match = locationPattern.Match(trimmedLine);
                        var locationValue = match.Groups[1].Value.Trim();
                        if (trimmedLine.StartsWith("Location", StringComparison.OrdinalIgnoreCase) ||
                            trimmedLine.StartsWith("Workstation ID/ location", StringComparison.OrdinalIgnoreCase))
                        {
                            locationFromLocation = locationValue;
                            Console.WriteLine($"ParseIncidentText: Found Location/Workstation={locationValue}");
                        }
                        else if (trimmedLine.StartsWith("CI", StringComparison.OrdinalIgnoreCase))
                        {
                            locationFromCI = locationValue;
                            Console.WriteLine($"ParseIncidentText: Found CI={locationValue}");
                        }
                    }
                    else if (issueDescriptionPattern.IsMatch(trimmedLine))
                    {
                        var newDescription = issueDescriptionPattern.Match(trimmedLine).Groups[1].Value.Trim();
                        if (!string.IsNullOrEmpty(newDescription) && currentIssueDescription != newDescription)
                        {
                            currentIssueDescription = newDescription;
                            ticket.IssueDescription = newDescription;
                            Console.WriteLine($"ParseIncidentText: Found IssueDescription={ticket.IssueDescription}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"ParseIncidentText: No match for line: '{trimmedLine}'");
                    }
                }

                // Set PositionLocation: prefer Location or Workstation ID/ location over CI
                ticket.PositionLocation = locationFromLocation ?? locationFromCI;
                if (string.IsNullOrEmpty(ticket.PositionLocation))
                {
                    Console.WriteLine("ParseIncidentText: PositionLocation not found in incident text");
                }
                else
                {
                    Console.WriteLine($"ParseIncidentText: Set PositionLocation={ticket.PositionLocation}");
                }

                // Set defaults for required fields
                if (string.IsNullOrEmpty(ticket.CallerName))
                {
                    ticket.CallerName = "N/A";
                    Console.WriteLine($"ParseIncidentText: Set default CallerName={ticket.CallerName}");
                }

                // Only compute CallerPhone from PositionLocation if CallerPhone is not explicitly provided
                if (string.IsNullOrEmpty(ticket.CallerPhone) && !string.IsNullOrEmpty(ticket.PositionLocation))
                {
                    var lastThreeChars = ticket.PositionLocation.Length >= 3 ? ticket.PositionLocation[^3..] : ticket.PositionLocation.PadLeft(3, '0');
                    if (int.TryParse(lastThreeChars, out int digits))
                    {
                        ticket.CallerPhone = $"10{digits:D3}";
                        Console.WriteLine($"ParseIncidentText: Computed CallerPhone={ticket.CallerPhone} from PositionLocation={ticket.PositionLocation}");
                    }
                    else
                    {
                        ticket.CallerPhone = "10000";
                        Console.WriteLine($"ParseIncidentText: Set default CallerPhone={ticket.CallerPhone} (non-numeric PositionLocation)");
                    }
                }
                else if (string.IsNullOrEmpty(ticket.CallerPhone))
                {
                    ticket.CallerPhone = "10000";
                    Console.WriteLine($"ParseIncidentText: Set default CallerPhone={ticket.CallerPhone} (no PositionLocation)");
                }

                if (ticket.OpenTime == default(DateTime))
                {
                    ticket.OpenTime = DateTime.UtcNow;
                    Console.WriteLine($"ParseIncidentText: Set default OpenTime={ticket.OpenTime:yyyy-MM-dd HH:mm:ss} (no OpenTime parsed)");
                }

                if (string.IsNullOrEmpty(ticket.IncidentNumber))
                {
                    Console.WriteLine("ParseIncidentText: Missing IncidentNumber");
                    return (null, "Incident number could not be parsed.");
                }

                Console.WriteLine($"ParseIncidentText: Parsing completed; IncidentNumber={ticket.IncidentNumber}; OpenTime={ticket.OpenTime:yyyy-MM-dd HH:mm:ss}; PositionLocation={ticket.PositionLocation}; ShortDescription={ticket.ShortDescription}; CallerPhone={ticket.CallerPhone}");
                return (ticket, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ParseIncidentText Error: {ex.Message}; StackTrace: {ex.StackTrace}");
                return (null, $"Parsing failed: {ex.Message}");
            }
        }

        public async Task SaveTicketAsync(Ticket ticket)
        {
            try
            {
                Console.WriteLine($"SaveTicketAsync: Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}; IncidentNumber={ticket?.IncidentNumber}; OpenTime={ticket?.OpenTime:yyyy-MM-dd HH:mm:ss}");
                if (ticket == null)
                {
                    Console.WriteLine("SaveTicketAsync: Ticket is null");
                    throw new ArgumentNullException(nameof(ticket));
                }

                await _context.Tickets.AddAsync(ticket);
                var rowsAffected = await _context.SaveChangesAsync();
                Console.WriteLine($"SaveTicketAsync: Saved changes; RowsAffected={rowsAffected}; IncidentNumber={ticket.IncidentNumber}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SaveTicketAsync: Error: {ex.Message}; InnerException: {ex.InnerException?.Message}; StackTrace: {ex.StackTrace}");
                throw;
            }
        }
    }
}