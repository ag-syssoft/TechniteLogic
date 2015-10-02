using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;


namespace Math3D
{
    /// <summary>
    /// Binary bounding volume hierarchy.
    /// Each node is either an inner node, containing exactly two children, or a leaf node, containing up to 4 objects.
    /// </summary>
    /// <typeparam name="T">Contained type. No restrictions, no requirements. Bounding volumes are supplied separately.</typeparam>
    public class BoundingVolumeHierarchy<T>
    {
        private bool isLocked = false;



        /// <summary>
        /// Tuple that contains both an element and its current bounding box.
        /// Primarily used internally, but may be used to more efficiently pass new entries to the hierarchy.
        /// </summary>
        public struct Entry
        {
            public readonly T Element;
            public readonly Box BoundingBox;
            public Entry(T element, Box box)
            {
                BoundingBox = box;
                Element = element;
            }
        }

        private class Node
        {
            public readonly bool IsLeaf;
            public readonly Box BoundingBox;


            public Node(bool isLeaf, Box boundingBox)
            {
                IsLeaf = isLeaf;
                BoundingBox = boundingBox;
            }

        }

        private class LeafNode : Node
        {
            public LeafNode(Box boundingBox) : base(true, boundingBox) { }

            public List<Entry> Elements = new List<Entry>();

            public IEnumerable<T> PureElements()
            {
                foreach (Entry e in Elements)
                    yield return e.Element;
            }

            public override String ToString()
            {
                return "Leaf(" + BoundingBox+")";
            }
        }

        private class InnerNode : Node
        {
            public InnerNode(Box boundingBox) : base(false, boundingBox) { }
            public Node     Child0,
                            Child1;
            public int      Axis;

            public override String ToString()
            {
                return "Inner(axis " + Axis + ", " + BoundingBox + ")";
            }

        }

        private Node _root;

        private List<Entry> _allItems = new List<Entry>();

        public void RebuildIfNeeded()
        {
            lock (this)
            {
                if (_root == null && _allItems.Count > 0)
                    ForceReconstruction();
            }
        }


        public void RuntimeInsert(T element, Box space)
        {
            lock(_allItems)
            {
                _allItems.Add(new Entry(element, space));
                if (isLocked)
                    _root = null;
                else
                    ForceReconstruction();
            }
        }
		public void LoadInsert(T element, Box space)
		{
			lock (_allItems)
			{
				_allItems.Add(new Entry(element, space));
				_root = null;
			}
		}

        public void RuntimeInsert(IEnumerable<Entry> entries)
        {
            lock (_allItems)
            {
                _allItems.AddRange(entries);
                if (isLocked)
                    _root = null;
                else
                    ForceReconstruction();
            }
        }
		public void LoadInsert(IEnumerable<Entry> entries)
		{
			lock (_allItems)
			{
				_allItems.AddRange(entries);
				_root = null;
			}
		}
		public void RuntimeRemove(T element)
        {
            lock(_allItems)
                for (int i = 0; i < _allItems.Count; i++)
                    if (object.ReferenceEquals(_allItems[i].Element,element))
                    {
                        _allItems.RemoveAt(i);
                        if (isLocked)
                            _root = null;
                        else
                            ForceReconstruction();
                        return;
                    }
        }
		public void LoadRemove(T element)
		{
			lock (_allItems)
				for (int i = 0; i < _allItems.Count; i++)
					if (object.ReferenceEquals(_allItems[i].Element, element))
					{
						_allItems.RemoveAt(i);
						_root = null;
						return;
					}
		}

        private class Bucket
        {
            public List<Entry>  Elements = new List<Entry>();
            public Box          BoundingBox = new Box(new Range(float.MaxValue, float.MinValue));
            public float        VolumeUntilThis = 0,
                                VolumeFromThis = 0;

            public void         Reset()
            {
                BoundingBox = new Box(new Range(float.MaxValue, float.MinValue));
                Elements.Clear();
                VolumeUntilThis = 0;
                VolumeFromThis = 0;
            }

        };


        private static Node BuildNode(List<Entry> elements, Bucket[] buckets)
        {
            Box boundingBox = new Box(new Range(float.MaxValue, float.MinValue));
            foreach (Entry e in elements)
                boundingBox.Include(e.BoundingBox);
            if (elements.Count <= 4)
            {
                LeafNode leaf = new LeafNode(boundingBox);
                leaf.Elements = elements;
                return leaf;
            }

            InnerNode innerNode = new InnerNode(boundingBox);

            Box centerVolume = new Box(new Range(float.MaxValue, float.MinValue));
            foreach (Entry e in elements)
                centerVolume.Include(e.BoundingBox.Center);


            float minVolume = float.MaxValue;
            int useAxis = -1;
            int splitAtBucket = 0;

            for (int axis = 0; axis < 3; axis++)
            {
                Range axisRange = centerVolume[axis];
                float extend = axisRange.Extend;
                if (extend == 0)
                    continue;
                    //extend = 1.0f;
                foreach (Entry e in elements)
                {
                    Bucket bucket = buckets[Math.Min(buckets.Length - 1, (int)((e.BoundingBox[axis].Center - axisRange.Min) / extend * buckets.Length))];
                    bucket.Elements.Add(e);
                    bucket.BoundingBox.Include(e.BoundingBox);
                }
                Box bounds = buckets[0].BoundingBox;
                float volume = bounds.Volume;
                buckets[0].VolumeUntilThis = volume;
                for (int i = 1; i < buckets.Length; i++)
                {
                    if (buckets[i].Elements.Count > 0)
                    {
                        bounds.Include(buckets[i].BoundingBox);
                        volume = bounds.Volume;
                    }
                    buckets[i].VolumeUntilThis = volume;
                   // Debug.WriteLine("volumeUntil(axis " + axis + ", bucket " + i + ") = " + volume);

                }
                bounds = buckets[buckets.Length-1].BoundingBox;
                volume = bounds.Volume;
                buckets[buckets.Length - 1].VolumeFromThis = volume;
                for (int i = buckets.Length - 2; i > -1; i--)
                {
                    if (buckets[i].Elements.Count > 0)
                    {
                        bounds.Include(buckets[i].BoundingBox);
                        volume = bounds.Volume;
                    }
                    buckets[i].VolumeFromThis = volume;
                    //Debug.WriteLine("volumeFrom(axis " + axis + ", bucket " + i + ") = " + volume);
                }

                for (int b = 1; b < buckets.Length; b++)
                {
                    volume = buckets[b-1].VolumeUntilThis + buckets[b].VolumeFromThis;
                    if (volume < minVolume)
                    {
                        minVolume = volume;
                        useAxis = axis;
                        splitAtBucket = b;
                    }
                    //Debug.WriteLine("volume(axis " + axis + ", split " + b + ") = " + volume);
                }
                foreach (Bucket b in buckets)
                    b.Reset();
            }
            if (useAxis == -1)
            {
                LeafNode leaf = new LeafNode(boundingBox);
                leaf.Elements = elements;
                return leaf;
            }

            Range axisRange2 = centerVolume[useAxis];
            float extend2 = axisRange2.Extend;
            Debug.Assert(extend2 != 0);

            List<Entry> lower = new List<Entry>(),
                        upper = new List<Entry>();
            foreach (Entry e in elements)
            {
                int bucketIndex = Math.Min(buckets.Length - 1, (int)((e.BoundingBox[useAxis].Center - axisRange2.Min) / extend2 * buckets.Length));
                if (bucketIndex >= splitAtBucket)
                    upper.Add(e);
                else
                    lower.Add(e);
            }

            innerNode.Axis = useAxis;
            innerNode.Child0 = BuildNode(lower,buckets);
            innerNode.Child1 = BuildNode(upper,buckets);
            return innerNode;
        }


        private static void walkNode(Node node, Box space, ref List<T> found)
        {
            if (node.IsLeaf)
            {
                LeafNode leaf = (LeafNode)node;
                if (space.Intersects(leaf.BoundingBox))
                    foreach (Entry e in leaf.Elements)
                        if (space.Intersects(e.BoundingBox))
                            found.Add(e.Element);
                return;
            }
            InnerNode n = (InnerNode)node;
            if (space.Intersects(n.BoundingBox))
            {
                walkNode(n.Child0, space, ref found);
                walkNode(n.Child1, space, ref found);
            }
        }
        private static void RecursivelyWalkNode(Node node, Vec3 point, ref List<T> found)
        {
            if (node.IsLeaf)
            {
                LeafNode leaf = (LeafNode)node;
                if (leaf.BoundingBox.Contains(point))
                    foreach (Entry e in leaf.Elements)
                        if (e.BoundingBox.Contains(point))
                            found.Add(e.Element);
                return;
            }
            InnerNode n = (InnerNode)node;
            if (n.BoundingBox.Contains(point))
            {
                RecursivelyWalkNode(n.Child0, point, ref found);
                RecursivelyWalkNode(n.Child1, point, ref found);
            }
        }

        private static void RecursivelyFindClosest(Node node, Vec3 point, ref T closest, ref float distance)
        {
            if (node.IsLeaf)
            {
                LeafNode leaf = (LeafNode)node;
                if (leaf.BoundingBox.Contains(point))
                    foreach (Entry e in leaf.Elements)
                        if (e.BoundingBox.Contains(point))
                        {
                            float d = Vec.QuadraticDistance(e.BoundingBox.Center, point);
                            if (d < distance)
                            {
                                distance = d;
                                closest = e.Element;
                            }
                        }
                return;
            }
            InnerNode n = (InnerNode)node;
            if (n.BoundingBox.Contains(point))
            {
                RecursivelyFindClosest(n.Child0, point, ref closest, ref distance);
                RecursivelyFindClosest(n.Child1, point, ref closest, ref distance);
            }
        }


        private static void RecursivelyWalkNode(Node node, Box space, ref List<T> found, ref int numTests)
        {
            if (node.IsLeaf)
            {
                LeafNode leaf = (LeafNode)node;
                numTests++;
                if (space.Intersects(leaf.BoundingBox))
                {
                    numTests += leaf.Elements.Count;
                    foreach (Entry e in leaf.Elements)
                        if (space.Intersects(e.BoundingBox))
                            found.Add(e.Element);
                }
                return;
            }
            InnerNode n = (InnerNode)node;
            numTests++;
            if (space.Intersects(n.BoundingBox))
            {
                RecursivelyWalkNode(n.Child0, space, ref found, ref numTests);
                RecursivelyWalkNode(n.Child1, space, ref found, ref numTests);
            }
        }
        private static void WalkNode(Node node, Vec3 point, ref List<T> found, ref int numTests)
        {
            if (node.IsLeaf)
            {
                LeafNode leaf = (LeafNode)node;
                numTests++;
                if (leaf.BoundingBox.Contains(point))
                {
                    numTests+= leaf.Elements.Count;
                    foreach (Entry e in leaf.Elements)
                        if (e.BoundingBox.Contains(point))
                            found.Add(e.Element);
                }
                return;
            }
            InnerNode n = (InnerNode)node;
            numTests++;
            if (n.BoundingBox.Contains(point))
            {
                WalkNode(n.Child0, point, ref found, ref numTests);
                WalkNode(n.Child1, point, ref found, ref numTests);
            }
        }
        private void TraceWalkNode(Node node, Box space, int indent=0)
        {
            PrintLine(indent, node.ToString());
            PrintLine(indent, "{");
                if (node.IsLeaf)
                {
                    if (space.Intersects(((LeafNode)node).BoundingBox))
                    {
                        PrintLine(indent + 1, "space lookup success. returning the following...");
                    }
                    else
                    {
                        PrintLine(indent + 1, "space lookup failed.");
                        PrintLine(indent + 1, "comparing " + ((LeafNode)node).BoundingBox + " with " + space);

                        PrintLine(indent + 1, "NOT returning the following...");
                         
                    }

                    foreach (Entry e in ((LeafNode)node).Elements)
                        PrintLine(indent + 1, e.Element + " at " + e.BoundingBox.Center);

                }
                else
                {
                    InnerNode n = (InnerNode)node;
                    if (space.Intersects(n.BoundingBox))
                    {
                        TraceWalkNode(n.Child0, space, indent + 1);
                        TraceWalkNode(n.Child1, space, indent + 1);
                    }
                }
            PrintLine(indent, "}");
        }

        private void TraceWalkNode(Node node, Vec3 point, int indent = 0)
        {
            PrintLine(indent, node.ToString());
            PrintLine(indent, "{");
            if (node.IsLeaf)
            {
                if (((LeafNode)node).BoundingBox.Contains(point))
                {
                    PrintLine(indent + 1, "point lookup success. returning the following...");
                }
                else
                {
                    PrintLine(indent + 1, "point lookup failed.");
                    PrintLine(indent + 1, "testing " + ((LeafNode)node).BoundingBox + ".Contains(" + point+")");

                    PrintLine(indent + 1, "NOT returning the following...");

                }

                foreach (Entry e in ((LeafNode)node).Elements)
                    PrintLine(indent + 1, e.Element + " at " + e.BoundingBox.Center);

            }
            else
            {
                InnerNode n = (InnerNode)node;
                if (n.BoundingBox.Contains(point))
                {
                    TraceWalkNode(n.Child0, point, indent + 1);
                    TraceWalkNode(n.Child1, point, indent + 1);
                }
                else
                {
                    PrintLine(indent + 1, "point check failed. not traversing children.");
                    PrintLine(indent + 1, "testing " + n.BoundingBox + ".Contains(" + point + ")");
                }

            }
            PrintLine(indent, "}");
        }

        private static void Indent(int indent)
        {
            for (int i = 0; i < indent; i++)
                Debug.Write('\t');
        }

        private static void PrintLine(int indent, string line)
        {
            Indent(indent);
            Debug.WriteLine(line);
        }



        private static void printNode(Node node, int indent)
        {
            PrintLine(indent, node.ToString());
            PrintLine(indent, "{");
            if (node.IsLeaf)
            {
                LeafNode leaf = (LeafNode)node;
                foreach (Entry e in leaf.Elements)
                    PrintLine(indent + 1, e.Element+ " at "+e.BoundingBox.Center);
            }
            else
            {
                InnerNode inner = (InnerNode)node;
                printNode(inner.Child0, indent + 1);
                printNode(inner.Child1, indent + 1);
            }
            PrintLine(indent, "}");
        }

        public void ForceReconstruction(bool verbose = false)
        {
            lock(_allItems)
            {
                if (_allItems.Count == 0)
                {
                    if (verbose)
                        Debug.WriteLine("Tree is empty");
                    _root = null;
                    return;
                }
                Bucket[] buckets = new Bucket[16]
                {
                    new Bucket(), new Bucket(), new Bucket(), new Bucket(),
                    new Bucket(), new Bucket(), new Bucket(), new Bucket(),
                    new Bucket(), new Bucket(), new Bucket(), new Bucket(),
                    new Bucket(), new Bucket(), new Bucket(), new Bucket()
                };

                _root = BuildNode(_allItems,buckets);
                if (verbose)
                    printNode(_root,0);
            }
        }



        public void TraceLookup(Box space)
        {
            Node root = _root;
            if (root != null)
                TraceWalkNode(root, space);
        }

        public void TraceLookup(Vec3 point)
        {
            Node root = _root;
            if (root != null)
                TraceWalkNode(root, point);
        }

        public void Lookup(Box space, ref List<T> found)
        {
            Node root = _root;  //might be rebuilt a moment after this
            if (root == null)
                return;
            walkNode(root, space, ref found);
        }
        public void Lookup(Vec3 position, ref List<T> lookupResult)
        {
            Node root = _root;  //might be rebuilt a moment after this
            if (root == null)
                return;
            RecursivelyWalkNode(root, position, ref lookupResult);
        }
        public void Lookup(Box space, ref List<T> found, out int numTests)
        {
            Node root = _root;  //might be rebuilt a moment after this
            numTests = 0;
            if (root == null)
                return;
            RecursivelyWalkNode(root, space, ref found, ref numTests);
        }
        public void Lookup(Vec3 position, ref List<T> lookupResult, out int numTests)
        {
            Node root = _root;  //might be rebuilt a moment after this
            numTests = 0;
            if (root == null)
                return;
            WalkNode(root, position, ref lookupResult, ref numTests);
        }
        public bool LookupClosest(Vec3 position, ref T result)
        {
            Node root = _root;  //might be rebuilt a moment after this
            if (root == null)
                return false;
            float distance = float.MaxValue;
            RecursivelyFindClosest(root, position, ref result, ref distance);
            return distance < float.MaxValue;
        }
        public void Clear()
        {
            lock (this)
            {
                _allItems.Clear();
                _root = null;
            }
        }

		public void Invalidate()
		{
			_root = null;
		}

        public void LockReconstruction()
        {
            isLocked = true;
        }

        public void UnlockReconstruction()
        {
            isLocked = false;
            RebuildIfNeeded();
        }
    }
}
