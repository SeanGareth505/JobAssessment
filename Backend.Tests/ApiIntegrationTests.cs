using System.Net.Http.Json;
using Backend.Data;
using Backend.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Backend.Tests;

public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Backend.Program>>
{
    private readonly HttpClient _client;

    public ApiIntegrationTests(WebApplicationFactory<Backend.Program> factory)
    {
        _client = factory.WithWebHostBuilder(b =>
        {
            b.ConfigureServices(s =>
            {
                var toRemove = s.Where(x => x.ServiceType == typeof(DbContextOptions<AppDbContext>) || x.ServiceType == typeof(AppDbContext)).ToList();
                foreach (var d in toRemove) s.Remove(d);
                s.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase("TestDb"));
            });
        }).CreateClient();
    }

    [Fact]
    public async Task Healthz_ReturnsOk()
    {
        var res = await _client.GetAsync("/healthz");
        res.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GetCustomersPage_ReturnsOk()
    {
        var res = await _client.GetAsync("/api/customers?page=1&pageSize=10");
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<PagedResult<CustomerDto>>();
        Assert.NotNull(body);
        Assert.NotNull(body.Items);
        Assert.True(body.PageSize <= 100);
    }
}
