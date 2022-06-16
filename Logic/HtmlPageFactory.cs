using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace ShoppingListServer.Logic
{
    public class HtmlPageFactory
    {
        public static MailMessage CreateMailMessageTemplate(string targetEMail, string htmlBody)
        {
            MailMessage message = new MailMessage("noreply@shopping-now.net", targetEMail);

            message.Subject = "ShoppingNow Registration";
            message.SubjectEncoding = System.Text.Encoding.UTF8;
            // Avoid MIME_HTML_ONLY to drop the spam score
            message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(htmlBody, Encoding.UTF8, "text/html"));
            message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString("Please use a html reader to read this E-Mail", null, "text/plain"));
            //message.BodyEncoding = System.Text.Encoding.UTF8;
            //message.IsBodyHtml = true;
            return message;
        }

        /// <summary>
        /// Creates a html page that redirects the caller to the main page of ShoppingNow.
        /// </summary>
        /// <returns></returns>
        public static string CreateRedirectToShoppingNowPage()
        {
            string redirectToMainPage =
                "<head>\n" +
                "<meta http-equiv=\"Refresh\" content=\"0; URL=https://shopping-now.net/\">\n" +
                "</head>\n";
            return redirectToMainPage;
        }

        public static string CreateHtmlHeader()
        {
            return
                "<html>\n" +
                "<head>\n" +
                "<style>\n" +
                "td {\n" +
                "    border-radius: 10px;\n" +
                "}\n" +
                "td a {\n" +
                "    padding: 16px 20px;\n" +
                "    border: 1px solid #1F83FF;\n" +
                "    border-radius: 10px;\n" +
                "    font-family: Arial, Helvetica, sans-serif;\n" +
                "    font-size: 14px;\n" +
                "    color: #ffffff; \n" +
                "    text-decoration: none;\n" +
                "    font-weight: bold;\n" +
                "    display: inline-block;  \n" +
                "}\n" +
                "</style>\n" +
                "</head>\n" +
                "<body>\n";
        }

        public static string CreateHtmlFooter()
        {
            return
                "</body>\n" +
                "</html>";
        }

        public static string CreateButton(string text, string link)
        {
            return string.Format(
                "<table width=\"100%\" cellspacing=\"0\" cellpadding=\"0\">\n" +
                "  <tr>\n" +
                "      <td>\n" +
                "          <table cellspacing=\"0\" cellpadding=\"0\">\n" +
                "              <tr>\n" +
                "                  <td class=\"button\" bgcolor=\"#1F83FF\">\n" +
                "                      <a class=\"link\" href=\"{0}\" target=\"_blank\">\n" +
                "                          {1}\n" +
                "                      </a>\n" +
                "                  </td>\n" +
                "              </tr>\n" +
                "          </table>\n" +
                "      </td>\n" +
                "  </tr>\n" +
                "</table>\n", link, text);
        }

        public static string CreateHyperlink(string text, string link)
        {
            return string.Format("<a href=\"{0}\">{1}</a>", link, text);
        }

        public static string CreateGreeting(string username)
        {
            return "Hello " + username + ",<br><br>\n";
        }

        public static string CreateNoReplyDisclaimer()
        {
            return "This mail has been automatically generated. Please do not reply to it.<br><br>\n" +
                "Sincerely,<br>\n" +
                "Your ShoppingNow Team";
        }
    }
}
