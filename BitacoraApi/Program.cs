using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<BitacoraApi.Data.BitacoraDbConnectionFactory>();
builder.Services.AddScoped<BitacoraApi.Repositories.IBitacoraRepository, BitacoraApi.Repositories.BitacoraRepository>();

var app = builder.Build();

var evidenciasRoot = Path.Combine(app.Environment.ContentRootPath, "Evidencias");
Directory.CreateDirectory(evidenciasRoot);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(evidenciasRoot),
    RequestPath = "/evidencias"
});

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(evidenciasRoot),
    RequestPath = "/Evidencias"
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
