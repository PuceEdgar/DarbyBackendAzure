using Darby.BackendApi.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace Darby.BackendApi.Endpoints
{
	public class UserManagementEndpoints
	{
		private readonly Container _container;
		private readonly ILogger<UserManagementEndpoints> _logger;

		public UserManagementEndpoints(CosmosClient cosmosClient)
		{
			_container = cosmosClient.GetContainer("DarbyDB", "Users");
			_logger = new LoggerFactory().CreateLogger<UserManagementEndpoints>();
		}

		private static JsonSerializerOptions GetOptions()
		{
			return new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
		}

		// 1. CREATE USER
		[Function("CreateUser")]
		public async Task<HttpResponseData> Create([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "users")] HttpRequestData req, CancellationToken ct)
		{
			var body = await new StreamReader(req.Body).ReadToEndAsync(ct);
			var request = JsonSerializer.Deserialize<CreateUserRequest>(body, GetOptions());

			if (request == null || string.IsNullOrWhiteSpace(request.Email))
			{
				return await CreateTextResponse(req, HttpStatusCode.BadRequest, "Invalid payload.");
			}

			var newUser = new UserDocument
			{
				Id = request.Email,
				Username = request.Username,
				Email = request.Email, // Partition Key
				FullName = request.FullName,
				PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
				Role = request.Role,
				CreatedAt = DateTime.UtcNow.ToString("o"),
				LastUpdated = DateTime.UtcNow.ToString("o")
			};

			try
			{
				await _container.CreateItemAsync(newUser, new PartitionKey(newUser.Email), cancellationToken: ct);

				var res = req.CreateResponse(HttpStatusCode.Created);
				await res.WriteAsJsonAsync(new { id = newUser.Id });
				return res;
			}
			catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
			{
				return await CreateTextResponse(req, HttpStatusCode.Conflict, "Email already exists.");
			}
		}

		// 2. FIND USER BY EMAIL
		[Function("FindUser")]
		public async Task<HttpResponseData> Find([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "users/{email}")] HttpRequestData req, string email, CancellationToken ct)
		{
			try
			{
				// Point read using /email partition key is instantly fast and highly optimized
				ItemResponse<UserDocument> response = await _container.ReadItemAsync<UserDocument>(
					id: email, // If you map your unique constraint by email, pass email here
					partitionKey: new PartitionKey(email),
					cancellationToken: ct
				);

				var res = req.CreateResponse(HttpStatusCode.OK);
				await res.WriteAsJsonAsync(response.Resource);
				return res;
			}
			catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
			{
				return await CreateTextResponse(req, HttpStatusCode.NotFound, "User not found.");
			}
		}

		// 3. EDIT USER
		[Function("EditUser")]
		public async Task<HttpResponseData> Edit([HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "users/{email}")] HttpRequestData req, string email,	CancellationToken ct)
		{
			var body = await new StreamReader(req.Body).ReadToEndAsync(ct);
			var request = JsonSerializer.Deserialize<UpdateUserRequest>(body, GetOptions());

			try
			{
				// Read existing record first
				var existingResponse = await _container.ReadItemAsync<UserDocument>(email, new PartitionKey(email), cancellationToken: ct);
				var user = existingResponse.Resource;

				// Apply updates
				user.Username = request?.Username ?? user.Username;
				user.FullName = request?.FullName ?? user.FullName;
				user.Role = request?.Role ?? user.Role;
				user.LastUpdated = DateTime.UtcNow.ToString("o");

				// Replace in Cosmos DB
				await _container.ReplaceItemAsync(user, user.Email, new PartitionKey(user.Email), cancellationToken: ct);

				return await CreateTextResponse(req, HttpStatusCode.OK, "User updated successfully.");
			}
			catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
			{
				return await CreateTextResponse(req, HttpStatusCode.NotFound, "User not found.");
			}
		}

		// 4. DELETE USER
		[Function("DeleteUser")]
		public async Task<HttpResponseData> Delete([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "users/{email}")] HttpRequestData req, string email, CancellationToken ct)
		{
			try
			{
				await _container.DeleteItemAsync<UserDocument>(
					id: email,
					partitionKey: new PartitionKey(email),
					cancellationToken: ct
				);
				return await CreateTextResponse(req, HttpStatusCode.OK, "User deleted.");
			}
			catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
			{
				return await CreateTextResponse(req, HttpStatusCode.NotFound, "User not found.");
			}
		}

		// Helper method to keep text responses short and readable
		private static async Task<HttpResponseData> CreateTextResponse(HttpRequestData req, HttpStatusCode code, string message)
		{
			var response = req.CreateResponse(code);
			await response.WriteStringAsync(message);
			return response;
		}
	}
}
