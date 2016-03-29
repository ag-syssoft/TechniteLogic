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

		static string serverURL = "";


		public static byte[] StringToByteArrayFastest(string hex)
		{
			if (hex.Length % 2 == 1)
				throw new Exception("The binary key cannot have an odd number of digits");

			byte[] arr = new byte[hex.Length >> 1];

			for (int i = 0; i < arr.Length; ++i)
			{
				arr[i] = (byte)((GetHexVal(hex[i << 1]) << 4) | (GetHexVal(hex[(i << 1) + 1])));
			}

			return arr;
		}

		public static int GetHexVal(char hex)
		{
			
			if (hex >= 'a' && hex <= 'f')
				return 10 + (hex - 'a');
			if (hex >= 'A' && hex <= 'F')
				return 10 + (hex - 'A');
			if (hex >= '0' && hex <= '9')
				return (hex - '0');
			return 0;
			int val = (int)hex;
			//For uppercase A-F letters:
			return val - (val < 58 ? 48 : 55);
			//For lowercase a-f letters:
			//return val - (val < 58 ? 48 : 87);
			//Or the two combined, but a bit slower:
			//return val - (val < 58 ? 48 : (val < 97 ? 55 : 87));
		}

		static void Main(string[] args)
		{
			Console.CancelKeyPress += new ConsoleCancelEventHandler(onCancel);

			ArgMode mode = ArgMode.Idle;


			Session.initMessage = "";

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
							Session.initMessage = arg;
						}
						continue;
					case ArgMode.ID:
						Technite.Me.ID = new Uuid(StringToByteArrayFastest(arg));
						Console.WriteLine("Updated my id to " + Technite.Me.ID);
						break;
					case ArgMode.Location:
						{
							int dotAt = arg.IndexOf('.');
							if (dotAt == -1)
							{
								Console.Error.WriteLine("Malformatted technite location: " + arg);
								break;
							}
							
							try
							{
								uint stackID = uint.Parse(arg.Substring(0, dotAt));
								int layer = int.Parse(arg.Substring(dotAt + 1));
								Technite.Me.Location = new Grid.CellID(stackID, layer);
								Console.WriteLine("Updated my location to " + Technite.Me.Location);
							}
							catch (Exception e)
							{
								Console.Error.WriteLine(e+" while trying to convert " + arg+" to location");
							}
						}
						break;
					case ArgMode.ParentID:
						Session.ParentTechniteID = new Guid(StringToByteArrayFastest(arg));
						break;
					case ArgMode.InitMessage:
						Session.initMessage += ' ' + arg;
						break;
					case ArgMode.URL:
						serverURL = arg;
						break;
					case ArgMode.Secret:
						Session.secret = StringToByteArrayFastest(arg);
						Debug.Assert(Session.secret.Length == 32);
						break;

				}
				mode = ArgMode.Idle;

			}

			Interface.Register();


			System.Threading.Thread.Sleep(100);
			for (;;)
			{
				Client client = new Client();
				Interface.globalClient = client;

				Out.Log(Logging.Significance.Important, "Connecting to server at " + serverURL);
				client.Connect(serverURL);

				Grid.FlushAllData();
				Technite.FlushAllData();
				Contacts.FlushAllData();

				System.Threading.Thread.Sleep(2000);
			}

			//ShutDown(0);
		}




	}
}
