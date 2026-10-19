using theseOn.PDF.Middlewares;
using theseOn.PDF.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOutputCache();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<IPdfGenerator, PuppeteerPdfGenerator>();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseOutputCache();
app.UseMiddleware<IdempotencyMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
