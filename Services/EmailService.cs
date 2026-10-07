using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace RiderIntercom.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly ILogger<EmailService> _logger;

        public EmailService(
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _httpClient = httpClientFactory.CreateClient("Brevo");
            _logger = logger;
        }

        public async Task SendPasswordResetEmailAsync(
            string recipientEmail,
            string recipientName,
            string resetUrl)
        {
            var apiKey = _configuration["Brevo:ApiKey"];
            var fromEmail = _configuration["Brevo:FromEmail"];
            var fromName = _configuration["Brevo:FromName"] ?? "Rider Intercom";

            if (string.IsNullOrWhiteSpace(apiKey) ||
                string.IsNullOrWhiteSpace(fromEmail))
            {
                throw new InvalidOperationException(
                    "Brevo email service is not configured.");
            }

            var htmlContent = $@"
<!DOCTYPE html>
<html>
<body style=""margin:0;background:#171a1d;color:#f2ead9;font-family:Arial,sans-serif;padding:32px;"">
  <div style=""max-width:560px;margin:auto;background:#202428;border:1px solid #3a3f43;padding:32px;"">
    <h1 style=""margin-top:0;color:#ffb020;"">Rider Intercom</h1>
    <p>Hi {WebUtility.HtmlEncode(recipientName)},</p>
    <p>We received a request to reset your Rider Intercom password.</p>
    <p>
      <a href=""{WebUtility.HtmlEncode(resetUrl)}""
         style=""display:inline-block;background:#ffb020;color:#171a1d;text-decoration:none;padding:12px 20px;font-weight:bold;"">
        Reset Password
      </a>
    </p>
    <p>This link expires in 30 minutes and can only be used once.</p>
    <p>If you did not request this, you can safely ignore this email.</p>
  </div>
</body>
</html>";

            var payload = new
            {
                sender = new
                {
                    email = fromEmail,
                    name = fromName
                },
                to = new[]
                {
                    new
                    {
                        email = recipientEmail,
                        name = recipientName
                    }
                },
                subject = "Reset your Rider Intercom password",
                htmlContent
            };

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "v3/smtp/email");

            request.Headers.Add("api-key", apiKey);
            request.Headers.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            request.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            try
            {
                using var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();

                    _logger.LogError(
                        "Brevo email request failed with status {StatusCode}. Response: {ResponseBody}",
                        (int)response.StatusCode,
                        responseBody);

                    throw new InvalidOperationException(
                        "Unable to send the password reset email.");
                }

                _logger.LogInformation(
                    "Password reset email sent successfully to {RecipientEmail}.",
                    recipientEmail);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Brevo email request timed out.");

                throw new InvalidOperationException(
                    "The email service timed out. Please try again later.");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Could not reach Brevo email service.");

                throw new InvalidOperationException(
                    "The email service is currently unavailable. Please try again later.");
            }
        }
    }
}
