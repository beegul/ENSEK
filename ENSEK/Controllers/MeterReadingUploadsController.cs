using System.Globalization;
using ENSEK.Entities;
using ENSEK.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ENSEK.Controllers;

[ApiController]
[Route("meter-reading-uploads")] 
public class MeterReadingUploadsController(MeterReadingContext context, ILogger<MeterReadingUploadsController> logger) : ControllerBase
{
    /// <summary>
    /// Uploads and processes meter readings from a CSV file.
    /// </summary>
    /// <param name="file">The CSV file containing meter readings.</param>
    /// <returns>The number of successful and failed readings.</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadMeterReadings(IFormFile? file)
    {
        // Check if a file was provided.
        if (file == null)
            return BadRequest("No file provided.");

        // Validate the file type.
        if (file.ContentType != "text/csv")
            return BadRequest("Invalid file type. Only CSV files are allowed.");

        var successfulReadings = 0;
        var failedReadings = 0;

        // Retrieve existing accounts for validation.
        var existingAccounts = await context.Accounts.ToDictionaryAsync(a => a.AccountId);

        using var reader = new StreamReader(file.OpenReadStream());
        await reader.ReadLineAsync(); // Skip header row.

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();

            // Skip empty lines.
            if (string.IsNullOrWhiteSpace(line))
            {
                logger.LogWarning("Empty line found in file");
                failedReadings++;
                continue;
            }
            
            var values = line.Split(',');

            // Validate that the line has enough values.
            if (values.Length < 3)  
            {
                logger.LogWarning("Line does not contain enough values: {Line}", line);
                failedReadings++;
                continue; 
            }

            // Parse the values from the line.
            if (!int.TryParse(values[0], out var accountId) ||
                !DateTime.TryParseExact(values[1], "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var meterReadingDateTime) ||
                !int.TryParse(values[2], out var meterReadValue))
            {
                logger.LogWarning("Invalid values found in line: {Line}", line);
                failedReadings++;
                continue;
            }
            
            // Validate the meter reading value.
            if (meterReadValue is >= 0 and <= 99999)
            {
                // Pad with zeros if necessary to make it 5 digits.
                var meterReadValueString = meterReadValue.ToString().PadLeft(5, '0'); 
                meterReadValue = int.Parse(meterReadValueString); 
            }
            else
            {
                logger.LogWarning("Invalid meter reading value found in line: {Line}", line);
                failedReadings++;
                continue;
            }
            
            // Check if the account exists.
            if (!existingAccounts.ContainsKey(accountId))
            {
                logger.LogWarning("Account {AccountId} does not exist", accountId);
                failedReadings++;
                continue;
            }

            var meterReading = new MeterReading
            {
                AccountId = accountId,
                MeterReadingDateTime = meterReadingDateTime,
                MeterReadValue = meterReadValue
            };
            
            // Check for duplicate meter readings.
            if (await context.MeterReadings.AnyAsync(mr => mr.AccountId == meterReading.AccountId && mr.MeterReadingDateTime == meterReading.MeterReadingDateTime))
            {
                logger.LogWarning("Meter reading for account {AccountId} at {MeterReadingDateTime} already exists", accountId, meterReadingDateTime);
                failedReadings++;
                continue; 
            }
            
            // Check for newer meter readings
            var existingReading = await context.MeterReadings
                .Where(mr => mr.AccountId == meterReading.AccountId)
                .OrderByDescending(mr => mr.MeterReadingDateTime)
                .FirstOrDefaultAsync();

            if (existingReading != null && existingReading.MeterReadingDateTime >= meterReading.MeterReadingDateTime)
            {
                logger.LogWarning("Meter reading for account {AccountId} at {MeterReadingDateTime} is older than the existing reading", accountId, meterReadingDateTime);
                failedReadings++;
                continue; 
            }

            try
            {
                // Add and save the meter reading.
                context.MeterReadings.Add(meterReading);
                await context.SaveChangesAsync();
                successfulReadings++;
            }
            catch (DbUpdateException)
            {
                logger.LogWarning("Failed to save meter reading to database: {@MeterReading}", meterReading);
                failedReadings++;
            }
        }

        logger.LogInformation("Successful readings: {SuccessfulReadings}, Failed readings: {FailedReadings}", successfulReadings, failedReadings);

        // Return the results.
        return Ok(new { SuccessfulReadings = successfulReadings, FailedReadings = failedReadings });
    }
}