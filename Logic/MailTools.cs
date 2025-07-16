using System.Net.Mail;

namespace ShoppingListServer
{
    public class Mail_Tools
    {
        public Mail_Tools()
        {

        }

        /// <summary>
        /// Checks if Email Address is valid and wanted.
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
        public bool Is_Valid_Email(string email)
        {
            MailAddress mail_address;

            // Check if valid e-mail address
            try
            {
                mail_address = new System.Net.Mail.MailAddress(email);
            }
            catch
            {
                return false;
            }

            // Basic check if is Trashmail
            if(mail_address.Host.Contains("trash") ||
                mail_address.Host.Contains("muell") ||
                mail_address.Host.Contains("spam") ||
                mail_address.Host == "byom.de")
            {
                return false;
            }

            return true;
        }

    }
}
