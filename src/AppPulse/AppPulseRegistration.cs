using AppPulse.Enum;
using AppPulse.Middleware;
using AppPulse.Model.Response;
using AppPulse.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Net.Security;
using System.Net;
using System.Security.Authentication;
using Prometheus;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using AppPulse.Service;
using RabbitMQ.Client;

namespace AppPulse
{
    public static class AppPulseRegistration
    {
        public static IApplicationBuilder AddAppPulseBuilder(this IApplicationBuilder app)
        {
            app.UseMetricServer();

            app.UseHttpMetrics(options =>
            {
                options.AddCustomLabel("host", context => context.Request.Host.Host);
            });

            app.UseMiddleware<AppPulseMiddleware>();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHealthChecks("/readiness", new HealthCheckOptions
                {
                    Predicate = healthCheck => healthCheck.Tags.Contains("ready"),
                    ResponseWriter = WriteHealthCheckResponse
                });
                endpoints.MapHealthChecks("/liveness", new HealthCheckOptions
                {
                    Predicate = _ => false,
                    ResponseWriter = WriteHealthCheckResponse
                });
                endpoints.MapControllers();
            });

            return app;
        }

        private static Task WriteHealthCheckResponse(HttpContext httpContext, HealthReport result)
        {
            httpContext.Response.ContentType = "application/json";

            var response = new AppPulseResponse()
            {
                StatusCode = (HttpStatusCode)httpContext.Response.StatusCode,
                Status = result.Status.ToString(),
                TotalDuration = result.TotalDuration,
                Services = result.Entries.Select(x => new AppPulseServiceResponse()
                {
                    Name = x.Key,
                    Status = x.Value.Status.ToString(),
                    Description = string.Join(",", x.Value.Tags)
                })
            };

            return httpContext.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
        }
        public static IServiceCollection AddAppPulseServiceCollection(this IServiceCollection services, List<AppPulseSource> appPulseSource)
        {
            services.AddSingleton<MetricReporter>();
            var hcBuilder = services.AddHealthChecks();

            foreach (var source in appPulseSource)
            {

                switch (source.DependencyType)
                {
                    case DependencyType.MSSql:
                        hcBuilder.AddSqlServer(
                            connectionString: source.DatabaseInformation.ConnectionString,
                            name: source.Name,
                            tags: ["db", "sql", "MSSql", "ready"]);
                        break;
                    case DependencyType.PostgreSQL:
                        hcBuilder.AddNpgSql(
                            connectionString: source.DatabaseInformation.ConnectionString,
                            name: source.Name,
                            tags: ["db", "sql", "PostgreSql", "ready"]);
                        break;
                    case DependencyType.Redis:
                        hcBuilder.AddRedis(
                            redisConnectionString: $"{source.RedisCacheInformation.Host}:{source.RedisCacheInformation.Port},password={source.RedisCacheInformation.Password}",
                            name: source.Name,
                            tags: ["cache", "redis", "ready"]);
                        break;
                    case DependencyType.RabbitMQ:
                        var sslOption = new SslOption()
                        {
                            Enabled = true,
                            AcceptablePolicyErrors = SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNotAvailable,
                            Version = SslProtocols.Tls13 | SslProtocols.Tls12,
                            ServerName = source.RabbitMqInformation.Host,
                        };
                        hcBuilder.AddRabbitMQ(
                            rabbitConnectionString: $"amqp://{source.RabbitMqInformation.UserName}:{source.RabbitMqInformation.Password}@{source.RabbitMqInformation.Host}:{source.RabbitMqInformation.Port}",
                            sslOption: source.RabbitMqInformation.IsUseSslOption ? sslOption : null,
                            name: source.Name,
                            tags: ["rabbit", "rabbitMQ", "ready"]);
                        break;
                    case DependencyType.ExternalService:
                        hcBuilder.AddUrlGroup(
                            uri: new Uri(source.ExternalServiceInformation.BaseAddress + "liveness"),
                            name: source.Name,
                            tags: ["external", "service", "api", "ready"]);
                        break;
                    default:
                        break;
                }

            }

            return services;
        }
    }
}
