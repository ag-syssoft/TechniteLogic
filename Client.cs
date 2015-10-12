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
			Out.Log(Significance.Common, "Connected. Authenticating...");
			Interface.ready.SendTo(this,Interface.CompileProtocolString());

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

		

		public void Connect(ushort port)
		{
			try
			{
                TcpClient.Connect("localhost",port);
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
