using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRLFruitstandESS.Models
{
    public class PaymentTransaction
    {
        public int Id { get; set; }

        public int? SaleId { get; set; }

        // ── Payment method & status ──────────────────────────────
        [Required][StringLength(50)]
        public string Method { get; set; } = string.Empty; // cash | gcash | paymaya | card

        [Required][StringLength(50)]
        public string Status { get; set; } = "pending"; // pending | paid | failed | expired

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        // ── PayMongo identifiers ─────────────────────────────────
        /// <summary>Payment Intent ID (pi_xxx) — used for card payments</summary>
        [StringLength(200)]
        public string? PayMongoIntentId { get; set; }

        /// <summary>Source ID (src_xxx) — used for GCash / Maya</summary>
        [StringLength(200)]
        public string? PayMongoSourceId { get; set; }

        /// <summary>Payment ID (pay_xxx) — the actual charge record on PayMongo</summary>
        [StringLength(200)]
        public string? PayMongoPaymentId { get; set; }

        // ── URLs & references ────────────────────────────────────
        [StringLength(500)]
        public string? CheckoutUrl { get; set; }

        /// <summary>External / bank reference number returned by PayMongo</summary>
        [StringLength(100)]
        public string? ReferenceNumber { get; set; }

        // ── Card / wallet detail (populated on success) ──────────
        /// <summary>e.g. "visa", "mastercard", "gcash", "paymaya"</summary>
        [StringLength(50)]
        public string? PaymentMethodType { get; set; }

        /// <summary>Last 4 digits of card (card payments only)</summary>
        [StringLength(4)]
        public string? CardLast4 { get; set; }

        /// <summary>Card brand: Visa, Mastercard, etc.</summary>
        [StringLength(30)]
        public string? CardBrand { get; set; }

        // ── Failure info ─────────────────────────────────────────
        [StringLength(200)]
        public string? FailureCode { get; set; }

        [StringLength(500)]
        public string? FailureMessage { get; set; }

        // ── Environment flag ─────────────────────────────────────
        /// <summary>True when processed with test/sandbox keys</summary>
        public bool IsTestMode { get; set; } = false;

        // ── Raw PayMongo response (for audit / debugging) ────────
        /// <summary>Full JSON response from PayMongo stored for audit trail</summary>
        public string? RawPayMongoResponse { get; set; }

        // ── Pending cart data (stored before redirect, retrieved on return) ──
        /// <summary>
        /// Serialised ProcessSaleViewModel JSON saved before the PayMongo redirect.
        /// Retrieved by txnId on PaymentSuccess so the cart never travels in the URL.
        /// Cleared after the sale is created.
        /// </summary>
        public string? PendingSaleDataJson { get; set; }

        // ── Audit fields ─────────────────────────────────────────
        public string ProcessedBy { get; set; } = string.Empty;
        public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;
        public DateTime? PaidAt    { get; set; }
        public DateTime? UpdatedAt { get; set; }

        [ForeignKey("SaleId")]
        public virtual Sale? Sale { get; set; }
    }
}
