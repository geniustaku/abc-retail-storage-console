// ABC Retail - Azure Storage Web Application
// Author: Genius Mhirizhonga
// Module: CLDV7112 - Cloud Development B

using System.ComponentModel.DataAnnotations;
using Azure;
using Azure.Data.Tables;

namespace ABCRetail.Web.Models;

/// <summary>
/// A customer record held in the CustomerProfiles table in Azure Table Storage.
/// </summary>
/// <remarks>
/// Every customer shares one partition. At ABC Retail's profile volumes the whole
/// table fits comfortably in a single partition, and keeping it that way means any
/// set of customers can still be written in one entity group transaction. Were the
/// customer base to grow past a single partition server, region would be the natural
/// partition key, since fulfilment queries are regional.
/// </remarks>
public class CustomerProfile : ITableEntity
{
    public const string Partition = "CUSTOMER";

    public string PartitionKey { get; set; } = Partition;

    public string RowKey { get; set; } = Guid.NewGuid().ToString();

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }

    [Required(ErrorMessage = "First name is required.")]
    [StringLength(60)]
    [Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Surname is required.")]
    [StringLength(60)]
    [Display(Name = "Surname")]
    public string Surname { get; set; } = string.Empty;

    [Required(ErrorMessage = "An email address is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(120)]
    [Display(Name = "Email address")]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Enter a valid contact number.")]
    [StringLength(20)]
    [Display(Name = "Contact number")]
    public string PhoneNumber { get; set; } = string.Empty;

    [StringLength(160)]
    [Display(Name = "Shipping address")]
    public string ShippingAddress { get; set; } = string.Empty;

    [StringLength(60)]
    [Display(Name = "City")]
    public string City { get; set; } = string.Empty;

    [StringLength(10)]
    [Display(Name = "Postal code")]
    public string PostalCode { get; set; } = string.Empty;

    [Display(Name = "Registered")]
    public DateTimeOffset RegisteredOn { get; set; } = DateTimeOffset.UtcNow;

    public string FullName => $"{FirstName} {Surname}".Trim();

    /// <summary>Two-letter monogram used by the customer list avatars.</summary>
    public string Initials
    {
        get
        {
            var first = FirstName.Length > 0 ? FirstName[0] : ' ';
            var second = Surname.Length > 0 ? Surname[0] : ' ';
            return $"{first}{second}".Trim().ToUpperInvariant();
        }
    }
}
