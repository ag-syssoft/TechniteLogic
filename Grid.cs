using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Math3D;
using System.Diagnostics;
using Logging;

namespace TechniteLogic
{
	public static class Grid
	{

		public enum Content
		{
			Unknown,
			Granite,
			Earth,
			Grass,
			Rock,
			Sand,
			Snow,
			Foundation,
			Road,
			Lava,
			Technite,
			Water,
			Clear,


			Undefined //client-only. used for debugging
		}



		public static class Graph
		{
			public struct Node
			{
				public readonly uint[] Neighbors;
				public readonly Vec3 StackBase;
				public readonly Vec3 StackDirection;

				public Node(uint[] neighbors, Vec3 stackBase, Vec3 stackDirection)
				{
					Neighbors = neighbors;
					StackBase = stackBase;
					StackDirection = stackDirection;
				}

				public static implicit operator Node(Interface.Struct.GridNode node)
				{
					return new Node(node.neighbors, node.stackBase, node.stackDirection);
				}

				public Vec3 GetLocation(int layerIndex)
				{
					return StackBase + StackDirection * (CellStack.HeightPerLayer * layerIndex);
				}
			}

			public static Node[] Nodes { get; private set; }

			public static Vec3 GetLocation(CellID loc)
			{
				return Nodes[loc.StackID].GetLocation(loc.Layer);
			}

			private static List<Node> nodeBuffer;


			public static void AddChunk(Interface.Struct.NodeChunk chunk)
			{
				//Out.Log(Significance.Common, "Received node-chunk, containing "+chunk.nodes.Length+" nodes");
				if (nodeBuffer == null)
					nodeBuffer = new List<Node>();
				for (int i = 0; i < chunk.nodes.Length; i++)
					nodeBuffer.Add(chunk.nodes[i]);
				if (chunk.isLast)
				{
					Out.Log(Significance.Common, "Received last node-chunk. Completing graph");
					Nodes = nodeBuffer.ToArray();
					nodeBuffer = null;
				}
			}

			internal static void FlushAllData()
			{
				Nodes = null;
				nodeBuffer = null;
			}
		}

		/// <summary>
		/// Checks whether or not a block is solid enough to support a technite standing on or next to it.
		/// Theoretically, technites themselves are solid, too, but they tend to die eventually, so they are excluded by default.
		/// Content.Unknown is excluded here, because Unknown may be water, depending on server configuration, which can not support technites.
		/// </summary>
		/// <param name="content">Content type to check</param>
		/// <param name="includeTechnites">Set true to also consider technites as solid</param>
		/// <returns>True, if the specified content type is solid enough to support a technite, false otherwise</returns>
		public static bool IsSolid(Content content, bool includeTechnites=false)
		{
			return content == Content.Earth 
				|| content == Content.Foundation 
				|| content == Content.Granite 
				|| content == Content.Grass 
				|| content == Content.Road 
				|| content == Content.Rock
				|| content == Content.Sand
				|| content == Content.Snow 
				|| (includeTechnites && content == Content.Technite)
				;
		}

		/// <summary>
		/// Location-based version of <see cref="IsSolid(Content content, bool includeTechnites)"/>
		/// </summary>
		/// <param name="location">Location to check the content of</param>
		/// <param name="includeTechnites">Set true to also consider technites as solid</param>
		/// <returns>True, if the determined content type is solid enough to support a technite, false otherwise</returns>
		public static bool IsSolid(CellID location, bool includeTechnites=false)
		{
			return IsSolid(World.GetCell(location).content,includeTechnites);
		}




		public class CellStack
		{
			public static float HeightPerLayer
			{
				get; private set;
			}

			public static int LayersPerStack
			{
				get; private set;
			}

			private static bool isSetUp = false;

			public static void StaticReset()
			{
				isSetUp = false;
				HeightPerLayer = 0f;
				LayersPerStack = 0;
			}


			public static void Setup(float heightPerLayer, int numLayersPerStack)
			{
				Debug.Assert(!isSetUp);
				isSetUp = true;
				HeightPerLayer = heightPerLayer;
				LayersPerStack = numLayersPerStack;
				Out.Log(Significance.Common, "CellStack.Setup: height per layer="+heightPerLayer+", layers per stack="+numLayersPerStack);
            }

			internal bool IsShaded(uint layer)
			{
				for (uint l = layer + 1; l < LayersPerStack; l++)
					if (volumeCell[l].content != Content.Clear)
						return true;
				return false;
			}

			public Cell[] volumeCell = new Cell[LayersPerStack];


			public CellStack()
			{
				for (int i = 0; i < volumeCell.Length; i++)
					volumeCell[i] = new Cell() { content = Content.Undefined, techniteFactionID = 0 };
			}

            public struct Cell
            {
				public Content content;
				public byte techniteFactionID;


				internal void ApplyContentDelta(byte value)
				{
					if (value != 0 || content == Content.Undefined)
						content = (Content)(value);
				}

				internal void ApplyTechniteDelta(byte value)
				{
					if (value != 0)
						techniteFactionID = (byte)(value - 1);
				}
			}
		}

		public static class World
		{

			public static void Create()
			{
				if (CellStacks == null)
				{
					CellStacks = new CellStack[Graph.Nodes.Length];
					for (int i = 0; i < CellStacks.Length; i++)
						CellStacks[i] = new CellStack();
				}
			}

			public static CellStack[] CellStacks
			{
				get; private set;
			}

			public static CellStack.Cell FloorCell { get; private set; }
			public static CellStack.Cell CeilingCell { get; private set; }


			private static void ApplyDeltaField(Interface.Struct.GridDelta delta, Interface.Struct.GridDeltaBlock[] blocks, Action<CellStack, uint, byte> action)
			{
				if (blocks.Length != 0)
				{
					//Out.Log(Significance.Low, "  Applying " + blocks.Length + " blocks...");
					uint at = 0;
					foreach (var block in blocks)
					{
						for (int i = 0; i < block.repitition; i++)
						{
							uint stackIndex = (at % delta.nodeCount) + delta.nodeOffset;
							uint layer = at / delta.nodeCount;

							CellStack stack = CellStacks[stackIndex];
							action(stack, layer, block.value);

							at++;
						}
					}
					Debug.Assert(at == delta.nodeCount * CellStack.LayersPerStack);
				}
			}


			public static void ApplyDelta(Interface.Struct.GridDelta delta)
			{
				if (CellStacks == null)
				{
					Out.Log(Significance.ProgramFatal, "World not initialized. Cannot apply delta.");
					return;
				}
				if (delta.nodeOffset + delta.nodeCount > Graph.Nodes.Length)
				{
					Out.Log(Significance.ProgramFatal, "Invalid node range for world delta: ["+delta.nodeOffset+","+(delta.nodeOffset+delta.nodeCount)+") /"+ Graph.Nodes.Length);
					return;
				}
				Out.Log(Significance.Common, "Applying world delta at range "+delta.nodeOffset+"..."+(delta.nodeOffset+delta.nodeCount)+ " /"+Graph.Nodes.Length);


				ApplyDeltaField(delta, delta.contentBlocks, (stack, layer, value) => stack.volumeCell[layer].ApplyContentDelta(value));
				ApplyDeltaField(delta, delta.techniteFactionBlocks, (stack, layer, value) => stack.volumeCell[layer].ApplyTechniteDelta(value));

			}

			internal static void Setup(Content coreContent)
			{
				FloorCell = new CellStack.Cell(){content = coreContent};
				CeilingCell = new CellStack.Cell(){content = Content.Clear};
				Out.Log(Significance.Common, "Set world core content to "+FloorCell.content);
			}

			/// <summary>
			/// Retrieves the cell associated with the specified cell id.
			/// The stack id of the specified cell must be valid, its layer may fall outside the valid range, resulting in CellStack.FloorCell or CellStack.CeilingCell to be returned instead.
			/// </summary>
			/// <param name="cell"></param>
			/// <returns></returns>
			public static CellStack.Cell GetCell(CellID cell)
			{
				if (cell.Layer < 0)
					return FloorCell;
				if (cell.Layer >= CellStack.LayersPerStack)
					return CeilingCell;
				return CellStacks[cell.StackID].volumeCell[cell.Layer];
			}

			internal static void FlushAllData()
			{
				CellStacks = null;
				FloorCell = new CellStack.Cell();
				CeilingCell = new CellStack.Cell();
				CellStack.StaticReset();
            }
		}

		

		public static void ApplyDelta(Interface.Struct.GridDelta delta)
		{
			World.Create();
			World.ApplyDelta(delta);
		}



		/// <summary>
		/// Cell descriptor relative to a given cell. Some relative targets may be valid only from specific cells.
		/// </summary>
		public struct RelativeCell
		{
			/// <summary>
			/// Linear index of the neighboorhood entry of the source cell id.
			/// Supported values are 0..14 for normal neighborhood, 15 for none (i.e. self),
			/// and uint.MaxValue to describe an invalid/unset relative cell.
			/// In order for a relative cell to be valid, NeighborIndex must be either 0xF or,
			/// relative to a given HCellID 'source', smaller than Grid.Graph.Nodes[source.StackID].Neighbors.Length.
			/// It is generally discouraged to use these values explicitly from within any logic implementation,
			/// because the underlying principles may change.
			/// </summary>
			public readonly uint NeighborIndex;

			/// <summary>
			/// Height delta value. Valid values are -1, 0, and +1.
			/// It is generally discouraged to use these values explicitly from within any logic implementation,
			/// because the underlying principles may change.
			/// </summary>
			public readonly int HeightDelta;

			/// <summary>
			/// Neighbor index to be used when addressing own stack index. If used in combination with a height delta value of 0, the target describes the local cell itself
			/// </summary>
			public static uint ThisStackNeighborIndex = 0xF;

			public RelativeCell(uint neighborIndex, int heightDelta)
			{
				if (neighborIndex > 0xF && neighborIndex != uint.MaxValue)
					throw new ArgumentOutOfRangeException("RelativeTarget expects the neighbor index to be passed as first parameter. Maximum degree is 15, but passed parameter is "+neighborIndex);
				if (neighborIndex == uint.MaxValue && heightDelta != 0)
					throw new ArgumentException("If neighbor index is uint.MaxValue, height delta must be set to 0");
				if (heightDelta > 1 || heightDelta < -1)
					throw new ArgumentOutOfRangeException("RelativeTarget expects a relative height delta to be passed as second parameter. This value must be in the range [-1,+1], but given value is " + heightDelta);
				NeighborIndex = neighborIndex;
				HeightDelta = heightDelta;
			}

			/// <summary>
			/// Constructs a relative target pointing only up or down. This is identical to calling RelativeCell(ThisStackNeighborIndex,heightDelta)
			/// </summary>
			/// <param name="heightDelta"></param>
			public RelativeCell(int heightDelta)
			{
				if (heightDelta > 1 || heightDelta < -1)
					throw new ArgumentOutOfRangeException("RelativeTarget expects a relative height delta to be passed as second parameter. This value must be in the range [-1,+1], but given value is " + heightDelta);
				NeighborIndex = ThisStackNeighborIndex;
				HeightDelta = heightDelta;
			}

			public override bool Equals(object obj)
			{
				return (obj is RelativeCell) && ((RelativeCell)obj) == this;
			}

			public static bool operator ==(RelativeCell a, RelativeCell b)
			{
				return a.HeightDelta == b.HeightDelta && a.NeighborIndex == b.NeighborIndex;
			}
			public static bool operator !=(RelativeCell a, RelativeCell b)
			{
				return a.HeightDelta != b.HeightDelta || a.NeighborIndex != b.NeighborIndex;
			}

			public override int GetHashCode()
			{
				int hash = 17;
				hash += NeighborIndex.GetHashCode();
				hash *= 31;
				hash += HeightDelta.GetHashCode();
				return hash;
			}

			public override string ToString()
			{
				if (NeighborIndex == ThisStackNeighborIndex)
				{
					return "<> |" + HeightDelta;
                }
				return "->" + NeighborIndex + " |" + HeightDelta;
			}

			public static Grid.CellID operator+(Grid.CellID cell, RelativeCell t)
			{
				if (t.NeighborIndex == ThisStackNeighborIndex)
					return new Grid.CellID(cell.StackID, cell.Layer + t.HeightDelta);
				return new Grid.CellID(Grid.Graph.Nodes[cell.StackID].Neighbors[t.NeighborIndex], cell.Layer + t.HeightDelta);
			}

			public static readonly RelativeCell Invalid = new RelativeCell(uint.MaxValue, 0);
			public static readonly RelativeCell Self = new RelativeCell(0);
        }



		/// <summary>
		/// Volume cell identifier using (unsigned) integers to describe the layer (0..CellStack.LayersPerStack-1) this cell resides on
		/// </summary>
		public struct CellID
		{
			public readonly uint StackID;

			/// <summary>
			/// Residual layer of this cell in the range [0,CellStack.LayersPerStack)
			/// Theoretically, a uint would be more appropriate. Practically applying relative targets causes too many weird cases with uint.
			/// </summary>
			public readonly int Layer;

			public static readonly CellID Invalid = new CellID(uint.MaxValue,int.MaxValue);

			public CellID(uint stackID, int layer)
			{
				StackID = stackID;
				Layer = layer;
			}


			/// <summary>
			/// Describes whether or not the local cell is a valid part of the currently known world.
			/// </summary>
			public bool IsValid {  get { return StackID < Graph.Nodes.Length && Layer >= 0 && Layer < CellStack.LayersPerStack; } }

			public override int GetHashCode()
			{
				int rs = 19;
				rs += StackID.GetHashCode();
				rs *= 37;
				rs += Layer.GetHashCode();
				return rs;
			}

			public override bool Equals(object obj)
			{
				if (!(obj is CellID))
					return false;
				return ((CellID)obj) == this;
			}

			public static bool operator==(CellID a, CellID b)
			{
				return a.StackID == b.StackID && a.Layer == b.Layer;
			}

			public static bool operator !=(CellID a, CellID b)
			{
				return a.StackID != b.StackID || a.Layer != b.Layer;
			}

			public override string ToString()
			{
				return StackID + "[" + Layer + "]";
			}

			/// <summary>
			/// Enumerates all valid/existing neighboring cells, including up, down, and diagonal
			/// </summary>
			/// <returns></returns>
			public IEnumerable<CellID>			GetNeighbors()
			{
				int lower = Layer > 0 ? Layer-1 : Layer,
					upper = Layer + 1 < CellStack.LayersPerStack ? Layer+1 : Layer;
				var neighbors = Grid.Graph.Nodes[StackID].Neighbors;
				for (int layer = lower; layer <= upper; layer++)
				{
					foreach (var n in neighbors)
					{
						yield return new CellID(n, layer);
					}
					if (layer != Layer)
						yield return new CellID(StackID,layer);
				}
			}

			/// <summary>
			/// Enumerates all existing horizontal neighbors, excluding up, down, and diagonal
			/// </summary>
			/// <returns></returns>
			public IEnumerable<CellID>			GetHorizontalNeighbors()
			{
				var neighbors = Grid.Graph.Nodes[StackID].Neighbors;
				foreach (var n in neighbors)
				{
					yield return new CellID(n, Layer);
				}
			}

            public IEnumerable<RelativeCell> GetRelativeUpperNeighbors()
            {
                int /*lower = Layer > 0 ? -1 : 0,*/
                    upper = Layer + 1 < CellStack.LayersPerStack ? 1 : 0;
                uint numNeighbors = (uint)Grid.Graph.Nodes[StackID].Neighbors.Length;
                //for (int delta = lower; delta <= upper; delta++)
                //{
                    for (uint i = 0; i < numNeighbors; i++)
                    {
                        yield return new RelativeCell(i, upper);
                    }
                    if (upper != 0)
                        yield return new RelativeCell(upper);
                //}
            }

            /// <summary>
            /// Describes the neighbor cell directly atop this cell. Note that this cell might not be part of the volume.
            /// Use IsValid to determine the validity if necessary.
            /// World.GetCell will always work with this value, however
            /// </summary>
            public CellID	TopNeighbor { get { return new CellID(StackID,Layer+1); } }

			/// <summary>
			/// Describes the neighbor cell directly beneath this cell. Note that this cell might not be part of the volume.
			/// Use IsValid to determine the validity if necessary.
			/// World.GetCell will always work with this value, however
			/// </summary>
			public CellID BottomNeighbor { get { return new CellID(StackID,Layer-1); } }

			/// <summary>
			/// Up-direction of the stack of this ID in world space
			/// </summary>
			public Vec3 UpDirection { get { return Graph.Nodes[StackID].StackDirection; } }

			/// <summary>
			/// Absolute world location of this cell ID in world space
			/// </summary>
			public Vec3 WorldPosition
			{
				get
				{
					var node = Graph.Nodes[StackID];
					return node.StackBase + node.StackDirection * CellStack.HeightPerLayer;
				}
			}

			/// <summary>
			/// Enumerates through all valid/existing neighboring cells, using relative descriptors
			/// </summary>
			/// <returns></returns>
			public IEnumerable<RelativeCell> GetRelativeNeighbors()
			{
				int lower = Layer > 0 ? -1 : 0,
					upper = Layer +1 < CellStack.LayersPerStack ? 1 : 0;
				uint numNeighbors = (uint)Grid.Graph.Nodes[StackID].Neighbors.Length;
				for (int delta = lower; delta <= upper; delta++)
				{
					for (uint i = 0; i < numNeighbors; i++)
					{
						yield return new RelativeCell(i,delta);
					}
					if (delta != 0)
						yield return new RelativeCell(delta);
				}
			}

            /// <summary>
			/// Enumerates through the only one valid/existing top neighboring cell, using relative descriptors
			/// </summary>
			/// <returns></returns>
            public IEnumerable<RelativeCell> GetRelativeUpperNeighbor()
            {
                int upper = Layer + 1 < CellStack.LayersPerStack ? 1 : 0;
                //uint numNeighbors = (uint)Grid.Graph.Nodes[StackID].Neighbors.Length;
                //for (int delta = lower; delta <= upper; delta++)
                //{
                    //for (uint i = 0; i < numNeighbors; i++)
                    //{
                        //yield return new RelativeCell(i, delta);
                    //}
                    //if (delta != 0)
                        yield return new RelativeCell(upper);
                //}
            }
        }

		internal static void BeginSession(float heightPerLayer, int numLayersPerStack)
		{
			CellStack.Setup(heightPerLayer, numLayersPerStack);
		}

		/// <summary>
		/// Erases all global grid data from the session.
		/// Use this to reset grid state to program default
		/// </summary>
		public static void FlushAllData()
		{
			World.FlushAllData();
			Graph.FlushAllData();
		}

	}
}
