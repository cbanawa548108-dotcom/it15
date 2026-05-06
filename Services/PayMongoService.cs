using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CRLFruitstandESS.Services
{
    public class PayMongoPaymentIntent
    {
        public string Id          { get; set; } = string.Empty;
        public string ClientKey   { get; set; } = string.Empty;
        public string Status      { get; set; } = string.Empty;
        public decimal Amount     { get; set; }
        public string Currency    { get; set; } = "PHP";
    }

    public class PayMongoSource
    {
        public string Id          { get; set; } = string.Empty;
        public string Type        { get; set; } = string.Empty; // gcash | paymaya
        public string Status      { get; set; } = string.Empty;
        public string RedirectUrl { get; set; } = string.Empty;
        public string FailedUrl   { get; set; } = string.Empty;
        public string CheckoutUrl { get; set; } = string.Empty;
    }

    public class PayMongoCheckoutSession
    {
        public string Id          { get; set; } = string.Empty;
        public string CheckoutUrl { get; set; } = string.Empty;
        public string Status      { get; set; } = string.Empty;
    }

    public interface IPayMongoService
    {
        Task<PayMongoPaymentIntent> CreatePaymentIntentAsync(decimal amount, string description, string paymentMethod = "card");
        Task<PayMongoSource> CreateSourceAsync(decimal amount, string type, string successUrl, string failedUrl, string description);
        Task<PayMongoCheckoutSession> CreateCheckoutSessionAsync(decimal amount, string[] paymentMethods, string successUrl, string cancelUrl, string description, List<(string name, int qty, decimal unitAmount)> lineItems);
        Task<PayMongoPaymentIntent?> GetPaymentIntentAsync(string intentId);
        Task<bool> AttachPaymentMethodAsync(string intentId, string paymentMethodId, string clientKey);
        bool VerifyWebhookSignature(string payload, string signature);
    }

    public class PayMongoService : IPayMongoService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;
        private readonly ILogger<PayMongoService> _logger;

        public PayMongoService(IHttpClientFactory factory, IConfiguration config, ILogger<PayMongoService> logger)
        {
            _http   = factory.CreateClient("PayMongo");
            _config = config;
            _logger = logger;
            // Auth header and BaseAddress are configured at registration in Program.cs
        }

        // ── Create a Payment Intent (for card and Maya payments)
        public async Task<PayMongoPaymentIntent> CreatePaymentIntentAsync(decimal amount, string description, string paymentMethod = "card")
        {
            // PayMongo payment_method_allowed values: "card", "paymaya", "dob", "dob_ubp", "brankas_bdo", etc.
            var body = new
            {
                data = new
                {
                    attributes = new
                    {
                        amount                 = (int)(amount * 100),
                        payment_method_allowed = new[] { paymentMethod },
                        payment_method_options = paymentMethod == "card"
                            ? (object)new { card = new { request_three_d_secure = "any" } }
                            : new { },
                        currency     = "PHP",
                        capture_type = "automatic",
                        description  = description
                    }
                }
            };

            var json     = JsonSerializer.Serialize(body);
            var content  = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _http.PostAsync("payment_intents", content);
            var raw      = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("PayMongo CreatePaymentIntent failed: {Raw}", raw);
                throw new Exception($"PayMongo error: {raw}");
            }

            using var doc = JsonDocument.Parse(raw);
            var attr = doc.RootElement.GetProperty("data").GetProperty("attributes");

            return new PayMongoPaymentIntent
            {
                Id        = doc.RootElement.GetProperty("data").GetProperty("id").GetString() ?? "",
                ClientKey = attr.TryGetProperty("client_key", out var ck) ? ck.GetString() ?? "" : "",
                Status    = attr.GetProperty("status").GetString() ?? "",
                Amount    = attr.GetProperty("amount").GetDecimal() / 100,
                Currency  = "PHP"
            };
        }

        // ── Create a Source (for GCash / Maya — redirects customer)
        public async Task<PayMongoSource> CreateSourceAsync(decimal amount, string type, string successUrl, string failedUrl, string description)
        {
            var body = new
            {
                data = new
                {
                    attributes = new
                    {
                        amount      = (int)(amount * 100),
                        currency    = "PHP",
                        type        = type,          // "gcash" or "paymaya"
                        description = description,
                        redirect    = new { success = successUrl, failed = failedUrl }
                    }
                }
            };

            var json     = JsonSerializer.Serialize(body);
            var content  = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _http.PostAsync("sources", content);
            var raw      = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("PayMongo CreateSource failed: {Raw}", raw);
                throw new Exception($"PayMongo error: {raw}");
            }

            using var doc = JsonDocument.Parse(raw);
            var data = doc.RootElement.GetProperty("data");
            var attr = data.GetProperty("attributes");
            var redirect = attr.GetProperty("redirect");

            return new PayMongoSource
            {
                Id          = data.GetProperty("id").GetString() ?? "",
                Type        = type,
                Status      = attr.GetProperty("status").GetString() ?? "",
                CheckoutUrl = redirect.GetProperty("checkout_url").GetString() ?? "",
                RedirectUrl = redirect.TryGetProperty("success", out var s) ? s.GetString() ?? "" : "",
                FailedUrl   = redirect.TryGetProperty("failed",  out var f) ? f.GetString() ?? "" : ""
            };
        }

        // ── Get Payment Intent status
        public async Task<PayMongoPaymentIntent?> GetPaymentIntentAsync(string intentId)
        {
            var response = await _http.GetAsync($"payment_intents/{intentId}");
            if (!response.IsSuccessStatusCode) return null;

            var raw = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(raw);
            var attr = doc.RootElement.GetProperty("data").GetProperty("attributes");

            return new PayMongoPaymentIntent
            {
                Id     = intentId,
                Status = attr.GetProperty("status").GetString() ?? "",
                Amount = attr.GetProperty("amount").GetDecimal() / 100
            };
        }

        // ── Attach payment method to intent
        public async Task<bool> AttachPaymentMethodAsync(string intentId, string paymentMethodId, string clientKey)
        {
            var body = new
            {
                data = new
                {
                    attributes = new
                    {
                        payment_method = paymentMethodId,
                        client_key     = clientKey
                    }
                }
            };

            var json     = JsonSerializer.Serialize(body);
            var content  = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _http.PostAsync($"payment_intents/{intentId}/attach", content);
            return response.IsSuccessStatusCode;
        }

        // ── Create a Checkout Session (supports GCash, Maya, card, GrabPay, etc.)
        // This is the recommended approach — returns a real checkout.paymongo.com URL.
        public async Task<PayMongoCheckoutSession> CreateCheckoutSessionAsync(
            decimal amount,
            string[] paymentMethods,
            string successUrl,
            string cancelUrl,
            string description,
            List<(string name, int qty, decimal unitAmount)> lineItems)
        {
            // Build line_items array — PayMongo requires at least one
            var items = lineItems.Select(li => new
            {
                currency    = "PHP",
                amount      = (int)(li.unitAmount * 100),
                name        = li.name,
                quantity    = li.qty,
                description = li.name
            }).ToArray();

            var body = new
            {
                data = new
                {
                    attributes = new
                    {
                        billing                = new { },
                        send_email_receipt     = false,
                        show_description       = true,
                        show_line_items        = true,
                        line_items             = items,
                        payment_method_types   = paymentMethods,
                        description            = description,
                        success_url            = successUrl,
                        cancel_url             = cancelUrl,
                        statement_descriptor   = "CRL Fruitstand"
                    }
                }
            };

            var json     = JsonSerializer.Serialize(body);
            var content  = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _http.PostAsync("checkout_sessions", content);
            var raw      = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("PayMongo CreateCheckoutSession failed: {Raw}", raw);
                throw new Exception($"PayMongo error: {raw}");
            }

            using var doc = JsonDocument.Parse(raw);
            var data = doc.RootElement.GetProperty("data");
            var attr = data.GetProperty("attributes");

            return new PayMongoCheckoutSession
            {
                Id          = data.GetProperty("id").GetString() ?? "",
                CheckoutUrl = attr.GetProperty("checkout_url").GetString() ?? "",
                Status      = attr.GetProperty("status").GetString() ?? ""
            };
        }

        // ── Verify webhook signature (HMAC-SHA256)
        public bool VerifyWebhookSignature(string payload, string signature)
        {
            var secret = _config["PayMongo:WebhookSecret"] ?? "";
            if (string.IsNullOrEmpty(secret)) return true; // skip in dev

            using var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var computed = Convert.ToHexString(hash).ToLower();
            return computed == signature?.ToLower();
        }
    }
}
