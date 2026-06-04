
using EcommerceAPI.Repositories;
using EcommerceService.Repository.Interface;
using EcommerceService.Repository.Service;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<IContactRepository, ContactRepository>();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Ye camelCase automatically handle kar lega
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
