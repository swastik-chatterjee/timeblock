using TimeBlock.Api.Services;

namespace TimeBlock.Tests;

public class ConnectionStringTests
{
    [Theory]
    [InlineData("postgres://")]
    [InlineData("postgresql://")]
    public void UriForm_IsConvertedToKeywordForm(string scheme)
    {
        var normalized = NpgsqlConnectionString.Normalize($"{scheme}timeblock:s3cret@db.example.com:6543/timeblock");

        Assert.DoesNotContain("://", normalized);
        Assert.Contains("Host=db.example.com", normalized);
        Assert.Contains("Port=6543", normalized);
        Assert.Contains("Database=timeblock", normalized);
        Assert.Contains("Username=timeblock", normalized);
        Assert.Contains("Password=s3cret", normalized);
    }

    [Fact]
    public void UriForm_WithoutPortOrCredentials_IsConverted()
    {
        var normalized = NpgsqlConnectionString.Normalize("postgresql://db.example.com/timeblock");

        Assert.Contains("Host=db.example.com", normalized);
        Assert.Contains("Database=timeblock", normalized);
        Assert.DoesNotContain("Port=", normalized);
        Assert.DoesNotContain("Username=", normalized);
        Assert.DoesNotContain("Password=", normalized);
    }

    [Fact]
    public void UriForm_PreservesQueryParameters()
    {
        var normalized = NpgsqlConnectionString.Normalize(
            "postgresql://timeblock:s3cret@db.example.com:5432/timeblock?sslmode=Require");

        Assert.Contains("sslmode=Require", normalized);
    }

    [Fact]
    public void UriForm_DecodesEscapedCredentials()
    {
        var normalized = NpgsqlConnectionString.Normalize(
            "postgresql://user%40mail:p%40ss@db.example.com:5432/timeblock");

        Assert.Contains("Username=user@mail", normalized);
        Assert.Contains("Password=p@ss", normalized);
    }

    [Theory]
    [InlineData("Host=localhost;Port=5432;Database=timeblock;Username=postgres;Password=postgres")]
    [InlineData("Data Source=timeblock.db")]
    [InlineData("")]
    public void KeywordForm_PassesThroughUnchanged(string connectionString)
    {
        Assert.Equal(connectionString, NpgsqlConnectionString.Normalize(connectionString));
    }

    [Fact]
    public void NormalizedUriForm_IsAcceptedByNpgsqlBuilder()
    {
        var normalized = NpgsqlConnectionString.Normalize(
            "postgresql://timeblock:s3cret@db.example.com:5432/timeblock?sslmode=Prefer");
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(normalized);

        Assert.Equal("db.example.com", builder.Host);
        Assert.Equal(5432, builder.Port);
        Assert.Equal("timeblock", builder.Database);
        Assert.Equal("timeblock", builder.Username);
        Assert.Equal("s3cret", builder.Password);
    }
}
