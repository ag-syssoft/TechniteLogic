using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Logging;

namespace TechniteLogic
{
	public static class Session
	{
		public static UInt32 roundNumber = 0;
		public static UInt32 techniteSubRoundNumber = 0;

		public static string FactionUUID { get; private set; }
		public static byte TechniteGridID { get; private set; }

		public static void Begin(Interface.Struct.BeginSession session)
		{
			roundNumber = session.roundNumber;
			techniteSubRoundNumber = session.techniteSubRoundNumber;
			FactionUUID = session.factionUUID;
			TechniteGridID = session.techniteGridID;
			Out.Log(Significance.Important, "Session started: "+FactionUUID+" in round "+roundNumber+"/"+techniteSubRoundNumber);
		}
	}
}
