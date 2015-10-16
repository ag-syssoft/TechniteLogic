using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Logging;
using System.Diagnostics;

namespace TechniteLogic
{
	class Program
	{
		public static volatile bool ShuttingDown = false;


		public static void ShutDown(int returnCode)
		{
			ShuttingDown = true;
			Logging.Out.EndService();
			//Console.ReadKey();
			Environment.Exit(returnCode);
		}


		static void onCancel(object sender, ConsoleCancelEventArgs e)
		{
			ShutDown(0);
		}



		static void Main(string[] args)
		{
			Console.CancelKeyPress += new ConsoleCancelEventHandler(onCancel);
			//Logging.Out.StartService();	//if multiple instances are started, the binding to a fixed ip is bad, so no service


			if (args.Length < 1)
			{
				Console.Error.WriteLine("Missing parameters. Use 'exe [port]'. Exiting");
				ShutDown(-1);
				return;
			}

			ushort serverPort;

			if (!ushort.TryParse(args[0], out serverPort))
			{
				Console.Error.WriteLine("Unable to parse parameter '"+args[0]+"' to port number. Exiting");
				ShutDown(-1);
				return;
			}

			Interface.Register();


			System.Threading.Thread.Sleep(100);
			for (;;)
			{
				Client client = new Client();

				Out.Log(Logging.Significance.Important, "Connecting to server on port " + serverPort);
				client.Connect(serverPort);

				Grid.FlushAllData();
				Technite.FlushAllData();
				Objects.FlushAllData();

				System.Threading.Thread.Sleep(2000);
			}

			//ShutDown(0);
		}




	}
}
