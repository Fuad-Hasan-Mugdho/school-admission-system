using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Endpoints;
using SchoolAdmission.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ---- configuration (all overridable through environment variables) ----
var connectionString = ConnectionStrings.Resolve(builder.Configuration);
var holdOptions = HoldOptions.FromConfiguration(builder.Configuration);

// ---- services ----
builder.Services.AddSingleton(holdOptions);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<AdmissionDbContext>(o => o.UseNpgsql(connectionString));
builder.Services.AddScoped<AdmissionService>();
builder.Services.AddHostedService<HoldExpirySweeper>();

builder.Services.Configure<JsonOptions>(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.SerializerOptions.NumberHandling = JsonNumberHandling.AllowReadingFromString;
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
});

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .AllowAnyOrigin()
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new() { Title = "School Admission API", Version = "v1" });
});

var app = builder.Build();

// ---- error handling: every failure is JSON a human can read, never a blank body ----
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var feature = context.Features.Get<IExceptionHandlerFeature>();
    var ex = feature?.Error;

    var (status, code, message) = ex switch
    {
        BadHttpRequestException bad => (bad.StatusCode, ErrorCodes.ValidationError,
            bad.InnerException is JsonException ? "Request body is not valid JSON or has wrong field types." : bad.Message),
        JsonException => (400, ErrorCodes.ValidationError, "Request body is not valid JSON or has wrong field types."),
        _ => (500, ErrorCodes.InternalError, "The server hit an unexpected error. Please try again."),
    };

    if (status == 500)
    {
        app.Logger.LogError(ex, "Unhandled exception");
    }

    context.Response.StatusCode = status;
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsJsonAsync(new ErrorResponse(code, message));
}));

app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();

app.MapAdmissionEndpoints();

// ---- schema ----
await DatabaseInitializer.MigrateWithRetryAsync(app.Services, app.Lifetime.ApplicationStopping);

app.Logger.LogInformation("Hold duration: {Seconds}s; sweep every {Sweep}s",
    holdOptions.HoldDurationSeconds, holdOptions.SweepIntervalSeconds);

app.Run();

/// <summary>Marker for WebApplicationFactory in the test project.</summary>
public partial class Program;
