using System.Text.Json.Serialization;

namespace Darby.BackendApi.Models
{
	// Request DTOs from Client
	public record CreateUserRequest(string Username, string Email, string FullName, string Password, string Role);
	public record UpdateUserRequest(string Username, string FullName, string Role);

	// Cosmos DB Data Document
	public class UserDocument
	{
		[JsonPropertyName("id")]
		public string Id { get; set; } = null!;

		[JsonPropertyName("username")]
		public string Username { get; set; } = null!;

		[JsonPropertyName("email")] // This is your partition key
		public string Email { get; set; } = null!;

		[JsonPropertyName("fullName")]
		public string FullName { get; set; } = null!;

		[JsonPropertyName("passwordHash")]
		public string PasswordHash { get; set; } = null!;

		[JsonPropertyName("role")]
		public string Role { get; set; } = null!;

		[JsonPropertyName("createdAt")]
		public string CreatedAt { get; set; } = null!;

		[JsonPropertyName("lastUpdated")]
		public string LastUpdated { get; set; } = null!;
	}
}
