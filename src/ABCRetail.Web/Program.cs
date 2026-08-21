// ABC Retail - Azure Storage Web Application
// Author: Genius Mhirizhonga
// Module: CLDV7112 - Cloud Development B

using System.Globalization;
using ABCRetail.Web.Models;
using ABCRetail.Web.Services;

// A number input always posts its value in invariant form, so the binding pipeline has to
// read it that way. Left to the host's own locale this breaks the moment the application
// runs somewhere that separates decimals with a comma, which is exactly the difference
// between a South African workstation and a Linux App Service instance. Pinning the
// culture makes the two environments behave identically; amounts are formatted for
// display through Money instead.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);

// Locally this resolves from user secrets, which live outside the repository. In Azure
// it resolves from the App Service application setting ConnectionStrings__AzureStorage,
// since the configuration provider maps a double underscore onto a colon. The key is
// therefore never committed in either environment.
var connectionString = builder.Configuration.GetConnectionString("AzureStorage");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "No storage connection string found. Set ConnectionStrings:AzureStorage in user secrets " +
        "for local runs, or the ConnectionStrings__AzureStorage application setting in Azure.");
}

var blobContainer = builder.Configuration["AzureStorage:BlobContainer"] ?? "product-images";
var fileShare = builder.Configuration["AzureStorage:FileShare"] ?? "application-logs";

builder.Services.AddControllersWithViews();

builder.Services.AddSingleton<ITableStorageService<CustomerProfile>>(sp =>
    new TableStorageService<CustomerProfile>(
        connectionString,
        "CustomerProfiles",
        sp.GetRequiredService<ILogger<TableStorageService<CustomerProfile>>>()));

builder.Services.AddSingleton<ITableStorageService<Product>>(sp =>
    new TableStorageService<Product>(
        connectionString,
        "Products",
        sp.GetRequiredService<ILogger<TableStorageService<Product>>>()));

builder.Services.AddSingleton<ITableStorageService<Order>>(sp =>
    new TableStorageService<Order>(
        connectionString,
        "Orders",
        sp.GetRequiredService<ILogger<TableStorageService<Order>>>()));

builder.Services.AddSingleton<IBlobStorageService>(sp =>
    new BlobStorageService(
        connectionString,
        blobContainer,
        sp.GetRequiredService<ILogger<BlobStorageService>>()));

builder.Services.AddSingleton<IQueueStorageService>(sp =>
    new QueueStorageService(
        connectionString,
        sp.GetRequiredService<ILogger<QueueStorageService>>()));

builder.Services.AddSingleton<IFileShareService>(sp =>
    new FileShareService(
        connectionString,
        fileShare,
        sp.GetRequiredService<ILogger<FileShareService>>()));

// Pairs the queue write with the log write so no caller can do one and forget the other.
builder.Services.AddSingleton<IActivityRecorder, ActivityRecorder>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
