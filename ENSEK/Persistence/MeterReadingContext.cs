using ENSEK.Entities;
using Microsoft.EntityFrameworkCore;

namespace ENSEK.Persistence;

public class MeterReadingContext : DbContext
{
    public MeterReadingContext(DbContextOptions<MeterReadingContext> options) : base(options)
    {
    }

    public DbSet<Account> Accounts { get; set; }
    public DbSet<MeterReading> MeterReadings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MeterReading>()
            .HasIndex(mr => new { mr.AccountId, mr.MeterReadingDateTime })
            .IsUnique();
    }
    
    // Seeds the in memory database with the data from the CSV file.
    public void SeedData(string csvFilePath)
    {
        if (!Accounts.Any())
        {
            using var reader = new StreamReader(csvFilePath);
            reader.ReadLine();

            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                var values = line.Split(',');

                if (values.Length
                    == 3 && 
                    int.TryParse(values[0],
                        out var accountId)) 
                {
                    var account = new Account
                    {
                        AccountId = accountId,
                        FirstName = values[1],
                        LastName = values[2]
                    };
                    Accounts.Add(account);
                }
            }

            SaveChanges();
        }
    }
}
