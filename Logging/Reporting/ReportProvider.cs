using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace Logging
{
	public abstract class ReportProvider
	{
		protected class Client
		{
			TcpClient tcpClient;
			Socket socket;
			Thread thread;
			ReportProvider parent;
			public const int MaxJobCount = 65536;
			ConcurrentQueue<byte[]> writeQueue = new ConcurrentQueue<byte[]>();
			Semaphore writeJobs = new Semaphore(0, MaxJobCount);

			public void Enqueue(byte[] data)
			{
				if (writeQueue.Count >= MaxJobCount)
					return;
				writeQueue.Enqueue(data);
				writeJobs.Release();
			}

			public Client(TcpClient tcpClient, ReportProvider parent)
			{
				this.tcpClient = tcpClient;
				this.parent = parent;
				socket = tcpClient.Client;
				thread = new Thread(new ThreadStart(ThreadMain));
				thread.Start();
			}

			public void Close()
			{
				if (socket != null)
				{
					socket.Close();
					tcpClient.Close();
					socket = null;
					tcpClient = null;
					Enqueue(null);
				}
			}

			private void ThreadMain()
			{
				while (tcpClient != null)
				{
					writeJobs.WaitOne();
					byte[] job;
					if (!writeQueue.TryDequeue(out job))
					{
						Out.Log("", "", "", Significance.Unusual, "Unable to dequeue job");
						continue;
					}
					if (job == null)
					{
						Close();
						return;
					}
					try
					{
						socket.Send(job);
					}
					catch
					{
						Close();
						parent.OnConnectionLoss(this);
						return;
					}
				}
			}


		}

		private Thread listenThread;
		private TcpListener listener;
		public static List<ReportProvider> All = new List<ReportProvider>();
		private List<Client> clients = new List<Client>();
		public bool HasClients { get; private set; }

		public readonly int Port;


		protected ReportProvider(int port)
		{
			Port = port;
			All.Add(this);
			listener = new TcpListener(System.Net.IPAddress.Loopback, port); //IPv4
			listenThread = new Thread(new ThreadStart(Listen));
		}

		public void Start()
		{
			listener.Start();
			listenThread.Start();
		}

		private void AddNewClient(TcpClient client)
		{
			client.NoDelay = true;
			lock (clients)
			{
				Client myClient = new Client(client, this);
				OnClientConnect(myClient);
				clients.Add(myClient);
				HasClients = true;
			}
		}

		protected virtual void OnClientConnect(Client client) { }

		private static volatile bool ended = false;

		internal static void End()
		{
			ended = true;
		}


		private void Listen()
		{
			Out.Log(Significance.Low, "Starting service listening thread on " + listener.LocalEndpoint.ToString());
			while (!ended)
			{
				try
				{
					AddNewClient(listener.AcceptTcpClient());
				}
				catch (InvalidOperationException ex)
				{
					if (!ended)
						Out.Log(Significance.ProgramFatal, ex.ToString());
					return;
				}
				catch (SocketException ex)
				{
					if (!ended)
						Out.Log(Significance.ProgramFatal, ex.ToString());
					return;
				}
				catch (Exception ex)
				{
					Out.Log(Significance.Unusual, ex.ToString());
					return;
				}
			}
			//Program.Log(null, Significance.Common, "Closing down listening thread");  //this is kinda deadly
		}

		public void Close()
		{
			lock (clients)
			{
				if (listener != null)
					listener.Stop();
				listenThread.Join();
				foreach (Client c in clients)
				{
					c.Close();
				}
				clients.Clear();
				HasClients = false;
			}
		}


		private void OnConnectionLoss(Client client)
		{
			lock (clients)
			{
				clients.Remove(client);
				HasClients = clients.Count > 0;
			}
		}



		protected void SendBytes(byte[] bytes)
		{
			lock (clients)
			{
				if (clients.Count == 0)
					return;
				for (int i = 0; i < clients.Count; i++)
					clients[i].Enqueue(bytes);
			}
		}



	}



	public class Logger : ReportProvider
	{
		private static System.Text.ASCIIEncoding encoder = new System.Text.ASCIIEncoding();

		public const int ServicePort = (int)Protocol.Port.Logger;

		public Queue<string> history = new Queue<string>();

		public Logger()
			: base(ServicePort)
		{ }

		public void SendEvent(string str)
		{
			if (HasClients)
			{
				int len = encoder.GetByteCount(str);
				byte[] bytes = new byte[len + 1];
				encoder.GetBytes(str, 0, str.Length, bytes, 0);
				bytes[len] = 0;
				SendBytes(bytes);
			}
			lock (history)
			{
				history.Enqueue(str);
				while (history.Count > 100)
					history.Dequeue();
			}

		}

		protected override void OnClientConnect(Client client)
		{
			lock (history)
			{
				foreach (string str in history)
				{
					int len = encoder.GetByteCount(str);
					byte[] bytes = new byte[len + 1];
					encoder.GetBytes(str, 0, str.Length, bytes, 0);
					bytes[len] = 0;
					client.Enqueue(bytes);
					//client.GetStream().WriteAsync(bytes, 0, bytes.Length);
				}
			}
		}
	}
}
