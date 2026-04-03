using Moq;
using Xunit;
using VehicleTelemetryAPI.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace VehicleTelemetryAPI.Tests;

/// <summary>
/// Tests for the MemoryCacheService functionality.
/// </summary>
public class MemoryCacheServiceTests
{
    private readonly MemoryCacheService _cacheService;
    private readonly IMemoryCache _memoryCache;
    private readonly Mock<ILogger<MemoryCacheService>> _mockLogger;

    public MemoryCacheServiceTests()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _mockLogger = new Mock<ILogger<MemoryCacheService>>();
        _cacheService = new MemoryCacheService(_memoryCache, _mockLogger.Object);
    }

    [Fact]
    public void SetAndGet_WithStringValue_ShouldReturnValue()
    {
        // Arrange
        string key = "test_key";
        string value = "test_value";

        // Act
        _cacheService.Set(key, value, TimeSpan.FromMinutes(1));
        var result = _cacheService.Get<string>(key);

        // Assert
        Assert.Equal(value, result);
    }

    [Fact]
    public void SetAndGet_WithIntValue_ShouldReturnValue()
    {
        // Arrange
        string key = "test_int";
        int value = 42;

        // Act
        _cacheService.Set(key, value, TimeSpan.FromMinutes(1));
        var result = _cacheService.Get<int>(key);

        // Assert
        Assert.Equal(value, result);
    }

    [Fact]
    public void SetAndGet_WithComplexObject_ShouldReturnValue()
    {
        // Arrange
        string key = "test_object";
        var value = new { Name = "Test", Id = 123 };

        // Act
        _cacheService.Set(key, value, TimeSpan.FromMinutes(1));
        var result = _cacheService.Get<object>(key);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void Get_WithNonExistentKey_ShouldReturnNull()
    {
        // Act
        var result = _cacheService.Get<string>("non_existent_key");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Remove_WithExistingKey_ShouldRemoveValue()
    {
        // Arrange
        string key = "test_remove";
        string value = "test_value";
        _cacheService.Set(key, value, TimeSpan.FromMinutes(1));

        // Act
        _cacheService.Remove(key);
        var result = _cacheService.Get<string>(key);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Set_WithZeroDuration_ShouldExpireImmediately()
    {
        // Arrange
        string key = "test_expire";
        string value = "test_value";

        // Act
        _cacheService.Set(key, value, TimeSpan.Zero);
        System.Threading.Thread.Sleep(100);
        var result = _cacheService.Get<string>(key);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Set_WithDifferentKeys_ShouldStoreSeparately()
    {
        // Arrange
        _cacheService.Set("key1", "value1", TimeSpan.FromMinutes(1));
        _cacheService.Set("key2", "value2", TimeSpan.FromMinutes(1));

        // Act
        var result1 = _cacheService.Get<string>("key1");
        var result2 = _cacheService.Get<string>("key2");

        // Assert
        Assert.Equal("value1", result1);
        Assert.Equal("value2", result2);
    }

    [Fact]
    public void Get_WithWrongType_ShouldReturnNull()
    {
        // Arrange
        string key = "test_type";
        _cacheService.Set(key, "string_value", TimeSpan.FromMinutes(1));

        // Act
        var result = _cacheService.Get<int>(key);

        // Assert
        Assert.Equal(0, result); // Default int value
    }

    [Fact]
    public void Set_WithNullValue_ShouldStore()
    {
        // Arrange
        string key = "null_test";

        // Act
        _cacheService.Set(key, (string?)null, TimeSpan.FromMinutes(1));
        var exists = _cacheService.Get<string?>(key);

        // Assert
        Assert.Null(exists);
    }

    [Fact]
    public void Set_WithMultipleUpdates_ShouldReturnLatestValue()
    {
        // Arrange
        string key = "test_update";

        // Act
        _cacheService.Set(key, "value1", TimeSpan.FromMinutes(1));
        _cacheService.Set(key, "value2", TimeSpan.FromMinutes(1));
        _cacheService.Set(key, "value3", TimeSpan.FromMinutes(1));
        var result = _cacheService.Get<string>(key);

        // Assert
        Assert.Equal("value3", result);
    }

    [Fact]
    public void Set_WithLongDuration_ShouldKeepValue()
    {
        // Arrange
        string key = "long_duration";
        string value = "persistent_value";

        // Act
        _cacheService.Set(key, value, TimeSpan.FromHours(1));
        var result = _cacheService.Get<string>(key);

        // Assert
        Assert.Equal(value, result);
    }

    [Fact]
    public void Remove_WithNonExistentKey_ShouldNotThrow()
    {
        // Act & Assert
        _cacheService.Remove("non_existent");
    }

    [Fact]
    public void Set_WithListOfValues_ShouldStore()
    {
        // Arrange
        string key = "list_test";
        var values = new List<string> { "a", "b", "c" };

        // Act
        _cacheService.Set(key, values, TimeSpan.FromMinutes(1));
        var result = _cacheService.Get<List<string>>(key);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task SetAsync_AndGetAsync_ShouldWork()
    {
        // Arrange
        string key = "async_test";
        string value = "async_value";

        // Act
        await _cacheService.SetAsync(key, value, TimeSpan.FromMinutes(1));
        var result = await _cacheService.GetAsync<string>(key);

        // Assert
        Assert.Equal(value, result);
    }

    [Fact]
    public async Task RemoveAsync_WithExistingKey_ShouldRemoveValue()
    {
        // Arrange
        string key = "async_remove";
        string value = "test_value";
        await _cacheService.SetAsync(key, value, TimeSpan.FromMinutes(1));

        // Act
        await _cacheService.RemoveAsync(key);
        var result = await _cacheService.GetAsync<string>(key);

        // Assert
        Assert.Null(result);
    }
}
