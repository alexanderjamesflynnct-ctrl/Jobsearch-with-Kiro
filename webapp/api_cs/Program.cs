using System.Text;
using System.IO;
using System.Text.Json;
using kuraiaepiai.Source;
using JobSearchAPI.Data;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Swagger;

var builder = WebApplication.CreateBuilder(args);

// ── 1. SERVICES ──────────────────────────────────────────────────────────────

builder.Services.AddControllers(); // This is the most important line for the refactor
builder.Services.AddEndpointsApiExplorer();

// .NET 10 Native OpenAPI 
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
    
    options.AddPolicy("KuraiaepiaiPolicy", p => 
        p.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod());
});

// Register our new Database Service
builder.Services.AddSingleton<JobSearchDatabase>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

// ── 2. DATABASE INITIALIZATION ───────────────────────────────────────────────

var db = app.Services.GetRequiredService<JobSearchDatabase>();
db.Initialize();

// ── 3. PIPELINE ──────────────────────────────────────────────────────────────

app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/openapi/v1.json", "Job Search API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();

// This replaces all the MapJobsEndpoints, MapKanbanEndpoints, etc.
// It automatically finds all the classes in the /Controllers folder.
app.MapControllers(); 


// <clearapi-start>
if (app.Environment.IsDevelopment())
{
    app.UseCors("KuraiaepiaiPolicy");
    app.MapGet("/clearapi/push", async (HttpContext context) => {
        try {
            string jsonContent = "";
            var swaggerProvider = context.RequestServices.GetService<ISwaggerProvider>();
            if (swaggerProvider != null) {
                var doc = swaggerProvider.GetSwagger("v1", null, "/");
                doc.Servers = new List<OpenApiServer> { new OpenApiServer { Url = $"{context.Request.Scheme}://{context.Request.Host}" } };
                using var sw = new StringWriter();
                doc.SerializeAsV3(new OpenApiJsonWriter(sw));
                jsonContent = sw.ToString();
            } else {
                using var client = new HttpClient();
                jsonContent = await client.GetStringAsync($"{context.Request.Scheme}://{context.Request.Host}/openapi/v1.json");
            }
            await File.WriteAllTextAsync("swagger.json", jsonContent, Encoding.UTF8);
            var report = await (new KuraiaepiaiReporter()).GenerateReport(Directory.GetCurrentDirectory(), jsonContent);
            using var client2 = new HttpClient();
            var response = await client2.PostAsJsonAsync("http://localhost:8000/api/collect", report);
            return response.IsSuccessStatusCode ? Results.Ok("Synced!") : Results.BadRequest("Sync failed.");
        } catch (Exception ex) { return Results.Problem(ex.Message); }
    });
}
// <clearapi-end>
app.Run("http://localhost:5300");