using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShoppingListServer.Logic
{
    public class HtmlPageFactory
    {
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
    }
}
