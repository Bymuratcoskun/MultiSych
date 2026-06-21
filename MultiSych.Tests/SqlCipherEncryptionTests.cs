using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using MultiSych.Services.Data;
using MultiSych.Services.Security;

namespace MultiSych.Tests
{
    public class SqlCipherEncryptionTests : IDisposable
    {
        private readonly string _dbPath;

        public SqlCipherEncryptionTests()
        {
            SQLitePCL.Batteries_V2.Init();
            _dbPath = Path.Combine(Path.GetTempPath(), $"multisych_test_{Guid.NewGuid()}.db");
        }

        public void Dispose()
        {
            if (File.Exists(_dbPath))
            {
                try { File.Delete(_dbPath); } catch { }
            }
        }

        [Fact]
        public void EncryptedDatabase_WithCorrectPassword_ShouldReadAndWrite()
        {
            // Arrange
            var password = "TestDbPassword123!";
            var connectionString = SecurityHelper.BuildSqlCipherConnectionString(_dbPath, password, encryptDatabase: true);

            var optionsBuilder = new DbContextOptionsBuilder<LocalCacheDbContext>();
            optionsBuilder.UseSqlite(connectionString);

            // Act & Assert - Write Data
            using (var context = new LocalCacheDbContext(optionsBuilder.Options))
            {
                context.Database.EnsureCreated();

                var secret = new AppSecretEntity
                {
                    Key = "TEST_API_KEY",
                    Value = "encrypted_value_here"
                };

                context.AppSecrets.Add(secret);
                context.SaveChanges();
            }

            // Act & Assert - Read Data with Correct Password
            using (var context = new LocalCacheDbContext(optionsBuilder.Options))
            {
                var retrieved = context.AppSecrets.FirstOrDefault(s => s.Key == "TEST_API_KEY");
                Assert.NotNull(retrieved);
                Assert.Equal("encrypted_value_here", retrieved.Value);
            }
        }

        [Fact]
        public void EncryptedDatabase_WithWrongPassword_ShouldThrowSqliteException()
        {
            // Arrange
            var password = "TestDbPassword123!";
            var wrongPassword = "WrongPassword999!";
            
            var correctConnectionString = SecurityHelper.BuildSqlCipherConnectionString(_dbPath, password, encryptDatabase: true);
            var wrongConnectionString = SecurityHelper.BuildSqlCipherConnectionString(_dbPath, wrongPassword, encryptDatabase: true);

            var correctOptions = new DbContextOptionsBuilder<LocalCacheDbContext>().UseSqlite(correctConnectionString).Options;
            var wrongOptions = new DbContextOptionsBuilder<LocalCacheDbContext>().UseSqlite(wrongConnectionString).Options;

            // 1. Create and populate database using correct password
            using (var context = new LocalCacheDbContext(correctOptions))
            {
                context.Database.EnsureCreated();
                context.AppSecrets.Add(new AppSecretEntity { Key = "SECRET", Value = "VALUE" });
                context.SaveChanges();
            }

            SqliteConnection.ClearAllPools();

            using (var context = new LocalCacheDbContext(wrongOptions))
            {
                var sqliteEx = Assert.Throws<SqliteException>(() =>
                {
                    _ = context.AppSecrets.ToList();
                });
                
                // SqliteErrorCode 26 is SQLITE_NOTADB (file is encrypted or not a database)
                Assert.True(sqliteEx.SqliteErrorCode == 26 || sqliteEx.SqliteErrorCode == 11 /* SQLITE_CORRUPT */);
            }
        }

        [Fact]
        public void EncryptedDatabase_WithoutPassword_ShouldThrowSqliteException()
        {
            // Arrange
            var password = "TestDbPassword123!";
            
            var correctConnectionString = SecurityHelper.BuildSqlCipherConnectionString(_dbPath, password, encryptDatabase: true);
            var unencryptedConnectionString = SecurityHelper.BuildSqlCipherConnectionString(_dbPath, null, encryptDatabase: false);

            var correctOptions = new DbContextOptionsBuilder<LocalCacheDbContext>().UseSqlite(correctConnectionString).Options;
            var unencryptedOptions = new DbContextOptionsBuilder<LocalCacheDbContext>().UseSqlite(unencryptedConnectionString).Options;

            // 1. Create and populate database using correct password
            using (var context = new LocalCacheDbContext(correctOptions))
            {
                context.Database.EnsureCreated();
                context.AppSecrets.Add(new AppSecretEntity { Key = "SECRET", Value = "VALUE" });
                context.SaveChanges();
            }

            SqliteConnection.ClearAllPools();

            using (var context = new LocalCacheDbContext(unencryptedOptions))
            {
                var sqliteEx = Assert.Throws<SqliteException>(() =>
                {
                    _ = context.AppSecrets.ToList();
                });

                Assert.True(sqliteEx.SqliteErrorCode == 26 || sqliteEx.SqliteErrorCode == 11);
            }
        }

        [Fact]
        public void TestCipherVersion()
        {
            SQLitePCL.Batteries_V2.Init();
            using var connection = new SqliteConnection("Data Source=:memory:;Password=test;");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA cipher_version;";
            var version = command.ExecuteScalar() as string;
            Assert.NotNull(version);
        }
    }
}
