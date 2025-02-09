using System.IO.Hashing;
using Microsoft.Data.Sqlite;

namespace PicArchiver.Core.Metadata.Loaders;

public sealed class ChecksumMetadataLoader : MetadataLoader
{
    private SqliteConnection? _connection;

    public override bool Initialize(FileArchiveContext context)
    {
        var checksum = context.Metadata.GetChecksum();
        var size = context.Metadata.GetFileSize();
            
        OpenDbConnection(context.DestinationBasePath);
        var existingName = ChecksumExists(checksum, size);
        if (!string.IsNullOrEmpty(existingName))
        {
            context.Metadata[FileMetadata.ExistingPicNameKey] = existingName;
            if (!context.Config.IgnoreDuplicates.GetValueOrDefault())
                return false;
        }

        var name = context.DestFileFullPath.Replace(context.DestinationBasePath.TrimEnd('/'), ".");
        var dateTime = context.Metadata.GetFileDateTime();
        if (dateTime == default)
            return false;
        
        InsertNewChecksumRecord(name, checksum, size, dateTime.Ticks);
        return true;
    }

    public override bool LoadMetadata(string path, FileMetadata metadata)
    {
        using var stream = File.OpenRead(path);
        var crcChecksum = new Crc64();
        crcChecksum.Append(stream);
        
        metadata.SetChecksum(crcChecksum.GetCurrentHashAsUInt64());
        metadata.SetFileSize(stream.Length);
        
        return true;
    }

    public override void Finalize(FileArchiveContext? context)
    {
        if (context is null) // Context null means we finished the archive process.
            CloseDbConnection();
    }

    private SqliteConnection OpenDbConnection(string path)
    {
        if (_connection != null) 
            return _connection;
        
        _connection = new SqliteConnection($"Data Source={Path.Combine(path, "checksums.db")}");
        _connection.Open();

        ExecuteSql(Sql.BeginExclusiveTransactionSql); // Just to test if the database is locked.
        ExecuteSql(Sql.CommitTransactionSql);
        ExecuteSql(Sql.BeginTransactionSql);
        ExecuteSql(Sql.CreateTableSql);

        return _connection;
    }

    private int ExecuteSql(string sql)
    {
        if (_connection == null)
            return -1;
        
        var command = _connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteNonQuery();
    }
    
    private int InsertNewChecksumRecord(string name, ulong checksum, long size, long timestamp)
    {
        if (_connection == null)
            return -1;
        
        var command = _connection.CreateCommand();
        command.CommandText = Sql.InsertSql;
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$checksum", checksum);
        command.Parameters.AddWithValue("$size", size);
        command.Parameters.AddWithValue("$timestamp", timestamp);
        command.Parameters.AddWithValue("$addeondtimestamp", DateTime.Now.Ticks);
        
        return command.ExecuteNonQuery();
    }

    private string? ChecksumExists(ulong checksum, long size)
    {
        if (_connection == null)
            return null;
        
        using var command = _connection.CreateCommand();
        command.CommandText = Sql.FindDuplicateSql;
        command.Parameters.AddWithValue("$checksum", checksum);
        command.Parameters.AddWithValue("$size", size);

        using var reader = command.ExecuteReader();
        return reader.Read() ? reader.GetString(0) : string.Empty;
    }

    private void CloseDbConnection()
    {
        if (_connection == null) 
            return;

        ExecuteSql(Sql.CommitTransactionSql);
        
        var connection = _connection;
        _connection = null;
        
        connection.Close();
        connection.Dispose();
    }
    
    private static class Sql
    {
        public const string CreateTableSql = """
                                             CREATE TABLE IF NOT EXISTS pictures (
                                                 name             TEXT (128) PRIMARY KEY NOT NULL,
                                                 checksum         INTEGER KEY NOT NULL,
                                                 size             INTEGER KEY NOT NULL,
                                                 timestamp        INTEGER KEY NOT NULL,
                                                 addeondtimestamp INTEGER KEY NOT NULL
                                             ) WITHOUT ROWID;
                                             """;

        public const string BeginTransactionSql = "BEGIN TRANSACTION;";
        public const string BeginExclusiveTransactionSql = "BEGIN EXCLUSIVE;";
        public const string CommitTransactionSql = "COMMIT;";

        public const string InsertSql =
            "INSERT OR REPLACE INTO pictures (name, checksum, size, timestamp, addeondtimestamp) VALUES ($name, $checksum, $size, $timestamp, $addeondtimestamp);";
        
        public const string SelectSql = "SELECT checksum, size, timestamp FROM pictures WHERE name = $name";
        public const string FindDuplicateSql = "SELECT name FROM pictures WHERE checksum = $checksum AND size = $size";
    }
}

public static class ChecksumMetadataExtensions
{
    public static ulong GetChecksum(this FileMetadata metadata) => metadata.Get<ulong>(FileMetadata.ChecksumKey);
    public static void SetChecksum(this FileMetadata metadata, ulong checksum) => metadata[FileMetadata.ChecksumKey] = checksum;
    public static long GetFileSize(this FileMetadata metadata) => metadata.Get<long>(FileMetadata.FileSizeKey);
    public static void SetFileSize(this FileMetadata metadata, long fileSize) => metadata[FileMetadata.FileSizeKey] = fileSize;
    
    public static string? GetExistingFileName(this FileMetadata metadata) => metadata[FileMetadata.ExistingPicNameKey] as string;
}