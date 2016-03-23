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

		enum ArgMode
		{
			Idle,
			ID,
			Location,
			ParentID,
			URL,
			Secret,
			InitMessage,
		}


		static void Main(string[] args)
		{
			Console.CancelKeyPress += new ConsoleCancelEventHandler(onCancel);

			ArgMode mode = ArgMode.Idle;


			string initMessage = "";

			foreach (var arg in args)
			{
				switch (mode)
				{
					case ArgMode.Idle:
						if (arg.StartsWith("--"))
						{
							string attrib = arg.Substring(2);
							switch (attrib)
							{
								case "id":
									mode = ArgMode.ID;
									break;
								case "at":
									mode = ArgMode.Location;
									break;
								case "parent":
									mode = ArgMode.ParentID;
									break;
								case "url":
									mode = ArgMode.URL;
									break;
								case "secret":
									mode = ArgMode.Secret;
									break;
								default:
									Console.Error.WriteLine("Unexpected attributed " + arg);
									break;
							}
						}
						else
						{
							mode = ArgMode.InitMessage;
							initMessage = arg;
						}
					break;
					case ArgMode.InitMessage:
						initMessage += ' ' + arg;
						break;




				}

			}


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
