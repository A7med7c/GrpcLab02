using Grpc.Net.Client.Configuration;
using NexusVault.Abstractions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "This_is_a_very_secret_key_used_for_demo_123456"))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();

builder.Services.AddGrpcClient<RecordCatalogService.RecordCatalogServiceClient>(o =>
{
    o.Address = new Uri(builder.Configuration["GrpcEndpoints:RecordServiceUrl"] ?? "http://localhost:5240");
}).ConfigureChannel(o =>
{
    o.UnsafeUseInsecureChannelCallCredentials = true;
    o.ServiceConfig = new ServiceConfig
    {
        MethodConfigs =
        {
           new MethodConfig {
            Names = { MethodName.Default },
            RetryPolicy = new RetryPolicy
            {
                MaxAttempts = 5,
                InitialBackoff = TimeSpan.FromSeconds(1),
                MaxBackoff = TimeSpan.FromSeconds(5),
                BackoffMultiplier = 2,
                RetryableStatusCodes = { Grpc.Core.StatusCode.Unavailable, Grpc.Core.StatusCode.DeadlineExceeded }
            },
            }
        }
    };
})
.AddCallCredentials((context, metadata, serviceProvider) =>
{
    var httpContext = serviceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext;

    if (httpContext != null && httpContext.Request.Headers.TryGetValue("Authorization", out var authHeader))
    {
        var token = authHeader.FirstOrDefault();
        if (!string.IsNullOrEmpty(token))
        {
            metadata.Add("Authorization", token);
        }
    }
    return Task.CompletedTask;
});

builder.Services.AddGrpcClient<DeviceRegistryService.DeviceRegistryServiceClient>(o =>
{
    o.Address = new Uri(builder.Configuration["GrpcEndpoints:DeviceServiceUrl"] ?? "http://localhost:5062");
}).EnableCallContextPropagation();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
