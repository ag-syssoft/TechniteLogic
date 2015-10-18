using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Logging;

namespace TechniteLogic
{
	public static class Objects
	{
		/// <summary>
		/// Known player object (building, vehicle, or satellite) in the world.
		/// The object may be a ghost, if is not visible anymore, or has died.
		/// Objects are unique by location, type, and IsGhost (e.g. among all ghosts, there can be only one vehicle in a given location, but another building, or a non-ghost vehicle might reside there as well)
		/// </summary>
		public struct GameObject
		{
			/// <summary>
			/// Coarse type of object being referenced here
			/// </summary>
			public enum ObjectType
			{
				/// <summary>
				/// The referenced object is a vehicle that can move around and can actually affect technites. Vehicles can execute a multitude of operations, some of which are damaging to technites
				/// </summary>
				Vehicle,
				/// <summary>
				/// The referenced object is a stationary building. Although some buildings can fire, they have limited terrain damage potential.
				/// </summary>
				Building,
				/// <summary>
				/// The referenced object is a satellite orbiting the planet. Most satellites cannot cause damage to the planetary terrain. The sole exception being Ragnarök, which can
				/// cause devastating damage to any technite faction. Most of the time only owned satellites are visible.
				/// </summary>
				Satellite,
			}
			public struct ObjectID
			{

				/// <summary>
				/// Current location of this object. If it moves (non-buildings), this instance is removed, and a new instance created. Tracking unit motions is currently not possible
				/// </summary>
				public readonly Grid.CellID Location;
				/// <summary>
				/// Indicates that this object is not actually known at this time. If <see cref="IsMine"/> is set, the local object references a lost object that has been destroyed by some means.
				/// </summary>
				public readonly bool IsGhost;
				/// <summary>
				/// Object type
				/// </summary>
				public readonly ObjectType Type;

				public ObjectID(Interface.Struct.GameObjectID id) : this()
				{
					Location = new Technite.CompressedLocation(id.location).CellID;
					IsGhost = id.isGhost;
					Type = id.type;
				}

				public override int GetHashCode()
				{
					return (((Location.GetHashCode() * 17 + 13) | Type.GetHashCode()) * 17 + 13) | IsGhost.GetHashCode();
				}

				public static bool operator==(ObjectID a, ObjectID b)
				{
					return a.Location == b.Location && a.Type == b.Type && a.IsGhost == b.IsGhost;
				}
				public static bool operator!=(ObjectID a, ObjectID b)
				{
					return !(a == b);
				}

				public override bool Equals(object obj)
				{
					return obj is ObjectID && ((ObjectID)obj == this);
				}

				public override string ToString()
				{
					string rs = Type + " @"+Location;
					if (IsGhost)
						rs += " (ghost)";
					return rs;
				}
			}
			public readonly ObjectID ID;
			/// <summary>
			/// Game round that this object was created in
			/// </summary>
			public readonly UInt32	BirthRound;
			/// <summary>
			/// Height of the local object in layers. Most objects are one layer high, but two-layer high buildings exist as well.
			/// </summary>
			public readonly byte Height;
			/// <summary>
			/// Indicates that this object covers horizontally neighboring cells as well. Objects may overlap in these cells.
			/// </summary>
			public readonly bool IsBroad;
			/// <summary>
			/// Indicates that this object is owned by the local faction. The identity of the faction of hostile objects cannot be determined.
			/// </summary>
			public readonly bool IsMine;
			/// <summary>
			/// Name of the class this object was spawned from
			/// </summary>
			public readonly string ClassName;

			public GameObject(Interface.Struct.GameObject other)
			{
				BirthRound = other.birthRound;
				ID = new ObjectID(other.id);
				Height = other.height;
				IsBroad = other.isBroad;
				IsMine = other.isMine;
				ClassName = other.className;
			}

			public int Age { get { return (int)(Session.roundNumber - BirthRound); } }

			public override bool Equals(object obj)
			{
				return obj is GameObject && ((GameObject)obj == this);
			}
			public override int GetHashCode()
			{
				return ID.GetHashCode();
			}

			public static bool operator==(GameObject a, GameObject b)
			{
				return a.ID == b.ID;
			}
			public static bool operator!=(GameObject a, GameObject b)
			{
				return !(a == b);
			}
			public override string ToString()
			{
				return (IsMine ? "My ":"")+ ID + " [age " + Age + "], class=" + ClassName;
			}
		}

		/// <summary>
		/// Control marker placed by the player.
		/// What it does depends on the technite programming
		/// </summary>
		public struct ControlMarker
		{
			/// <summary>
			/// User-definable type index in the range 0-9.
			/// Implementation is technite-dependent
			/// </summary>
			public readonly byte			TypeIndex;
			/// <summary>
			/// Geometric radius of this marker. Point markers have radius 0
			/// </summary>
			public readonly float			Radius;
			/// <summary>
			/// Location of this marker in the world
			/// </summary>
			public readonly Grid.CellID		Location;
			/// <summary>
			/// Game round that this marker was created in
			/// </summary>
			public readonly UInt32			BirthRound;

			public int Age { get { return (int)(Session.roundNumber - BirthRound); } }


			public ControlMarker(Interface.Struct.ControlMarker other)
			{
				BirthRound = other.birthRound;
				Location = new Technite.CompressedLocation(other.location).CellID;
				Radius = other.radius;
				TypeIndex = other.typeIndex;
			}

			public override string ToString()
			{
				return Location + " [age " + Age + "], type=" + TypeIndex + ", Radius=" + Radius;
			}

		}



		public static List<GameObject> All { get; private set; }
		public static List<ControlMarker> ControlMarkers { get; private set; }


		static Objects()
		{
			All = new List<GameObject>();
			ControlMarkers = new List<ControlMarker>();
		}

		public static void		FlushAllData()
		{
			All.Clear();
			ControlMarkers.Clear();
		}


		public static void Add(Interface.Struct.GameObject obj)
		{
			GameObject nobj = new GameObject(obj);
			Out.Log(Significance.Low, "Added game object " + nobj);
			All.Add(nobj);
		}

		public static void Remove(Interface.Struct.GameObjectID obj)
		{
			GameObject.ObjectID id = new GameObject.ObjectID(obj);

			for (int i = 0; i < All.Count; i++)
			{
				if (All[i].ID == id)
				{
					Out.Log(Significance.Low, "Removed game object " + id);
					All.RemoveAt(i);
					return;
				}
			}
			Out.Log(Significance.Unusual, "Unable to locate game object '" + id + "'");
		}

		public static void Add(Interface.Struct.ControlMarker marker)
		{
			ControlMarker nmarker = new ControlMarker(marker);
            Out.Log(Significance.Low, "Added control marker " + nmarker);
			ControlMarkers.Add(nmarker);
		}

		public static void RemoveControlMarker(UInt32 markerLocation)
		{
			Grid.CellID id = new Technite.CompressedLocation(markerLocation).CellID;

			for (int i = 0; i < ControlMarkers.Count; i++)
			{
				if (ControlMarkers[i].Location == id)
				{
					Out.Log(Significance.Low, "Removed control marker " + id);
					ControlMarkers.RemoveAt(i);
					return;
				}
			}
			Out.Log(Significance.Unusual, "Unable to locate control marker '" + id + "'");
		}

	}
}
