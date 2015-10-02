using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logging
{
	public enum Significance
	{
		Low,
		Common,
		Unusual,
		Important,
		ClientFatal,
		ProgramFatal
	}

	public static class Out
	{


		static Logger logger;



		private static object logLock = new object();

		public static void Log(Significance significance, String message)
		{
			Log("", "", "", significance, message);

		}

		public static void Log(string clientName, string accountID, string accountName, Significance significance, String message)
		{
			string printLine, fileLine;
			fileLine = DateTime.Now.ToString();
			fileLine += "\t" + significance;

			fileLine += "\t" + clientName;

			fileLine += "\t" + accountID + "\t" + accountName;

			{
				fileLine += "\t" + "\t" + "\t";
				printLine = System.String.Format("[{1}]<{0}> {2}({3}): {4}", significance, DateTime.Now.ToShortTimeString(), clientName, accountName, message);
			}

			fileLine += "\t" + message;

			if (logger != null)
				logger.SendEvent(fileLine);

			//if (significance >= Significance.Unusual)
			{
				lock (logLock)
					System.IO.File.AppendAllText("execution.log", fileLine + "\n");
				Console.WriteLine(printLine);
			}
		}

		public static void StartService()
		{
			logger = new Logger();
			logger.Start();
		}

		public static void EndService()
		{
			ReportProvider.End();
			foreach (ReportProvider p in ReportProvider.All)
			{
				p.Close();
			}

		}
	}
}
