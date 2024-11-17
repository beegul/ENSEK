using System.Text;
using ENSEK.Controllers;
using ENSEK.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ENSEK.Tests;

public class MeterReadingUploadsControllerTests
{
    private readonly MeterReadingContext _context;
    private readonly MeterReadingUploadsController _controller;
    
    public MeterReadingUploadsControllerTests()
    {
        var options = new DbContextOptionsBuilder<MeterReadingContext>()
            .UseInMemoryDatabase(databaseName: "TestMeterReadingDb")
            .Options;
        _context = new MeterReadingContext(options);
        _controller = new MeterReadingUploadsController(_context, new NullLogger<MeterReadingUploadsController>());
    }

    [Fact]
    public async Task UploadMeterReadings_ValidCSV_ReturnsSuccess()
    {
        await ReseedDatabase();
        
        // Arrange
        using var stream = new MemoryStream();
        await using var writer = new StreamWriter(stream, Encoding.UTF8);
        await writer.WriteLineAsync("AccountId,MeterReadingDateTime,MeterReadValue");
        await writer.WriteLineAsync("2344,22/04/2019 09:24,1002");
        await writer.FlushAsync();
        stream.Position = 0;
        var file = new FormFile(stream, 0, stream.Length, "file", "meter_readings.csv")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/csv"
        };
        
        // Act
        var result = await _controller.UploadMeterReadings(file);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

        var actualResult = okResult.Value?.ToString();
        Assert.Contains("SuccessfulReadings = 1", actualResult);
        Assert.Contains("FailedReadings = 0", actualResult);
    }
    
    [Fact]
    public async Task UploadMeterReadings_InvalidCSV_ReturnsFailure()
    {
        await ReseedDatabase();
        
        // Arrange
        using var stream = new MemoryStream();
        await using var writer = new StreamWriter(stream, Encoding.UTF8);
        await writer.WriteLineAsync("AccountId,MeterReadingDateTime,MeterReadValue");
        await writer.WriteLineAsync("2344,22/04/2019 09:24,1002");
        await writer.WriteLineAsync("invalid_account_id,23/04/2019 10:00,500");
        await writer.FlushAsync();
        stream.Position = 0;
        var file = new FormFile(stream, 0, stream.Length, "file", "meter_readings.csv")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/csv"
        };

        // Act
        var result = await _controller.UploadMeterReadings(file);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode); 

        var actualResult = okResult.Value?.ToString();
        Assert.Contains("SuccessfulReadings = 1", actualResult);
        Assert.Contains("FailedReadings = 1", actualResult); 
    }

    [Fact]
    public async Task SeedData_SeedsAccountsCorrectly()
    {
        await ReseedDatabase();
        
        // Arrange
        var csvFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Test_Accounts.csv");
        var expectedCount = File.ReadLines(csvFilePath).Count() - 1;

        await ReseedDatabase();

        // Assert
        Assert.Equal(expectedCount, _context.Accounts.Count());
        Assert.NotNull(await _context.Accounts.FindAsync(2344));
    }
    
    [Fact]
    public async Task UploadMeterReadings_NoFile_ReturnsBadRequest()
    {
        await ReseedDatabase();
        
        // Act
        var result = await _controller.UploadMeterReadings(null);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("No file provided.", badRequestResult.Value);
    }
    
    [Fact]
    public async Task UploadMeterReadings_InvalidFileType_ReturnsBadRequest()
    {
        await ReseedDatabase();
        
        // Arrange
        using var stream = new MemoryStream();
        var file = new FormFile(stream, 0, stream.Length, "file", "text.txt") 
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain" 
        };

        // Act
        var result = await _controller.UploadMeterReadings(file);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid file type. Only CSV files are allowed.", badRequestResult.Value);
    }
    
    [Fact]
    public async Task UploadMeterReadings_EmptyFile_ReturnsSuccessWithNoReadings()
    {
        await ReseedDatabase();
        
        // Arrange
        using var stream = new MemoryStream();
        var file = new FormFile(stream, 0, stream.Length, "file", "empty.csv")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/csv"
        };

        // Act
        var result = await _controller.UploadMeterReadings(file);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode); 

        var actualResult = okResult.Value?.ToString();
        Assert.Contains("SuccessfulReadings = 0", actualResult);
        Assert.Contains("FailedReadings = 0", actualResult); 
    }
    
    [Fact]
    public async Task UploadMeterReadings_DuplicateEntry_ReturnsFailure()
    {
        await ReseedDatabase();
        
        // Arrange - First upload a valid entry
        using var stream1 = new MemoryStream();
        await using var writer1 = new StreamWriter(stream1, Encoding.UTF8);
        await writer1.WriteLineAsync("AccountId,MeterReadingDateTime,MeterReadValue");
        await writer1.WriteLineAsync("2344,22/04/2019 09:24,1002");
        await writer1.FlushAsync();
        stream1.Position = 0;
        var file1 = new FormFile(stream1, 0, stream1.Length, "file1", "meter_readings1.csv")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/csv"
        };
        await _controller.UploadMeterReadings(file1);

        // Arrange - Now try to upload the same entry again
        using var stream2 = new MemoryStream();
        await using var writer2 = new StreamWriter(stream2, Encoding.UTF8);
        await writer2.WriteLineAsync("AccountId,MeterReadingDateTime,MeterReadValue");
        await writer2.WriteLineAsync("2344,22/04/2019 09:24,1002"); 
        await writer2.FlushAsync();
        stream2.Position = 0;
        var file2 = new FormFile(stream2, 0, stream2.Length, "file2", "meter_readings2.csv")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/csv"
        };

        // Act
        var result = await _controller.UploadMeterReadings(file2);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode); 

        var actualResult = okResult.Value?.ToString();
        Assert.Contains("SuccessfulReadings = 0", actualResult);
        Assert.Contains("FailedReadings = 1", actualResult); 
    }
    
    [Fact]
    public async Task UploadMeterReadings_InvalidMeterValue_ReturnsFailure()
    {
        await ReseedDatabase();
        
        // Arrange
        using var stream = new MemoryStream();
        await using var writer = new StreamWriter(stream, Encoding.UTF8);
        await writer.WriteLineAsync("AccountId, MeterReadingDateTime, MeterReadValue");
        await writer.WriteLineAsync("2344,22/04/2019 09:24,1002"); // Valid
        await writer.WriteLineAsync("2233,22/04/2019 12:25,123456"); // Invalid - Too many digits
        await writer.WriteLineAsync("8766,22/04/2019 12:25,ABC"); // Invalid - Not a number
        await writer.FlushAsync();
        stream.Position = 0;
        var file = new FormFile(stream, 0, stream.Length, "file", "meter_readings.csv")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/csv"
        };

        // Act
        var result = await _controller.UploadMeterReadings(file);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

        var actualResult = okResult.Value?.ToString();
        Assert.Contains("SuccessfulReadings = 1", actualResult);
        Assert.Contains("FailedReadings = 2", actualResult); 
    }
    
    [Fact]
    public async Task UploadMeterReadings_InvalidAccountId_ReturnsFailure()
    {
        await ReseedDatabase();
        
        // Arrange
        using var stream = new MemoryStream();
        await using var writer = new StreamWriter(stream, Encoding.UTF8);
        await writer.WriteLineAsync("AccountId,MeterReadingDateTime,MeterReadValue");
        await writer.WriteLineAsync("2344,22/04/2019 09:24,1002"); // Valid
        await writer.WriteLineAsync("9999,22/04/2019 12:25,12345"); // Invalid - Account ID doesn't exist
        await writer.FlushAsync();
        stream.Position = 0;
        var file = new FormFile(stream, 0, stream.Length, "file", "meter_readings.csv")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/csv"
        };

        // Act
        var result = await _controller.UploadMeterReadings(file);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

        var actualResult = okResult.Value?.ToString();
        Assert.Contains("SuccessfulReadings = 1", actualResult);
        Assert.Contains("FailedReadings = 1", actualResult); 
    }
    
    [Fact]
    public async Task UploadMeterReadings_NegativeMeterValue_ReturnsFailure()
    {
        await ReseedDatabase();
        
        // Arrange
        using var stream = new MemoryStream();
        await using var writer = new StreamWriter(stream, Encoding.UTF8);
        await writer.WriteLineAsync("AccountId, MeterReadingDateTime, MeterReadValue");
        await writer.WriteLineAsync("2344, 22/04/2019 09:24, -1002"); // Invalid - Negative value
        await writer.FlushAsync();
        stream.Position = 0;
        var file = new FormFile(stream, 0, stream.Length, "file", "meter_readings.csv")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/csv"
        };

        // Act
        var result = await _controller.UploadMeterReadings(file);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

        var actualResult = okResult.Value?.ToString();
        Assert.Contains("SuccessfulReadings = 0", actualResult);
        Assert.Contains("FailedReadings = 1", actualResult);
    }
    
    [Fact]
    public async Task UploadMeterReadings_LeadingZeroMeterValue_ReturnsSuccess()
    {
        await ReseedDatabase();
        
        // Arrange
        using var stream = new MemoryStream();
        await using var writer = new StreamWriter(stream, Encoding.UTF8);
        await writer.WriteLineAsync("AccountId,MeterReadingDateTime,MeterReadValue");
        await writer.WriteLineAsync("2344,22/04/2019 09:24,01002");
        await writer.FlushAsync();
        stream.Position = 0;
        var file = new FormFile(stream, 0, stream.Length, "file", "meter_readings.csv")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/csv"
        };

        // Act
        var result = await _controller.UploadMeterReadings(file);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

        var actualResult = okResult.Value?.ToString();
        Assert.Contains("SuccessfulReadings = 1", actualResult);
        Assert.Contains("FailedReadings = 0", actualResult);
    }
    
    [Fact]
    public async Task UploadMeterReadings_DecimalMeterValue_ReturnsFailure()
    {
        await ReseedDatabase();
        
        // Arrange
        using var stream = new MemoryStream();
        await using var writer = new StreamWriter(stream, Encoding.UTF8);
        await writer.WriteLineAsync("AccountId, MeterReadingDateTime, MeterReadValue");
        await writer.WriteLineAsync("2344,22/04/2019 09:24,1002.5"); // Invalid - Decimal value
        await writer.FlushAsync();
        stream.Position = 0;
        var file = new FormFile(stream, 0, stream.Length, "file", "meter_readings.csv")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/csv"
        };

        // Act
        var result = await _controller.UploadMeterReadings(file);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

        var actualResult = okResult.Value?.ToString();
        Assert.Contains("SuccessfulReadings = 0", actualResult);
        Assert.Contains("FailedReadings = 1", actualResult);
    }
    
    [Fact]
    public async Task UploadMeterReadings_BoundaryConditions_ReturnsSuccess()
    {
        await ReseedDatabase();
        
        // Arrange
        using var stream = new MemoryStream();
        await using var writer = new StreamWriter(stream, Encoding.UTF8);
        await writer.WriteLineAsync("AccountId, MeterReadingDateTime, MeterReadValue");
        await writer.WriteLineAsync("2344,22/04/2019 09:24,00000"); // Valid - Minimum value
        await writer.WriteLineAsync("2344,22/04/2019 09:25,99999"); // Valid - Maximum value
        await writer.FlushAsync();
        stream.Position = 0;
        var file = new FormFile(stream, 0, stream.Length, "file", "meter_readings.csv")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/csv"
        };

        // Act
        var result = await _controller.UploadMeterReadings(file);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

        var actualResult = okResult.Value?.ToString();
        Assert.Contains("SuccessfulReadings = 2", actualResult);
        Assert.Contains("FailedReadings = 0", actualResult);
    }
    
    [Fact]
    public async Task UploadMeterReadings_MultipleErrors_ReturnsCorrectCounts()
    {
        await ReseedDatabase();
        
        // Arrange
        using var stream = new MemoryStream();
        await using var writer = new StreamWriter(stream, Encoding.UTF8);
        await writer.WriteLineAsync("AccountId,MeterReadingDateTime,MeterReadValue");
        await writer.WriteLineAsync("2344,22/04/2019 09:24,1002"); // Valid
        await writer.WriteLineAsync("2233,22/04/2019 12:25,123456"); // Invalid - Too many digits
        await writer.WriteLineAsync("8766,22/04/2019 12:25,ABC"); // Invalid - Not a number
        await writer.WriteLineAsync("9999,22/04/2019 12:25,12345"); // Invalid - Account ID doesn't exist
        await writer.WriteLineAsync("2344,22/04/2019 09:24,1002"); // Duplicate
        await writer.FlushAsync();
        stream.Position = 0;
        var file = new FormFile(stream, 0, stream.Length, "file", "meter_readings.csv")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/csv"
        };

        // Act
        var result = await _controller.UploadMeterReadings(file);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

        var actualResult = okResult.Value?.ToString();
        Assert.Contains("SuccessfulReadings = 1", actualResult);
        Assert.Contains("FailedReadings = 4", actualResult);
    }
    
    [Fact]
    public async Task UploadMeterReadings_InvalidDateFormat_ReturnsFailure()
    {
        await ReseedDatabase();
        
        // Arrange
        using var stream = new MemoryStream();
        await using var writer = new StreamWriter(stream, Encoding.UTF8);
        await writer.WriteLineAsync("AccountId, MeterReadingDateTime, MeterReadValue");
        await writer.WriteLineAsync("2344, 22-04-2019 09:24, 1002"); // Invalid date format
        await writer.FlushAsync();
        stream.Position = 0;
        var file = new FormFile(stream, 0, stream.Length, "file", "meter_readings.csv")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/csv"
        };

        // Act
        var result = await _controller.UploadMeterReadings(file);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

        var actualResult = okResult.Value?.ToString();
        Assert.Contains("SuccessfulReadings = 0", actualResult);
        Assert.Contains("FailedReadings = 1", actualResult);
    }
    
    [Fact]
    public async Task UploadMeterReadings_MissingColumns_ReturnsFailure()
    {
        await ReseedDatabase();
        
        // Arrange
        using var stream = new MemoryStream();
        await using var writer = new StreamWriter(stream, Encoding.UTF8);
        await writer.WriteLineAsync("AccountId, MeterReadValue"); // Missing MeterReadingDateTime column
        await writer.WriteLineAsync("2344, 1002");
        await writer.FlushAsync();
        stream.Position = 0;
        var file = new FormFile(stream, 0, stream.Length, "file", "meter_readings.csv")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/csv"
        };

        // Act
        var result = await _controller.UploadMeterReadings(file);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

        var actualResult = okResult.Value?.ToString();
        Assert.Contains("SuccessfulReadings = 0", actualResult);
        Assert.Contains("FailedReadings = 1", actualResult);
    }
    
    [Fact]
    public async Task UploadMeterReadings_ExtraColumns_ReturnsSuccess()
    {
        await ReseedDatabase();
        
        // Arrange
        using var stream = new MemoryStream();
        await using var writer = new StreamWriter(stream, Encoding.UTF8);
        await writer.WriteLineAsync("AccountId,MeterReadingDateTime,MeterReadValue,ExtraColumn1,ExtraColumn2");
        await writer.WriteLineAsync("2344,22/04/2019 09:24,1002,extra_value1,extra_value2");
        await writer.FlushAsync();
        stream.Position = 0;
        var file = new FormFile(stream, 0, stream.Length, "file", "meter_readings.csv")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/csv"
        };

        // Act
        var result = await _controller.UploadMeterReadings(file);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

        var actualResult = okResult.Value?.ToString();
        Assert.Contains("SuccessfulReadings = 1", actualResult);
        Assert.Contains("FailedReadings = 0", actualResult);
    }
    
    [Fact]
    public async Task UploadMeterReadings_OlderReading_ReturnsFailure()
    {
        await ReseedDatabase();

        // Arrange
        using var stream1 = new MemoryStream();
        await using var newStream = new StreamWriter(stream1, Encoding.UTF8);
        await newStream.WriteLineAsync("AccountId,MeterReadingDateTime,MeterReadValue");
        await newStream.WriteLineAsync("2344,22/04/2019 09:24,1002"); 
        await newStream.FlushAsync();
        stream1.Position = 0;
        var file1 = new FormFile(stream1, 0, stream1.Length, "file1", "meter_readings1.csv")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/csv"
        };
        await _controller.UploadMeterReadings(file1);

        // Arrange
        using var stream2 = new MemoryStream();
        await using var oldStream = new StreamWriter(stream2, Encoding.UTF8);
        await oldStream.WriteLineAsync("AccountId,MeterReadingDateTime,MeterReadValue");
        await oldStream.WriteLineAsync("2344,21/04/2019 09:24,1001");
        await oldStream.FlushAsync();
        stream2.Position = 0;
        var file2 = new FormFile(stream2, 0, stream2.Length, "file2", "meter_readings2.csv")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/csv"
        };

        // Act
        var result = await _controller.UploadMeterReadings(file2);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

        var actualResult = okResult.Value?.ToString();
        Assert.Contains("SuccessfulReadings = 0", actualResult);
        Assert.Contains("FailedReadings = 1", actualResult);
    }
    
    private  async Task ReseedDatabase()
    {
        try
        {
            _context.MeterReadings.RemoveRange(_context.MeterReadings);
            await _context.SaveChangesAsync();
            _context.SeedData("Test_Accounts.csv");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error resetting database: {ex.Message}");
        }
    }
}
