using DtrCheck.Core;
using DtrCheck.Core.Cql;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

var dataDirectory = Path.Combine(builder.Environment.ContentRootPath, "data");
var cqlDirectory = new DirectoryInfo(Path.Combine(dataDirectory, "cql"));

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.SnakeCaseLower));
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// CqlEngine compiles all CQL libraries once at startup (a real, several-second
// compile step -- CQL to ELM to Roslyn-compiled C#) and is reused for every request.
builder.Services.AddSingleton(sp =>
    new CqlEngine(cqlDirectory, sp.GetRequiredService<ILoggerFactory>()));
builder.Services.AddSingleton<Matcher>();
builder.Services.AddSingleton(new DataPaths(dataDirectory));

var app = builder.Build();

app.UseCors();
app.MapControllers();

// Warm the CQL compiler at startup rather than on the first request.
_ = app.Services.GetRequiredService<CqlEngine>();

app.Run();

public record DataPaths(string DataDirectory)
{
    public string PatientPath => Path.Combine(DataDirectory, "patients", "synthetic_patient_osa.json");
    public string QuestionnairePath => Path.Combine(DataDirectory, "questionnaires", "respiratory_assist_device_questionnaire.json");
    public string RulesPath => Path.Combine(DataDirectory, "rules", "respiratory_assist_device_rules.json");
    public string ValuesetsPath => Path.Combine(DataDirectory, "cql", "valuesets.json");
}
