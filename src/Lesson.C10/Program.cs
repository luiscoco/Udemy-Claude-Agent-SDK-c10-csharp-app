using Lesson.C10.Components;
using Lesson.C10.Lessons;
using Lesson.C10.Services;

// .env and the run journal live in the app root (the folder holding Lesson.C10.slnx), like the Node app.
// `dotnet run` may switch the working directory to the project folder, so search upwards for it.
var appRoot = Directory.GetCurrentDirectory();
for (var dir = new DirectoryInfo(appRoot); dir is not null; dir = dir.Parent)
    if (File.Exists(Path.Combine(dir.FullName, "Lesson.C10.slnx"))) { appRoot = dir.FullName; break; }
var envPath = Path.Combine(appRoot, ".env");
DotEnv.Load(envPath);

if (args.FirstOrDefault() == "setup")
{
    await ResourceSetup.Run(envPath);
    return;
}

var builder = WebApplication.CreateBuilder(args);
ILesson lesson = new C10Lesson();
var port = int.TryParse(Environment.GetEnvironmentVariable("PORT"), out var configuredPort) ? configuredPort : 3000 + lesson.Number;
// Loopback only: credentials and resource IDs stay on this machine.
builder.WebHost.UseUrls($"http://127.0.0.1:{port}");

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSingleton(lesson);
builder.Services.AddSingleton(ManagedAgentSettings.FromEnvironment());
builder.Services.AddSingleton<LiveCommentary>();
builder.Services.AddSingleton<LiveQueue>();
builder.Services.AddSingleton<ExperimentGate>();
builder.Services.AddSingleton(new RunJournal(Path.Combine(appRoot, ".lab-data")));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Lifetime.ApplicationStarted.Register(() => Console.WriteLine($"Lesson C{lesson.Number:D2} ready: http://127.0.0.1:{port}"));
app.Run();
