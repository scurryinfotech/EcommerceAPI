using EcommerceAPI.Repositories;
using EcommerceAPI.Repository.Interface;
using EcommerceAPI.Repository.Service;
using EcommerceAPI.Services;
using EcommerceService.Repository.Interface;
using EcommerceService.Repository.Service;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

builder.Services.AddHttpClient();
builder.Services.AddHttpClient<IMuzztechService, MuzztechService>();
builder.Services.AddHttpClient<IOtpService, MuzztechOtpService>();
builder.Services.AddScoped<IOtpService, MuzztechOtpService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<IContactRepository, ContactRepository>();
builder.Services.AddScoped<IWebsiteSettingsRepository, WebsiteSettingsRepository>();
builder.Services.AddScoped<IBannerRepository, BannerRepository>();
builder.Services.AddScoped<IBrandRepository, BrandRepository>();
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IHomeRepository, HomeRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();

// ADD THIS LINE
builder.Services.AddScoped<CategoryRepository>();

builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();

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

EcommerceAPI.Repository.DatabaseInitializer.InitializeDatabase(builder.Configuration);

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseAuthorization();

app.MapControllers();

app.Run();