using System.Net;
using System.Net.Mail;

namespace Project.Services
{
    // Thin wrapper around System.Net.Mail.SmtpClient - no new NuGet packages,
    // configured entirely from the "Smtp" section of appsettings.json.

    public class EmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public void SendEmail(string toEmail, string subject, string htmlBody, byte[]? attachment = null, string? attachmentName = null)
        {
            var host = _config["Smtp:Host"];
            var port = int.Parse(_config["Smtp:Port"] ?? "587");
            var username = _config["Smtp:Username"];
            var password = _config["Smtp:Password"];
            var fromEmail = _config["Smtp:FromEmail"] ?? username;
            var fromName = _config["Smtp:FromName"] ?? "HemaFlow";

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException(
                    "SMTP is not configured. Fill in the \"Smtp\" section of appsettings.json (Host/Username/Password) first.");
            }

            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(username, password),
                EnableSsl = true,
                Timeout = 20000   // ms - fail fast instead of hanging the page
            };

            using var message = new MailMessage
            {
                From = new MailAddress(fromEmail!, fromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);
            if (attachment != null && !string.IsNullOrWhiteSpace(attachmentName))
                message.Attachments.Add(new Attachment(new MemoryStream(attachment), attachmentName, "application/pdf"));

            client.Send(message);
        }
    }
}
