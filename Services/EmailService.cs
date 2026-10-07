using System.Net;
using System.Net.Mail;

namespace RiderIntercom.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendPasswordResetEmailAsync(string recipientEmail, string recipientName, string resetUrl)
        {
            var host = _configuration["Email:SmtpHost"];
            var portValue = _configuration["Email:SmtpPort"];
            var username = _configuration["Email:Username"];
            var password = _configuration["Email:Password"];
            var fromEmail = _configuration["Email:FromEmail"];
            var fromName = _configuration["Email:FromName"] ?? "Rider Intercom";

            if (string.IsNullOrWhiteSpace(host) ||
                !int.TryParse(portValue, out var port) ||
                string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(fromEmail))
            {
                throw new InvalidOperationException("Password reset email is not configured.");
            }

            using var message = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName),
                Subject = "Reset your Rider Intercom password",
                IsBodyHtml = true,
                Body = $@"
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
</html>"
            };

            message.To.Add(new MailAddress(recipientEmail));

            using var client = new SmtpClient(host, port)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(username, password)
            };

            await client.SendMailAsync(message);
        }
    }
}
