using System;
namespace ShoppingListServer.Models
{
    public class GoogleUser
    {
		public string Id
		{
			get;
			set;
		}

		public string Name
		{
			get;
			set;
		}

		public string GivenName
		{
			get;
			set;
		}

		public string FamilyName
		{
			get;
			set;
		}

		public string Email
		{
			get;
			set;
		}

		public Uri Picture
		{
			get;
			set;
		}
	}
}
