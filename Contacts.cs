using System;
using System.Collections.Generic;

namespace TechniteLogic
{
	public static class Contacts
	{
		static List<Interface.Struct.EntityContact> contacts = new List<Interface.Struct.EntityContact>();




		public static void Deprecate()
		{
			contacts.Clear();
		}

		public static void Add(Interface.Struct.EntityContact contact)
		{
			contacts.Add(contact);
		}

		public static void Tidy()
		{
			//todo

		}

		internal static void FlushAllData()
		{
			contacts.Clear();
		}
	}
}