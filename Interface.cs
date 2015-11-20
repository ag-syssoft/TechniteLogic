using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Math3D;
using Protocol;
using Logging;

namespace TechniteLogic
{
	public static class Interface
	{
		public enum ChannelID
		{
			Unused,
			Ready,                  //c2s: String: protocol version+name
			Error,                  //s2c: String: message
			TechniteStateChunk,     //s2c: TechniteStateChunk
			InstructTechnites,      //s2c: InstructTechnites
			TechniteInstructionChunk, //c2s: TechniteInstructionChunk

			NodeChunk,      //s2c: NodeChunk - completed before SessionBegin is sent
			GridConfig,     //s2c: GridConfig
			GridDelta,      //s2c: GridDelta
			WorldInfo,      //s2c: WorldInfo
			BeginSession,   //s2c: BeginSession

			CreateControlMarker,    //s2c: ControlMarker
			CreateGameObject,       //s2c: GameObject
			RemoveControlMarker,    //s2c: UINT32 (compressed location)
			RemoveGameObject,       //s2c: RemoveGameObject

			RequestNextRound, //c2s: <signal>

			TechniteColorChunk, //c2s: TechniteColorChunk

			Count     //must remain last
		};


		public static OutChannel<Struct.TechniteInstructionChunk> techniteInstructionChunk = new Protocol.OutChannel<Struct.TechniteInstructionChunk>((uint)ChannelID.TechniteInstructionChunk);
		public static OutChannel<string> ready = new OutChannel<string>((uint)ChannelID.Ready);
		public static SignalChannel requestNextRound = new SignalChannel((uint)ChannelID.RequestNextRound);
		public static OutChannel<Struct.TechniteColorChunk> techniteColorChunk = new OutChannel<Struct.TechniteColorChunk>((uint)ChannelID.TechniteColorChunk);

		public static void Register()
        {
			ChannelMap.Register<string>((uint)ChannelID.Error, Event.Error);
			ChannelMap.Register<Struct.TechniteStateChunk>((uint)ChannelID.TechniteStateChunk, Event.TechniteStateChunk);
			ChannelMap.Register<Struct.InstructTechnites>((uint)ChannelID.InstructTechnites,Event.InstructTechnites);
			ChannelMap.Register<Struct.GridConfig>((uint)ChannelID.GridConfig, Event.GridConfig);
			ChannelMap.Register<Struct.NodeChunk>((uint)ChannelID.NodeChunk, Event.NodeChunk);
			ChannelMap.Register<Struct.GridDelta>((uint)ChannelID.GridDelta, Event.GridDelta);
			ChannelMap.Register<Struct.WorldInfo>((uint)ChannelID.WorldInfo, Event.WorldInfo);
			ChannelMap.Register<Struct.ControlMarker>((uint)ChannelID.CreateControlMarker, Event.CreateControlMarker);
			ChannelMap.Register<Struct.GameObject>((uint)ChannelID.CreateGameObject, Event.CreateGameObject);
			ChannelMap.Register<UInt32>((uint)ChannelID.RemoveControlMarker, Event.RemoveControlMarker);
			ChannelMap.Register<Struct.GameObjectID>((uint)ChannelID.RemoveGameObject, Event.RemoveGameObject);
			ChannelMap.Register<Struct.BeginSession>((uint)ChannelID.BeginSession, Event.BeginSession);

		}


		public class Struct
		{

			public struct TechniteResources
			{
				public byte energy,
							matter;


			}

			public struct WorldInfo
			{
				public byte coreContent;
			}

			public struct TechniteState
			{
				public UInt32 location;
				public TechniteResources resources;
				public byte taskResult,
							state;
			}

			public struct Color
			{
				public UInt32 index;
				public byte r, g, b;

				public Color(UInt32 index, Technite.Color c)
				{
					this.index = index;
					r = c.Red;
					g = c.Green;
					b = c.Blue;
				}
			}

			public struct TechniteColorChunk
			{
				public UInt32 offset;
				public Color[] colors;
			}


			public enum TechniteChunkFlags
			{
				IsFirst = 0x1,
				IsLast = 0x2

			}

			public struct TechniteStateChunk
			{
				public byte flags;
				public TechniteState[] states;


				public bool FlagIsSet(TechniteChunkFlags flag)
				{
					return ((int)flags & (int)flag) != 0;
				}

			}

			public struct TechniteInstruction
			{
				public byte nextTask,
							taskTarget,
							taskParameter;
			}

			public struct TechniteInstructionChunk
			{
				public const int MaxPerChunk = 10000;
				public UInt32 offset;
				public TechniteInstruction[] instructions;
			}


			public struct GridDeltaBlock
			{
				public UInt32 repitition;
				public byte value;
			}


			public struct GameObjectID
			{
				public UInt32	location;
				public bool		isGhost;
				public Objects.GameObject.ObjectType type;
			}

			public struct GameObject
			{
				public GameObjectID id;
				public UInt32	birthRound;
				public byte		height;
				public bool		isBroad;
				public string	className;
				public bool		isMine;
			}

			public struct ControlMarker
			{
				public UInt32	location;
				public float	radius;
				public byte		typeIndex;
				public UInt32	birthRound;
			}


			public struct InstructTechnites
			{
				public UInt32 roundNumber,
								techniteSubRoundNumber;
			}


			public struct GridDelta
			{
				public UInt32 nodeOffset,
								nodeCount;
				public GridDeltaBlock[] contentBlocks,
										techniteFactionBlocks;

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
				public byte[] matterYieldByContentType;
				public byte[] matterDegradationTable;
				public byte[] initialTTLAtLayer,
								energyYieldAtLayer;
			}

			public struct Chunk<T>
			{
				public bool isLast;
				public T[] elements;

			}

			public struct NodeChunk
			{
				public bool isLast;
				public GridNode[] nodes;
			}

			public struct BeginSession
			{
				public string factionUUID;
				public byte techniteGridID;

				public UInt32 roundNumber,
								techniteSubRoundNumber;
			}

			//public struct SessionBegin
			//{
			//	public GridConfig grid;
			//	public byte myFactionGridID;
			//}
		}

        public static class Event
		{

			public static void InstructTechnites(Protocol.Client cl, Struct.InstructTechnites instruct)
			{
				Technite.Cleanup();	//updates must be done by now
				Session.roundNumber = instruct.roundNumber;
				Session.techniteSubRoundNumber = instruct.techniteSubRoundNumber;
				Out.Log(Significance.Common, "Instructing technites in round " + Session.roundNumber+"/"+Session.techniteSubRoundNumber);
				Logic.ProcessTechnites();


				SendColorState(cl);

				int numTechnites = Technite.Count;
				int numChunks = numTechnites / Struct.TechniteInstructionChunk.MaxPerChunk;
				if ((numTechnites % Struct.TechniteInstructionChunk.MaxPerChunk)!= 0)
					numChunks++;


				Out.Log(Significance.Common, "Sending "+numChunks+" technite data response chunks");
				var e = Technite.All.GetEnumerator();
				int offset = 0;
				for (int i = 0; i < numChunks; i++)
				{
					int chunkSize = Math.Min(Struct.TechniteInstructionChunk.MaxPerChunk, numTechnites - offset);

					Struct.TechniteInstructionChunk chunk = new Struct.TechniteInstructionChunk();
					chunk.offset = (uint)offset;
					chunk.instructions = new Struct.TechniteInstruction[chunkSize];
					for (int j = 0; j < chunkSize; j++)
					{
						bool success = e.MoveNext();
						Debug.Assert(success);
                        Technite t = e.Current;
						chunk.instructions[j] = t.ExportInstruction();
					}

					techniteInstructionChunk.SendTo(cl,chunk);

					offset += chunkSize;
				}
			}

			public static void TechniteStateChunk(Protocol.Client cl, Struct.TechniteStateChunk chunk)
			{
				Out.Log(Significance.Common, "Received technite state chunk containing "+chunk.states.Length+" technites");
				if (chunk.FlagIsSet(Struct.TechniteChunkFlags.IsFirst))
					Technite.Reset();
				foreach (var state in chunk.states)
					Technite.CreateOrUpdate(state);
				if (chunk.FlagIsSet(Struct.TechniteChunkFlags.IsLast))
					Technite.Cleanup();

			}

			internal static void GridDelta(Protocol.Client cl, Struct.GridDelta gridDelta)
			{
				Grid.ApplyDelta(gridDelta);
			}

			internal static void NodeChunk(Protocol.Client cl, Struct.NodeChunk nodeChunk)
			{
				Grid.Graph.AddChunk(nodeChunk);
			}

			internal static void GridConfig(Protocol.Client cl, Struct.GridConfig gridConfig)
			{
				Grid.BeginSession(gridConfig.heightPerLayer, gridConfig.numLayersPerStack);
				if (Technite.MatterYield.Length != gridConfig.matterYieldByContentType.Length)
				{
					Out.Log(Significance.ProgramFatal, "Received matter yield vector (" + gridConfig.matterYieldByContentType.Length + ") does not match number of supported content types (" + Technite.MatterYield.Length + ").");
					return;
				}
				if (Technite.MatterDegradeTo.Length != gridConfig.matterDegradationTable.Length)
				{
					Out.Log(Significance.ProgramFatal, "Received matter degradation vector (" + gridConfig.matterDegradationTable.Length + ") does not match number of supported content types (" + Technite.MatterDegradeTo.Length + ").");
					return;
				}

                Technite.MatterYield = gridConfig.matterYieldByContentType;
				Technite.MatterDegradeTo = gridConfig.matterDegradationTable;

				Technite.InitialTTLAtLayer = gridConfig.initialTTLAtLayer;
				Technite.EnergyYieldAtLayer = gridConfig.energyYieldAtLayer;

		}

		internal static void WorldInfo(Protocol.Client cl, Struct.WorldInfo worldInfo)
			{
				Grid.World.Setup((Grid.Content)worldInfo.coreContent);

			}

			internal static void Error(Protocol.Client client, string message)
			{
				Out.Log(Significance.ProgramFatal, ">"+message);
				client.ForceDisconnect();
				return;
			}

			internal static void CreateControlMarker(Protocol.Client arg1, Struct.ControlMarker marker)
			{
				Objects.Add(marker);
			}

			internal static void CreateGameObject(Protocol.Client arg1, Struct.GameObject obj)
			{
				Objects.Add(obj);
			}

			internal static void RemoveControlMarker(Protocol.Client arg1, UInt32 markerID)
			{
				Objects.RemoveControlMarker(markerID);
			}

			internal static void RemoveGameObject(Protocol.Client arg1, Struct.GameObjectID objID)
			{
				Objects.Remove(objID);
			}

			internal static void BeginSession(Protocol.Client arg1, Struct.BeginSession session)
			{
				Session.Begin(session);
			}
		}

		static List<Struct.Color> colorBuffer = new List<Struct.Color>();


		public static string CompileProtocolString()
		{
			return "Aquinas v1.6." + (int)ChannelID.Count;
		}

		private static void SendColorChunks(Protocol.Client cl, UInt32 offset, List<Struct.Color> list)
		{
			//Struct.TechniteColorChunk chunk = new Struct.TechniteColorChunk();
			//chunk.offset = offset;
			//chunk.colors = colorBuffer.ToArray();
			//colorBuffer.Clear();
			//techniteColorChunk.SendTo(client, chunk);

			int numVecs = list.Count();
			int numChunks = numVecs / Struct.TechniteInstructionChunk.MaxPerChunk;
			if ((numVecs % Struct.TechniteInstructionChunk.MaxPerChunk) != 0)
				numChunks++;


			Out.Log(Significance.Common, "Sending " + numChunks + " technite color chunk(s), starting from "+offset);
			int localOffset = 0;
			for (int i = 0; i < numChunks; i++)
			{
				int chunkSize = Math.Min(Struct.TechniteInstructionChunk.MaxPerChunk, numVecs - localOffset);

				Struct.TechniteColorChunk chunk = new Struct.TechniteColorChunk();
				chunk.offset = offset;
				chunk.colors = new Struct.Color[chunkSize];
				list.CopyTo(localOffset, chunk.colors, 0, chunkSize);
				techniteColorChunk.SendTo(cl, chunk);
				offset += (uint)chunkSize;
				localOffset += chunkSize;
            }


		}

		public static void SendColorState(Protocol.Client client)
		{
			colorBuffer.Clear();
			UInt32 at=0;
			foreach (Technite t in Technite.All)
			{
				if (t.UsesCustomColor)
				{
					//if (colorBuffer.Count == 0)
					//	offset = at;
					colorBuffer.Add(new Struct.Color(at, t.CustomColor ));
				}
				//else
				//	if (colorBuffer.Count > 0)
				//{
				//	SendColorChunks(client, offset, colorBuffer);
				//	colorBuffer.Clear();
    //            }
				at++;
			}

			if (colorBuffer.Count > 0)
			{
				SendColorChunks(client, 0, colorBuffer);
				colorBuffer.Clear();
			}
		}
	}
}
