using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Logging;
using Math3D;
using System.Diagnostics;

namespace TechniteLogic
{
	/// <summary>
	/// Technite representation. Once instance is created/maintained per owned technite. Technite objects are persistent
	/// as long as they live. In rare cases the same object may be reused if a technite dies, and is replaced by a new
	/// technite during the same round.
	/// Known technites of other factions are represented in the world grid, but no instances of this class are created.
	/// </summary>
	public class Technite
	{

		/// <summary>
		/// Byte encoding of a technite location. Used as a more compact description applied during serialization.
		/// </summary>
		public struct CompressedLocation
		{
			private readonly UInt32		Data;

//			public UInt32	Data { get { return Data; } }

			public CompressedLocation(UInt32 data)
			{
				Data = data;
			}

			public	Grid.CellID	CellID
			{
				get
				{
					return new Grid.CellID(GetStackID(), (int)GetLayer());
				}
			}


			public uint		GetLayer()
			{
				return Data & 0xFF;
			}
		
			public uint	GetStackID()
			{
				return Data >> 8;
			}


			public static implicit operator CompressedLocation(Grid.CellID cellID)
			{
				return new CompressedLocation((cellID.StackID << 8) | (uint)cellID.Layer );
			}
		}

		/// <summary>
		/// Technite resource state. Contains the amount of each available resource type
		/// </summary>
		public struct Resources
		{
			public byte		Energy;
			public static readonly Resources Zero = new Resources(0);

			private static byte Clamp(int value)
			{
				return (byte)Math.Max( Math.Min(value,byte.MaxValue), byte.MinValue);
			}

			public 			Resources(int energy)
			{
				Energy = Clamp(energy);
			}


			//public static implicit operator Resources(Interface.Struct.TechniteResources res)
			//{
			//	return new Resources(res.energy);
			//}

			public static Resources	operator/(Resources res, int div)
			{
				return new Resources(res.Energy / div);

			}

			public static Resources		operator+(Resources a, Resources b)
			{
				return new Resources( Sum(a.Energy,b.Energy) );
			}

			private static byte Sum(byte a, byte b)
			{
				return Clamp(a+b);
			}

			public bool		Decrease(Resources by)
			{
				if (Energy < by.Energy)
					return false;
				Energy -= by.Energy;
				return true;
			}

			public static bool operator>=(Resources a, Resources b)
			{
				return a.Energy >= b.Energy;
			}
			public static bool operator <=(Resources a, Resources b)
			{
				return a.Energy <= b.Energy;
			}

			public static bool operator==(Resources a, Resources b)
			{
				return a.Energy == b.Energy;
			}
			public static bool operator !=(Resources a, Resources b)
			{
				return a.Energy != b.Energy;
			}

			public override int GetHashCode()
			{
				int h = Energy.GetHashCode();
				//h *= 257;
				//h += Matter.GetHashCode();
				return h;
			}

			public override bool Equals(object obj)
			{
				return obj is Resources && ((Resources)obj == this);
			}

			public override string ToString()
			{
				return "Energy=" + Energy;
			}
		};

		/// <summary>
		/// Attempts to a find a technite based on its location.
		/// Hashtable lookup is used to quickly locate the technite.
		/// </summary>
		/// <param name="location">Location to look at</param>
		/// <returns>Technite reference, if a technite was found in the given location, null otherwise</returns>
		public static Technite Find(Grid.CellID location)
		{
			Technite rs;
			if (map.TryGetValue(location, out rs))
				return rs;
			return null;
		}



		/// <summary>
		/// Possible tasks. Must be kept in sync with the server implementation, or very weird things will happen.
		/// The transfer protocol supports up to 256 different tasks.
		/// </summary>
		public enum Task
		{
			/// <summary>
			/// Don't do anything.
			/// </summary>
			None,       //->TaskResult::Success
			Scan,       //->TaskResult::Success/InsufficientResources
			Message,    //->TaskResult::Success/TargetOutOfRange/TargetNoTechnite/TargetOffline
			Move,       //->TaskResult::Success/TargetBlocked/TargetOutOfRange/InsufficientResources/CannotStandHere
			Transfer,   //->TaskResult::Success/TargetOutOfRange/TargetNoTechnite/InsufficientResources
			Replace,    //->TaskResult::Success/TargetBlocked/TargetOutOfRange/InsufficientResources
			End,        //->TaskResult::Success/InsufficientResources
			Spawn,      //->TaskResult::Success/TargetBlocked/InsufficientResources
			Kill,       //->TaskResult::Success/TargetOutOfRange/TargetNoTechnite

			Count
		};

		/// <summary>
		/// Result of the last executed task. Must be kept in sync with the server implementation, or very weird things will happen.
		/// The transfer protocol supports up to 256 different task result codes.
		/// </summary>
		public enum TaskResult
		{
			Success,
			BadCommand,
			CannotStandThere,
			TargetOutOfRange,
			TargetNoTechnite,
			TargetBlocked,
			TargetOffline,
			InsufficientResources,
			NoTechniteAtDestination,
		};

		/// <summary>
		/// Byte encoding of a relative cell. Used as a more compact description applied during serialization. Relative cells can be encoded in a single byte.
		/// </summary>
		public struct CompressedTarget
		{
			public readonly byte Data;

			public Grid.RelativeCell Decoded
			{ 
				get
				{
					return new Grid.RelativeCell(GetNeighborIndex(), GetHeightDelta());
				}
			}

			public		CompressedTarget(byte data)
			{
				Data = data;
			}
			public CompressedTarget(Grid.RelativeCell target)
			{
				Data = (byte)((byte)target.NeighborIndex | (byte)((target.HeightDelta + 1) << 4));
			}

			public uint	GetNeighborIndex()
			{
				return (uint)(Data & 0xF);
			}
			public int			GetHeightDelta()
			{
				return (((int)(Data >> 4)) - 1);
			}

		}


		/// <summary>
		/// Byte encoding of the current technite state (TTL/lit). Used as a more compact description applied during serialization. Technite states can be encoded in a single byte.
		/// </summary>
		public struct CompressedState
		{
			public readonly byte Data;
		
			public		CompressedState(State st)
			{
				Data = (byte)((st.TTL & 0x7F) | (st.Lit ? 0x80 : 0x0));
			}

			public		CompressedState(byte data)
			{
				Data = data;
			}

			public State Decoded
			{
				get
				{
					return new State(IsLit(), GetTTL());
				}
			}

			public bool		IsLit()
			{
				return (Data & 0x80) != 0;
			}
			public byte		GetTTL()
			{
				return (byte)(Data & 0x7f);
			}

		}

		/// <summary>
		/// Extracted technite state
		/// </summary>
		public struct State
		{
			/// <summary>
			/// Indicates that this technite is lit/not shaded.
			/// Lit technites receive energy per round corresponding to their height in the world volume. The higher, the more.
			/// </summary>
			public readonly bool Lit;
			/// <summary>
			/// Maximum remaining rounds that this technite has left to live. New technites currently start with a TTL of 64.
			/// Each round the TTL decreases at least by 1, depending on the respective technite's height in the world volume.
			/// The higher, the faster.
			/// </summary>
			public readonly byte TTL;

			public State(bool lit, byte ttl)
			{
				Lit = lit;
				TTL = ttl;
			}

			public override string ToString()
			{
				return "TTL=" + TTL + ", Lit=" + Lit;
			}

		}

		internal static void DeprecateOthers()
		{
			all.Clear();
			idMap.Clear();
			map.Clear();

			map.Add(Me.location, Me);
			idMap.Add(Me.id, Me);
			all.Add(Me);
		}

		internal static void Tidy()
		{
			//todo

		}

		internal void Update(Interface.Struct.OwnState state)
		{
			Update(state.commonState);
			this.state = new CompressedState(state.compressedState).Decoded;
			taskResult = (TaskResult)state.taskResult;
			visionRadius = state.visionRadius;
		}

		internal void Update(Interface.Struct.CommonTechniteState commonState)
		{
			Location = new CompressedLocation(commonState.location).CellID;
			lastResources = resources;
			resources = new Resources(commonState.resources);
		}

		Resources			resources, lastResources;
		TaskResult			taskResult = TaskResult.Success;
		Grid.CellID			location;
		State				state;

		internal static Technite Find(Guid guid)
		{
			Technite rs;
			if (idMap.TryGetValue(guid, out rs))
				return rs;
			return null;
		}

		float				visionRadius;

		public static Technite AddContact(Guid id)
		{
			Technite rs = new Technite(id, Grid.CellID.Invalid);
			idMap.Add(id, rs);
			all.Add(rs);
			//not adding to map here, location is unknown
			return rs;
		}

		Guid				id;

		//public struct Instruction
		//{
		//	public Guid			targetTechnite;
		//	public string		payload;
		//	public Task			nextTask;
		//	public CompressedTarget taskTarget;
		//	public byte			taskParameter;
		//}

		//Instruction			nextInstruction;



		public Grid.CellID Location
		{
			get
			{
				return location;
			}
			set
			{
				map.Remove(location);
				location = value;
				map.Add(location, this);
			}
		}

		/// <summary>
		/// Retrieves the current resource fill level of the local technite.
		/// </summary>
		public Resources	CurrentResources { get { return resources; } }
		/// <summary>
		/// Retrieves the last-round resource fill level of the last technite.
		/// This value is provided for convenience, and memorized locally. The server does not actually maintain/update this value
		/// </summary>
		public Resources	LastResources { get { return lastResources; } }
		/// <summary>
		/// Result of the last executed task.
		/// </summary>
		public TaskResult	LastTaskResult { get { return taskResult; } }
		/// <summary>
		/// The current technite state (lit/ttl)
		/// </summary>
		public State		Status {  get { return state;  } }

		public Guid			ID
		{
			get
			{
				return id;
			}
			set
			{
				idMap.Remove(id);
				id = value;
				idMap.Add(id, this);
			}
		}

		public override string ToString()
		{
			return Location + " (" + state + ") {" + resources + "}";
		}


		public class TaskException : Exception
		{
			public readonly Technite	SourceTechnite;
			public TaskException(Technite t, string message) : base(message)
			{ 
				SourceTechnite = t;
			}

			public override string ToString()
			{
				return SourceTechnite.Location+": "+base.ToString();
			}

		}


		/// <summary>
		/// Updates the next task to execute. Technites can memorize and execute only one task per round, thus the logic
		/// must redefine this task each round. If the last task result is TaskResult.MoreWorkNeeded, then  the last task 
		/// is not cleared automatically, but can be redefined if desired.
		/// Calling the method several times on the same technite before a new round is processed will overwrite the previously set task.
		/// Calling SetNextTask() at all is optional. The technite will sooner or later default to not doing anything in this case.
		/// </summary>
		/// <param name="t">Task to execute next</param>
		/// <param name="target">Location target of the task</param>
		/// <param name="parameter">Task parameter. What the parameter does depends on the task executed.</param>
		public void SetNextTask(Task t, Grid.RelativeCell target, byte parameter = 0)
		{
			//Out.Log(Significance.Low, this + "->" + t + " @" + target);
			Grid.CellID absoluteTarget = Location + target;
			if (!absoluteTarget.IsValid)
				throw new TaskException(this,"Trying to set invalid relative target "+target+". Task not set.");

			//if (t == Task.End)
			//{
			//	if (parameter > MatterYield.Length)
			//		throw new TaskException(this,"Parameter for task "+t+" ("+parameter+") is not a valid matter type.");
			//	if (MatterYield[parameter] == 0)
			//		throw new TaskException(this,"Parameter for task " + t + " (" + parameter + "/"+((Grid.Content)parameter) + ") is not a suitable transformation output.");
			//}
			//else
			if ((t == Task.Transfer) && parameter == 0)
				throw new TaskException(this, "Task "+t+" requires a non-zero parameter value");

			Grid.Content content = Grid.World.CellStacks[absoluteTarget.StackID].volumeCell[absoluteTarget.Layer].content;

			Interface.Struct.RegularInstruction inst = new Interface.Struct.RegularInstruction();
			inst.nextTask = (byte)t;
			inst.relativeTarget = new CompressedTarget(target).Data;
			inst.taskParameter = parameter;

			Interface.regularInstruction.SendTo(Interface.globalClient, inst);
		}

		protected /**/				Technite(Guid id, Grid.CellID loc)
		{
			location = loc;
			this.id = id;
		}




		private static Dictionary<Grid.CellID,Technite>	map = new Dictionary<Grid.CellID,Technite>();
		private static Dictionary<Guid, Technite> idMap = new Dictionary<Guid, Technite>();

		private static List<Technite>	all = new List<Technite>();

		public static readonly Technite Me = new Technite(Guid.Empty,Grid.CellID.Invalid);

		public static IEnumerable<Technite> All { get { return all; } }
		public static int Count { get {  return all.Count; } }

		/// <summary>
		/// Erases all global technite data from the session.
		/// Use this to reset technite state to program default
		/// </summary>
		public static void FlushAllData()
		{
			map.Clear();
			idMap.Clear();
			all.Clear();
			Me.location = Grid.CellID.Invalid;
			Me.id = Guid.Empty;
		}


		internal static bool EnoughSupportHere(Grid.CellID cell)
		{
			if (Grid.IsSolid(cell.BottomNeighbor))
				return true;
			foreach (var n0 in cell.GetHorizontalNeighbors())
				if (Grid.IsSolid(n0) && Grid.IsSolid(n0.BottomNeighbor))
					return true;
			return false;
		}
	}
}
