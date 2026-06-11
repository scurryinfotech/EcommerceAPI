using EcommerceAPI.Models;
using Microsoft.Data.SqlClient;

namespace EcommerceAPI.Repositories
{
    public class ContactRepository : IContactRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<ContactRepository> _logger;

        public ContactRepository(IConfiguration config, ILogger<ContactRepository> logger)
        {
            _connectionString = config.GetConnectionString("EcommerceDb")
                                ?? throw new InvalidOperationException("Connection string not found.");
            _logger = logger;
        }

        // ── Save contact to DB ──────────────────────────────────
        public async Task<bool> SaveContactAsync(ContactModel contact)
        {
            const string query = @"
                INSERT INTO Contact (Name, Email, Phone, Message)
                VALUES (@Name, @Email, @PhoneNumber, @Comment)";

            try
            {
                using var conn = new SqlConnection(_connectionString);
                using var cmd = new SqlCommand(query, conn);

                cmd.Parameters.AddWithValue("@Name", (object?)contact.name ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Email", contact.email);
                cmd.Parameters.AddWithValue("@PhoneNumber", (object?)contact.phoneNumber ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Comment", (object?)contact.comment ?? DBNull.Value);
                //cmd.Parameters.AddWithValue("@CreatedAt", contact.CreatedAt);

                await conn.OpenAsync();
                var rowsAffected = await cmd.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving contact for {Email}", contact.email);
                return false;
            }
        }

        // ── Get all contacts ────────────────────────────────────
        //public async Task<IEnumerable<ContactModel>> GetAllContactsAsync()
        //{
        //    const string query = "SELECT Id, Name, Email, PhoneNumber, Comment, CreatedAt FROM Contacts ORDER BY CreatedAt DESC";
        //    var list = new List<ContactModel>();

        //    try
        //    {
        //        using var conn = new SqlConnection(_connectionString);
        //        using var cmd = new SqlCommand(query, conn);

        //        await conn.OpenAsync();
        //        using var reader = await cmd.ExecuteReaderAsync();

        //        while (await reader.ReadAsync())
        //        {
        //            list.Add(new ContactModel
        //            {
        //                Id = reader.GetInt32(0),
        //                Name = reader.IsDBNull(1) ? null : reader.GetString(1),
        //                Email = reader.GetString(2),
        //                PhoneNumber = reader.IsDBNull(3) ? null : reader.GetString(3),
        //                Comment = reader.IsDBNull(4) ? null : reader.GetString(4),
        //                //CreatedAt = reader.GetDateTime(5)
        //            });
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error fetching all contacts.");
        //    }

        //    return list;
        //}

        // ── Get contact by Id ───────────────────────────────────
        //public async Task<ContactModel?> GetContactByIdAsync(int id)
        //{
        //    const string query = "SELECT Id, Name, Email, PhoneNumber, Comment, CreatedAt FROM Contacts WHERE Id = @Id";

        //    try
        //    {
        //        using var conn = new SqlConnection(_connectionString);
        //        using var cmd = new SqlCommand(query, conn);

        //        cmd.Parameters.AddWithValue("@Id", id);

        //        await conn.OpenAsync();
        //        using var reader = await cmd.ExecuteReaderAsync();

        //        if (await reader.ReadAsync())
        //        {
        //            return new ContactModel
        //            {
        //                Id = reader.GetInt32(0),
        //                Name = reader.IsDBNull(1) ? null : reader.GetString(1),
        //                Email = reader.GetString(2),
        //                PhoneNumber = reader.IsDBNull(3) ? null : reader.GetString(3),
        //                Comment = reader.IsDBNull(4) ? null : reader.GetString(4),
        //                CreatedAt = reader.GetDateTime(5)
        //            };
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error fetching contact with Id {Id}", id);
        //    }

        //    return null;
        //}
    }
}