using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Logging;

namespace TechniteLogic
{
	internal class Client : Protocol.Client
	{

		public Client() : base(new TcpClient(), false)
		{
		}

		protected override void OnReadThreadStart()
		{
			Out.Log(Significance.Common, "Connected. Waiting for challenge...");
			//Interface.ready.SendTo(this,Interface.CompileProtocolString());

		}

		protected override void OnClose()
		{
			Out.Log(Significance.Important, "Connection lost to host.");
			//Program.ShutDown(0);
		}

		protected override void OnAbnormalException(Exception e)
		{
			Out.Log(Significance.ClientFatal, e.ToString());
		}

		

		public void Connect(string url)
		{
			try
			{
				int colonAt = url.IndexOf(':');
				if (colonAt < 0)
					return;
				{
					ushort port = ushort.Parse(url.Substring(colonAt + 1));
					
					TcpClient.Connect(url.Substring(0, colonAt), port);
				}
				StartNoSSLSelf();
			}
			catch (Exception /*ex*/)
			{
			//	Out.Log(Significance.Important,"Unable to connect to service host on port "+port+": "+ex.ToString());
				//Program.ShutDown(-1);
			}
		}

	}
}
