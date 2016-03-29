using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Math3D;
using Protocol;
using Logging;
using System.Security.Cryptography;

namespace TechniteLogic
{
	public static class Interface
	{
		public enum ChannelID
		{
			Unused,
			Challenge,          //	s2c : CryptographicID
			Authenticate,       //	c2s : Authenticate
			Authenticated,      //	s2c : <signal>
			NodeChunk,          //	s2c : NodeChunk
			GridConfig,         //	s2c : GridConfig
			StateUpdateBegin,   //	s2c : <signal>
			EntityContact,      //	s2c : EntityContact
			OwnTechniteState,   //	s2c : OwnState
			OtherTechniteState, //	s2c : OtherState
			TerrainState,       //	s2c : NETA<BYTE>
			Message,            //	s2c : Message
			IsDead,             //	s2c : <signal>
			RegularInstruction, //	c2s : RegularInstruction
			MessageInstruction, //	c2s : MessageInstruction
			ProcessRound,       //	s2c : ProcessRound

			Count     //must remain last
		};


		public static OutChannel<Struct.Authenticate> authenticate = new Protocol.OutChannel<Struct.Authenticate>((uint)ChannelID.Authenticate);
		public static OutChannel<Struct.RegularInstruction> regularInstruction = new Protocol.OutChannel<Struct.RegularInstruction>((uint)ChannelID.RegularInstruction);
		public static OutChannel<Struct.MessageInstruction> messageInstruction = new Protocol.OutChannel<Struct.MessageInstruction>((uint)ChannelID.MessageInstruction);
		internal static Client globalClient;

		public static void Register()
        {
			ChannelMap.Register<Struct.Sha256Hash>((uint)ChannelID.Challenge,Event.Challenge);
			ChannelMap.RegisterSignal((uint)ChannelID.Authenticated, Event.Authenticated);
			ChannelMap.RegisterSignal((uint)ChannelID.StateUpdateBegin, Event.StateUpdateBegin);
			ChannelMap.RegisterSignal((uint)ChannelID.IsDead, Event.IsDead);

			ChannelMap.Register<Struct.EntityContact>((uint)ChannelID.EntityContact, Event.EntityContact);
			ChannelMap.Register<Struct.OwnState>((uint)ChannelID.OwnTechniteState, Event.OwnTechniteState);
			ChannelMap.Register<Struct.OtherState>((uint)ChannelID.OtherTechniteState, Event.OtherTechniteState);
			ChannelMap.Register<Struct.TerrainStateCell[]>((uint)ChannelID.TerrainState, Event.TerrainState);
			ChannelMap.Register<Struct.Message>((uint)ChannelID.Message, Event.Message);
			ChannelMap.Register<Struct.ProcessRound>((uint)ChannelID.ProcessRound, Event.ProcessRound);



			ChannelMap.Register<Struct.GridConfig>((uint)ChannelID.GridConfig, Event.GridConfig);
			ChannelMap.Register<Struct.NodeChunk>((uint)ChannelID.NodeChunk, Event.NodeChunk);
			//ChannelMap.Register<Struct.WorldInfo>((uint)ChannelID.WorldInfo, Event.WorldInfo);

		}


		public class Struct
		{


			//public struct WorldInfo
			//{
			//	public byte coreContent;
			//}


			public struct CommonTechniteState
			{
				public UInt32 location;
				public byte resources;
			}


			public struct OwnState
			{
				public CommonTechniteState commonState;
				public byte taskResult;
				public byte compressedState;
				public float visionRadius;
			}

			public struct OtherState
			{
				public Uuid id;
				public CommonTechniteState commonState;
			}



			public struct TerrainStateCell
			{
				public UInt32 compressedLocation;
				public byte content;
			};


			public struct EntityContact
			{
				public Uuid id;
				public string type;
				public byte height;
				public UInt32 location;
				public UInt16 currentHealth;
				public UInt16 maxHealth;
			}

			public struct Message
			{
				public Uuid sender;
				public string message;
			}

			public struct Sha256Hash
			{
				public UInt64 v0, v1, v2, v3;

				public Sha256Hash(byte[] data)
				{
					Debug.Assert(data.Length == 32);
					v0 = BitConverter.ToUInt64(data, 0);
					v1 = BitConverter.ToUInt64(data, 8);
					v2 = BitConverter.ToUInt64(data, 16);
					v3 = BitConverter.ToUInt64(data, 24);
				}
				public byte[] Bytes
				{
					get
					{
						byte[] rs = new byte[4 * 8];
						BitConverter.GetBytes(v0).CopyTo(rs,0);
						BitConverter.GetBytes(v1).CopyTo(rs, 8);
						BitConverter.GetBytes(v2).CopyTo(rs, 16);
						BitConverter.GetBytes(v3).CopyTo(rs, 24);
						return rs;
					}
				}
			}

			public struct Authenticate
			{
				public Uuid myID;
				public Sha256Hash challengeResponse;
				public string protocolVersion;
			}


			public struct RegularInstruction
			{
				public byte nextTask,
							relativeTarget,
							taskParameter;
			}

			public struct MessageInstruction
			{
				public Uuid receiver;
				public byte receiverLocation;
				public string message;
			}


			

			public struct ProcessRound
			{
				public UInt32 roundNumber,
								techniteSubRoundNumber;
			}

			
			public struct GridNode
            {
                public UInt32[] neighbors;
                public Vec3 stackBase;
                public Vec3 stackDirection;
            }

			public struct GridConfig
			{
				public float heightPerLayer;
				public int numLayersPerStack;
				public byte[] initialTTLAtLayer,
								energyYieldAtLayer;
				public byte coreContent;
			}

			public struct NodeChunk
			{
				public bool isLast;
				public GridNode[] nodes;
			}

		}

		public static class Event
		{

			static byte[] Concat(byte[] a, byte[] b)
			{
				byte[] c = new byte[a.Length + b.Length];
				a.CopyTo(c, 0);
				b.CopyTo(c, a.Length);
				return c;
			}

			internal static void NodeChunk(Protocol.Client cl, Struct.NodeChunk nodeChunk)
			{
				Grid.Graph.AddChunk(nodeChunk);
			}

			internal static void GridConfig(Protocol.Client cl, Struct.GridConfig gridConfig)
			{
				Grid.BeginSession(gridConfig.heightPerLayer, gridConfig.numLayersPerStack);
				Grid.World.Setup((Grid.Content)gridConfig.coreContent);

				//...
			}

			//internal static void WorldInfo(Protocol.Client cl, Struct.WorldInfo worldInfo)
			//{
			//	Grid.World.Setup((Grid.Content)worldInfo.coreContent);

			//}

			internal static void Error(Protocol.Client client, string message)
			{
				Out.Log(Significance.ProgramFatal, ">"+message);
				client.ForceDisconnect();
				return;
			}

			internal static void Challenge(Protocol.Client peer, Struct.Sha256Hash challenge)
			{
				Out.Log(Significance.Important, "Received challenge. Authenticating...");
				SHA256 sha = SHA256.Create();
				Struct.Authenticate rs = new Struct.Authenticate();
				
				rs.challengeResponse = new Struct.Sha256Hash(sha.ComputeHash(Concat(Session.secret, challenge.Bytes)));
				rs.myID = Technite.Me.ID;
				rs.protocolVersion = CompileProtocolString();
				Interface.authenticate.SendTo(peer, rs);
			}

			internal static void Authenticated(Protocol.Client peer)
			{
				Out.Log(Significance.Important,"Authenticated");
			}

			internal static void StateUpdateBegin(Protocol.Client peer)
			{
				Contacts.Deprecate();
				Technite.DeprecateOthers();
				Grid.World.FlushVisibility();
				Messages.Clear();
			}

			internal static void IsDead(Protocol.Client peer)
			{
				Out.Log(Significance.ClientFatal, "Died");
				peer.ForceDisconnect();
			}

			internal static void EntityContact(Protocol.Client peer, Struct.EntityContact contact)
			{
				Contacts.Add(contact);
			}

			internal static void OwnTechniteState(Protocol.Client peer, Struct.OwnState state)
			{
				Technite.Me.Update(state);
			}

			internal static void OtherTechniteState(Protocol.Client peer, Struct.OtherState state)
			{
				Technite.AddContact(state.id).Update(state.commonState);
			}

			internal static void TerrainState(Protocol.Client peer, Struct.TerrainStateCell[] state)
			{
				foreach (var st in state)
				{
					var loc = new Technite.CompressedLocation(st.compressedLocation).CellID;

					Grid.World.Update(loc, (Grid.Content)st.content);
				}
			}

			internal static void Message(Protocol.Client peer, Struct.Message message)
			{
				Messages.Add(Technite.Find(message.sender), message.message);
			}

			internal static void ProcessRound(Protocol.Client peer, Struct.ProcessRound process)
			{
				Contacts.Tidy();
				Technite.Tidy();
				Session.roundNumber = process.roundNumber;
				Session.techniteSubRoundNumber = process.techniteSubRoundNumber;
				Out.Log(Significance.Important, "Processing round "+Session.roundNumber+", "+Session.techniteSubRoundNumber);

				Logic.Execute();

			}
		}


		public static string CompileProtocolString()
		{
			return "Aquinas v2.0." + (int)ChannelID.Count;
		}
		
	}
}
