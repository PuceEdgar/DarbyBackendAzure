using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

//var builder = FunctionsApplication.CreateBuilder(args);

//builder.ConfigureFunctionsWebApplication();

//if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")))
//{
//	builder.Services.AddOpenTelemetry()
//		.UseFunctionsWorkerDefaults()
//		.UseAzureMonitorExporter();
//}

//builder.Build().Run();

var host = new HostBuilder()
	.ConfigureFunctionsWebApplication()
	.ConfigureServices(services =>
	{
		services.AddSingleton(sp =>
		{
			string connectionString = Environment.GetEnvironmentVariable("CosmosDBConnectionString")
				?? throw new InvalidOperationException("CosmosDBConnectionString is missing from configuration.");

			return new CosmosClient(connectionString, new CosmosClientOptions
			{
				SerializerOptions = new CosmosSerializationOptions
				{
					PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
				}
			});
		});
	})
	.Build();

host.Run();