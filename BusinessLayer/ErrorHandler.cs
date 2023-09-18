using System.Net.Mail;
using System.Net;
using Microsoft.Extensions.Configuration;

namespace BusinessLayer
{
    public static class ErrorHandler
    {
        public static void SendErrorEmail(IConfiguration configuration, Exception exception, string websiteUrl, string inputText)
        {
            try
            {
                // Gmail SMTP settings
                var smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential(configuration["FromEmail"], configuration["EmailPassword"]),
                    EnableSsl = true,
                    UseDefaultCredentials = false,
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(configuration["FromEmail"]),
                    Subject = "Error occurred at " + websiteUrl + " auto email responder API",
                    Body = exception.Message + "\n" + exception.StackTrace + "\n" + "Question Asked: " + inputText, // Error stack trace
                };

                mailMessage.To.Add(configuration["ReceiverEmail"]);

                smtpClient.Send(mailMessage);
            }
            catch (Exception ex)
            {
                // Handle any exceptions that occur during email sending, e.g., log them
                Console.WriteLine("Error sending email: " + ex.Message);
            }
        }
    }
}