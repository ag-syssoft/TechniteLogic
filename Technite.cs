using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Logging;
using Math3D;

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
		/// Currently constant cost values. Will be moved into the protocol, and specified by the server soon.
		/// </summary>
		public static byte	ConsumeEnergyCost = 2,
							GnatAtEnergyCost = 1,
							SplitEnergyCost = 5,
							SplitMatterCost = 5,
							EnergyPackageSize = 5,
							MatterPackageSize = 5;
        public static bool isLit = false;


		/// <summary>
		/// Amount of matter than can be extracted from the respective grid content type.
		/// Gnawing at a block currently yields half this amount (rounded down)
		/// </summary>
		public static byte[] MatterYield = new byte[Enum.GetValues(typeof(Grid.Content)).Length - 1];//discounting Undefined

		public static byte[] MatterDegradeTo = new byte[Enum.GetValues(typeof(Grid.Content)).Length - 1];//discounting Undefined


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
			public byte		Energy,
							Matter;
			public static readonly Resources Zero = new Resources(0,0);

			private static byte Clamp(int value)
			{
				return (byte)Math.Max( Math.Min(value,byte.MaxValue), byte.MinValue);
			}

			public 			Resources(int energy, int matter)
			{
				Energy = Clamp(energy);
				Matter = Clamp(matter);
			}


			public static implicit operator Resources(Interface.Struct.TechniteResources res)
			{
				return new Resources(res.energy,res.matter);
			}

			public static Resources	operator/(Resources res, int div)
			{
				return new Resources(res.Energy / div, res.Matter / div);

			}

			public static Resources		operator+(Resources a, Resources b)
			{
				return new Resources( Sum(a.Energy,b.Energy), Sum(a.Matter,b.Matter) );
			}

			private static byte Sum(byte a, byte b)
			{
				return Clamp(a+b);
			}

			public bool		Decrease(Resources by)
			{
				if (Matter < by.Matter || Energy < by.Energy)
					return false;
				Matter -= by.Matter;
				Energy -= by.Energy;
				return true;
			}

			public static bool operator>=(Resources a, Resources b)
			{
				return a.Energy >= b.Energy && a.Matter >= b.Matter;
			}
			public static bool operator <=(Resources a, Resources b)
			{
				return a.Energy <= b.Energy && a.Matter <= b.Matter;
			}

			public static bool operator==(Resources a, Resources b)
			{
				return a.Energy == b.Energy && a.Matter == b.Matter;
			}
			public static bool operator !=(Resources a, Resources b)
			{
				return a.Energy != b.Energy || a.Matter != b.Matter;
			}

			public override int GetHashCode()
			{
				int h = Energy.GetHashCode();
				h *= 257;
				h += Matter.GetHashCode();
				return h;
			}

			public override bool Equals(object obj)
			{
				return obj is Resources && ((Resources)obj == this);
			}

			public override string ToString()
			{
				return "Energy=" + Energy + ", Matter=" + Matter;
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

		public static readonly Resources	SplitCost = new Resources(SplitEnergyCost,SplitMatterCost), 
												ConsumeCost = new Resources(ConsumeEnergyCost, 0),
												GnawAtCost = new Resources(GnatAtEnergyCost, 0);


		/// <summary>
		/// Possible tasks. Must be kept in sync with the server implementation, or very weird things will happen.
		/// The transfer protocol supports up to 256 different tasks.
		/// </summary>
		public enum Task
		{
			/// <summary>
			/// Don't do anything.
			/// </summary>
			None,
			/// <summary>
			/// Destructive task to gain matter from a world volume cell.
			/// If the targeted cell contains terrain matter, the full matter yield is awarded to the local technite, and the respective
			/// content type degrades. Most matter types follow a chain of degradations until they are finally removed entirely (<see cref="Technite.MatterDegradeTo"/>). If
			/// multiple technites try to consume the same terrain cell, the order is determined randomly, and the terrain cell
			/// may be degraded several times during the same round.
			/// If the targeted cell contains a technite of the local faction, the respective technite is destroyed, and a portion of
			/// its resources awarded to the local technite.
			/// If the targeted cell contains a hostile technite, the relative amount of stored energy determines which technite wins.
			/// If the local technite has equal or more energy in storage than the hostile technite, the targeted technite is destroyed, and
			/// a portion of its stored matter awarded to the local technite. The energy amount stored by the hostile technite is
			/// _removed_ from the local technite's storage.
			/// If, instead, the hostile technite has more energy, the task fails, and both technites lose the amount of energy stored
			/// by the local technite. As a result the local technite will survive, but have zero energy left.
			/// </summary>
			ConsumeSurroundingCell,
			/// <summary>
			/// Non-destructive task to gain matter from a world volume cell.
			/// Half the targeted cell's matter yield is awarded to the local cell. The targeted cell is not affected.
			/// GnawAt can only be executed on terrain content types. Technite targets are invalid.
			/// </summary>
			GnawAtSurroundingCell,
			/// <summary>
			/// Creates a new technite in the specified location. The new technite starts with a TTL of 64, and zero matter.
			/// An initial amount of energy depending on the height (and lighting state) of the new technite is granted as a bonus.
			/// The local technite loses energy and matter as specified in Technite.SplitCost.
			/// If the local technite does not have enough resources, or the destination cell is unsuitable (e.g. filled by Lava), 
			/// the task will fail.
			/// GrowTo implicitly executes ConsumeSurroundingCell in the targeted cell first. If any resources are awarded this way, they
			/// are given to the local technite (not the new technite).
			/// Note that this command can destroy other technites, friendly or hostile.
			/// </summary>
			GrowTo,
			/// <summary>
			/// Transfers the energy amount specified in the task parameter to the targeted neighbor technite.
			/// The task will fail if the local technite does not have that much energy, or the destination technite has vanished.
			/// If the targeted technite as a result holds more than the maximum allowed amount of energy per technite (255), then any
			/// excess energy is lost.
			/// </summary>
			TransferEnergyTo,
			/// <summary>
			/// Transfers the matter amount specified in the task parameter to the targeted neighbor technite.
			/// The task will fail if the local technite does not have that much matter, or the destination technite has vanished.
			/// If the targeted technite as a result holds more than the maximum allowed amount of matter per technite (255), then any
			/// excess matter is lost.
			/// </summary>
			TransferMatterTo,
			/// <summary>
			/// Kills the local technite and replaces it with the content specified in the task parameter.
			/// Conversion into a type that cannot be mined by technites is not possible. If, for Grid.Content type, MatterYield[type] is 0,
			/// then type is not a valid transform parameter. An exception will be thrown in this case.
			/// The local technite also loses an amount of matter equal to MatterYield[parameter]. The task will fail otherwise.
			/// </summary>
			SelfTransformToType,
			/// <summary>
			/// Creates a single-round spherical sensor field arround the technite.
			/// The task parameter specifies the amount of energy that should be used for the burst.
			/// The sensor radius applied is calculated as the square root of this amount.
			/// The task fails if the local technite does not have enough energy to execute the burst.
			/// The sensor bubble remains in place until the next round is processed, and captures any technites or units within.
			/// </summary>
			Scan,

			Count
		};

		/// <summary>
		/// Result of the last executed task. Must be kept in sync with the server implementation, or very weird things will happen.
		/// The transfer protocol supports up to 256 different task result codes.
		/// </summary>
		public enum TaskResult
		{
			NothingToDo,
			/// <summary>
			/// This usually should not happen as it indicates a problem with the targeted cell or the command itself.
			/// </summary>
			BadCommand,
			/// <summary>
			/// The targeted terrain cell is not suitable for consumation. This may happen if the targeted cell contains laval,
			/// or any contained entity is indestructible.
			/// </summary>
			CannotEatThis,
			/// <summary>
			/// The targeted cell contains something that cannot be harvested without destroying it.
			/// This may happen if the target cell contains a live technite, a structure/vehicle, or lava. With the exception of lava, using ConsumeSurroundingCell,
			/// instead, will yield better results.
			/// </summary>
			CannotGnawAtThis,
			/// <summary>
			/// The desired location is not valid for a new technite to spawn into
			/// </summary>
			CannotStandThere,
			/// <summary>
			/// The operation failed because the technite has insufficient resources to complete it.
			/// </summary>
			TechniteLacksResources,
			/// <summary>
			/// A transfer operation failed because the targeted cell does not contain a live technite.
			/// </summary>
			NoTechniteAtDestination,
			/// <summary>
			/// The operation has changed something, but the same command must be issued again.
			/// This happens if the technite is explicitly or implicitly trying to consume a terrain cell that requires multiple iterations to desolve completely.
			/// </summary>
			MoreWorkNeeded,
			/// <summary>
			/// Could not complete operation in the iteration required. Most commonly this means some other technite is competing for the same target cell and has delayed the local cell too long
			/// </summary>
			OperationWindowMissed,
			/// <summary>
			/// This code is never transfered to the client but contained here for compatibility reasons. Certain commands require the executing server to retry a certain operation at a later stage.
			/// </summary>
			Again,
			/// <summary>
			/// The operation succeeded as intended.
			/// </summary>
			Success
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



		Resources			resources, lastResources;
		TaskResult			taskResult = TaskResult.NothingToDo;
		Color				customColor = new Color();
		bool				usesCustomColor = false;


		public struct Color
		{
			public readonly byte Red, Green, Blue;

			public Color(byte red, byte green, byte blue)
			{
				Red = red;
				Green = green;
				Blue = blue;
			}

			public			Color(Vec3 color)
			{
				Red = ClampF(color.x);
				Green = ClampF(color.y);
				Blue = ClampF(color.z);
			}

			public			Color(double red, double green, double blue)
			{
				Red = ClampF(red);
				Green = ClampF(green);
				Blue = ClampF(blue);
			}

			private static byte ClampF(float f)
			{
				return (byte)Math.Min(Math.Max(0f, f * 255f), 255f);
			}
			private static byte ClampF(double f)
			{
				return (byte)Math.Min(Math.Max(0.0, f * 255.0), 255.0);
			}

			internal static Color Random(Random r)
			{
				return new Color(r.NextDouble(), r.NextDouble(), r.NextDouble());
			}

			public static implicit operator Vec3(Color c)
			{
				return new Vec3((float)c.Red / 255f, (float)c.Green / 255f, (float)c.Blue / 255f);
			}

		};


		public Color		CustomColor { get { return customColor; } }
		public bool			UsesCustomColor {  get { return usesCustomColor;  } }

		public void			SetCustomColor(Color color) { customColor = color;  usesCustomColor = true; }
		public void			UnsetCustomColor() { usesCustomColor = false; }

		
		/// <summary>
		/// World volume location of the local technite. Readonly and unique: technites cannot move.
		/// </summary>
		public readonly Grid.CellID	Location;

		/// <summary>
		/// Retrieves the current resource fill level of the local technite.
		/// </summary>
		public Resources CurrentResources { get { return resources; } }
		/// <summary>
		/// Retrieves the last-round resource fill level of the last technite.
		/// This value is provided for convenience, and memorized locally. The server does not actually maintain/update this value
		/// </summary>
		public Resources LastResources { get { return lastResources; } }
		/// <summary>
		/// Result of the last executed task.
		/// </summary>
		public TaskResult LastTaskResult { get { return taskResult; } }


		Task				nextTask;
		byte				taskParameter;
		Grid.RelativeCell	taskTarget;

		State				state;

		enum TransitionState
		{
			Unknown,
			RemoveFromMap,
			Preserved
		}

		TransitionState transitionState = TransitionState.Unknown;


		/// <summary>
		/// The current technite state (lit/ttl)
		/// </summary>
		public State		Status {  get { return state;  } }


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

			if (t == Task.SelfTransformToType)
			{
				if (parameter > MatterYield.Length)
					throw new TaskException(this,"Parameter for task "+t+" ("+parameter+") is not a valid matter type.");
				if (MatterYield[parameter] == 0)
					throw new TaskException(this,"Parameter for task " + t + " (" + parameter + "/"+((Grid.Content)parameter) + ") is not a suitable transformation output.");
			}
			else
			if ((t == Task.TransferEnergyTo || t == Task.TransferMatterTo) && parameter == 0)
				throw new TaskException(this, "Task "+t+" requires a non-zero parameter value");

			Grid.Content content = Grid.World.CellStacks[absoluteTarget.StackID].volumeCell[absoluteTarget.Layer].content;
			nextTask = t;
			taskParameter = parameter;
			taskTarget = target;
		}

		protected /**/				Technite(Grid.CellID loc)
		{
			Location = loc;
		}

		private void				ImportState(Interface.Struct.TechniteState state)
		{

			taskParameter = 0;
			lastResources = resources;
			resources = state.resources;
			taskResult = (TaskResult)state.taskResult;
			this.state = new CompressedState(state.state).Decoded;
			transitionState = TransitionState.Preserved;

			if (taskResult == Technite.TaskResult.OperationWindowMissed)
			{
				if (++windowMissedThisRound < MaxLogPerRound)
					Out.Log(Significance.Unusual, "Operation Window Missed on " + nextTask + " of " + this);
			}


			if (taskResult != TaskResult.MoreWorkNeeded)
				nextTask = Task.None;

		}



		private static Dictionary<Grid.CellID,Technite>	map = new Dictionary<Grid.CellID,Technite>();

		private static List<Technite>	all = new List<Technite>(), check = new List<Technite>();

		public static IEnumerable<Technite> All { get { return all; } }
		public static int Count { get {  return all.Count; } }

		/// <summary>
		/// Describes whether or not the local technite has sufficient resources to execute <see cref="Task.GrowTo"/>
		/// </summary>
		public bool CanSplit { get { return CurrentResources >= SplitCost;  } }
		/// <summary>
		/// Describes whether or not the local technite has sufficient resources to execute <see cref="Task.ConsumeSurroundingCell"/> 
		/// </summary>
		public bool CanConsume { get { return CurrentResources >= ConsumeCost; } }
		/// <summary>
		/// Describes whether or not the local technite has sufficient resources to execute <see cref="Task.GnawAt"/> 
		/// </summary>
		public bool CanGnawAt { get { return CurrentResources >= GnawAtCost; } }

		/// <summary>
		/// Placeholder. Not actually updated at this stage. Will return the byte code of the local faction.
		/// </summary>
		public static byte MyFactionGridID { get; internal set; }

		/// <summary>
		/// Describes the last assigned task
		/// </summary>
		public Task LastTask { get { return nextTask; } }

		/// <summary>
		/// The TTL value assigned to new technites on that layer.
		/// </summary>
		public static byte[] InitialTTLAtLayer { get; internal set; }
		public static byte[] EnergyYieldAtLayer { get; internal set; }


		public byte EnergyYieldPerRound { get { return EnergyYieldAtLayer[Location.Layer]; } }

		/// <summary>
		/// Automatically called once before the first technite state chunk is processed.
		/// Marks all known technites as potentially dead until proven otherwise.
		/// </summary>
		public static void Reset()
		{
			check.Clear();
			foreach (var t in all)
			{ 
				t.transitionState = TransitionState.Unknown;	//leave in dict for now
				check.Add(t);
			}
			all.Clear();
		}

		/// <summary>
		/// Overwritable constructor for new technites, in case derived classes are desired
		/// </summary>
		public static Func<Grid.CellID, Technite>	createNew = (loc) => new Technite(loc);


		private static int createdThisRound = 0,
							diedThisRound = 0,
							windowMissedThisRound = 0;
		private const int MaxLogPerRound = 3;
		/// <summary>
		/// Automatically called for each technite state chunk received from the server.
		/// </summary>
		/// <param name="state"></param>
		public static void CreateOrUpdate(Interface.Struct.TechniteState state)
		{
			Grid.CellID loc = new CompressedLocation(state.location).CellID;

			Grid.Content cellContent = Grid.World.CellStacks[loc.StackID].volumeCell[loc.Layer].content;

			Technite tech;
			if (map.TryGetValue(loc, out tech))
			{
				if (tech.state.TTL < new CompressedState(state.state).GetTTL())
				{
					tech.transitionState = TransitionState.RemoveFromMap;
					map.Remove(loc);
				}
				else
				{
					tech.ImportState(state);
					all.Add(tech);

					if (cellContent != Grid.Content.Technite)
						Out.Log(Significance.Unusual, "Expected technite content in cell, but found " + cellContent + " (reused state is " + tech + ")");


					return;
				}
			}
			tech = createNew(loc);
			//new Technite(loc);
			tech.ImportState(state);
			map.Add(loc,tech);
			all.Add(tech);
			if (++createdThisRound <= MaxLogPerRound)
				Out.Log(Significance.Low, tech + " created");

			if (cellContent != Grid.Content.Technite)
				Out.Log(Significance.Unusual, "Expected technite content in cell, but found " + cellContent + " (new state is " + tech + ")");
		}

		/// <summary>
		/// Called during Cleanup(), if death is detected.
		/// </summary>
		public virtual void OnDeath()
		{
			if (++diedThisRound <= MaxLogPerRound)
				Out.Log(Significance.Low, this + " died");

		}

		/// <summary>
		/// Automatically called after technite state frames have been received, before logic processing should begin.
		/// Dumps any remaining technite objects that apparently died last round.
		/// </summary>
		public static void Cleanup()
		{
			foreach (Technite t in check)
			{
				if (t.transitionState != TransitionState.Preserved)
				{
					t.OnDeath();
					if (t.transitionState != TransitionState.RemoveFromMap)
						map.Remove(t.Location);
				}
			}
			check.Clear();


			if (diedThisRound > 0)
				Out.Log(Significance.Common, diedThisRound + " technite(s) died this round.");
			diedThisRound = 0;
			if (createdThisRound > 0)
				Out.Log(Significance.Common, createdThisRound + " technite(s) were created this round.");
            createdThisRound = 0;
			if (windowMissedThisRound > 0)
				Out.Log(Significance.Unusual, windowMissedThisRound + " technite(s) missed their operation window this round.");
			windowMissedThisRound = 0;

			//int badCount = 0;
			//foreach (Grid.CellStack stack in Grid.World.CellStacks)
			//{
			//	foreach (Grid.CellStack.Cell cell in stack.volumeCell)
			//	{
			//		if (cell.content == Grid.Content.Undefined)
			//		{
			//			badCount++;

			//		}
			//	}
			//}
			//if (badCount > 0)
			//	Out.Log(Significance.Unusual, "Found terrain holes in "+badCount+"/"+(Grid.World.CellStacks.Length*Grid.CellStack.LayersPerStack)+" locations");
		}

		/// <summary>
		/// Erases all global technite data from the session.
		/// Use this to reset technite state to program default
		/// </summary>
		public static void FlushAllData()
		{
			map.Clear();
			all.Clear();
			check.Clear();
		}

		internal Interface.Struct.TechniteInstruction ExportInstruction()
		{
			return new Interface.Struct.TechniteInstruction()
			{
				nextTask = (byte)this.nextTask,
				taskParameter = this.taskParameter,
				taskTarget = new CompressedTarget(this.taskTarget).Data
			};
		}

		internal static bool EnoughSupportHere(Grid.CellID cell)
		{
			if (Grid.IsSolid(cell.BottomNeighbor.BottomNeighbor))
				return true;
			foreach (var n0 in cell.GetHorizontalNeighbors())
				if (Grid.IsSolid(n0) && Grid.IsSolid(n0.BottomNeighbor))
					return true;
			return false;
		}
	}
}
