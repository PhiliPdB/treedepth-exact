using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;

namespace TreeDepth {
	internal class Program {

		private static short _graphSize;
		private static short _graphEdges;
		private static List<short>[] _adjacencyList;

		/// <summary>
		/// Hashtable to link sub-graphs with previously calculated results.
		/// </summary>
		/// <remarks>
		/// The stored integers are divided in two sets of 16 bits where the
		/// last 8 bits always represent the computed result.
		/// If these last 8 bits are all ones (they represent the value 2^16),
		/// the first 8 bits store the value of the bestResult parameter.
		/// Otherwise the first 8 bits represent the root that was chosen to
		/// achieve this result.
		///
		/// Thus if result == 2^16:
		///		bestResult | result
		/// else:
		///		root       | result
		/// </remarks>
		private static Dictionary<BitArray, int> _table;

		private static short[] _nodesByDegree;

		// DFS Variables
		private static BitArray _visited;

		// Tree reconstruction
		private static short[] _nodeLocations;

		// Node ranking
		private static Dictionary<BitArray, short[]> _treeRanks;

		public static void Main(string[] args) {
#if DEBUG
			// Try to load file when needed
			if (args != null && args.Length > 0) {
				StreamReader inputFile;
				using (inputFile = new StreamReader(args[0])) {
					Console.SetIn(inputFile);
				}
			}
#endif

			// Load the graph
			ReadGraph();

			// Fill nodes by degree array and sort
			_nodesByDegree = new short[_graphSize];
			for (short i = 0; i < _graphSize; i++) {
				_nodesByDegree[i] = i;
			}
			// Sort the nodes by degree
			Array.Sort(_nodesByDegree, (n1, n2) => _adjacencyList[n2].Count.CompareTo(_adjacencyList[n1].Count));

			// Init memorization 'array'
			_table     = new Dictionary<BitArray, int>(new BitArrayEqualityComparer());
			// Init tree ranks dictionary
			_treeRanks = new Dictionary<BitArray, short[]>(new BitArrayEqualityComparer());

			// Run solver
			SubGraph fullGraph = new SubGraph(
				new BitArray(_graphSize, true),
				_graphSize,
				_graphEdges,
				1
			);

			// Try if a tree-depth below a certain number can be found.
			short currentTry = (short) (Math.Floor(Math.Log(fullGraph.size, 2)) + 2);

			int result = 0;
			_table[fullGraph.nodes] = short.MaxValue;
			while ((_table[fullGraph.nodes] & 0xFFFF) == short.MaxValue) {
				// Try to get the tree-depth
				result = TreeDepth(fullGraph, currentTry);

				// Increase the current try
				currentTry = (short) (currentTry * 1.5);
			}

			// Reconstruct tree
			_nodeLocations = new short[_graphSize];
			ConstructTreeDepthTree(fullGraph);

			// Write the results
			Console.WriteLine(result);
			foreach (short node in _nodeLocations) {
				Console.WriteLine(node);
			}

#if DEBUG
			Console.ReadLine();
#endif
		}

		public static int TreeDepth(SubGraph graph, short bestResult = short.MaxValue, short from = -1) {
			if (_table.ContainsKey(graph.nodes)) {
				int res = _table[graph.nodes];
				if ((res & 0xFFFF) < short.MaxValue
				    || (res >> 16) >= bestResult
				    ) {
					return res & 0xFFFF;
				}
			}

			// Graph is just a single node
			if (graph.size == 1) {
				return 1;
			}

			// Graph is a tree
			if (IsTree(graph)) {
				int log = (int) Math.Log(graph.size, 2) + 1;
				if (log > bestResult) {
					return short.MaxValue;
				}

				// A path is special type of tree
				if (IsPath(graph.nodes)) { // Graph is a path
					return log;
				}

				// Graph is not a path, but still a tree.

				// Find a root for the tree
				short treeRoot   = _adjacencyList[from].Find(v => graph.nodes[v]);
				// Create the space to write the ranks to
				short[] treeRank = new short[_graphSize];
				// Set the tree-ranks list
				_treeRanks[graph.nodes] = treeRank;
				// Get rank of the tree
				int rank = CriticalRankTree(CreateTree(graph.nodes, treeRoot), treeRank).GetMax();
				// Save the rank to the table
				_table[graph.nodes] = (from << 16) | rank;
				// Return the rank
				return rank;
			}

			// The current best result
			// NOTE: The tree-depth cannot be bigger than the amount of nodes in the graph.
			int result = Math.Min(bestResult, graph.size);
			// The root that we chose to achieve this result
			int root   = -1;

			foreach (short i in _nodesByDegree) {
				if (!graph.nodes[i]) continue;

				// Remove node i from the graph
				BitArray newNodes = new BitArray(graph.nodes) { [i] = false };

				int size = 0;
				// Go through connected components
				foreach (SubGraph cc in ConnectedComponents(i, newNodes)) {
					// The tree-depth if the component has te be at least the size of
					// a path that it contains.
					if (result < (short) Math.Log(cc.pathSize, 2) + 1) {
						size = short.MaxValue;
						break;
					}

					// NOTE: The current value of 'result' is our best solution yet
					int td = TreeDepth(cc, (short) (result - 1), i);
					
					// ReSharper disable once InvertIf
					if (td > size) {
						size = td;

						// If the current tree-depth is also bigger than our best result yet,
						// it is better to break the branch.
						if (td >= result) {
							break;
						}
					}
				}
				// Add the current node to the size
				size += 1;

				// ReSharper disable once InvertIf
				if (size <= result) { // Found a better result
					result = size;
					root   = i;
				}
			}


			if (root == -1) {
				// Set the root as the best result used to get this result
				root   = result;
				result = short.MaxValue;
			}

			_table[graph.nodes] = (root << 16) | result;
			// Return the result
			return result;
		}

		#region Input/output

		public static void ReadGraph() {
			int edgesLeft = 1;
			while (true) {
				// ReSharper disable once PossibleNullReferenceException
				string[] currentLine = Console.ReadLine().Split();

				switch (currentLine[0]) {

					case "p":
						// Initialize graph
						_graphSize  = short.Parse(currentLine[2]);
						_graphEdges = short.Parse(currentLine[3]);
						edgesLeft   = _graphEdges;

						_adjacencyList = new List<short>[_graphSize];
						for (int i = 0; i < _graphSize; i++) {
							_adjacencyList[i] = new List<short>();
						}

						// Initialize visited bitarray
						_visited  = new BitArray(_graphSize, false);

						break;

					case "c": // Comment
						continue;

					default:
						// Add vertex to adjacency list
						short from = (short) (short.Parse(currentLine[0]) - 1);
						short to   = (short) (short.Parse(currentLine[1]) - 1);
						_adjacencyList[from].Add(to);
						_adjacencyList[to].Add(from);

						if (--edgesLeft == 0) return;
						break;
				}
			}
		}

		/// <summary>
		/// Reconstruct the calculated tree, by backtracking the results
		/// </summary>
		/// <param name="graph"></param>
		public static void ConstructTreeDepthTree(SubGraph graph) {
			if (graph.size == 1) return;

			short currentNode = (short) (_table[graph.nodes] >> 16);
			
			BitArray newNodes = new BitArray(graph.nodes) { [currentNode] = false };

			foreach (SubGraph cc in ConnectedComponents(currentNode, newNodes)) {
				// Leafs aren't saved in the table.
				if (cc.size == 1) {
					_nodeLocations[FirstNode(cc.nodes)] = (short) (currentNode + 1);
					continue;
				}

				if (IsTree(cc)) {
					// Path is a special type of tree
					// Paths require a different reconstruction
					if (IsPath(cc.nodes)) {
						ConstructPath(cc.size, cc.nodes, currentNode);
						continue;
					}

					short treeRoot = _adjacencyList[currentNode].Find(v => cc.nodes[v]);
					_nodeLocations[treeRoot] = (short) (currentNode + 1);
					ConstructSubTree(CreateTree(cc.nodes, treeRoot), _treeRanks[cc.nodes]);
					continue;
				}

				_nodeLocations[_table[cc.nodes] >> 16] = (short) (currentNode + 1);
				ConstructTreeDepthTree(cc);
			}
		}

		/// <summary>
		/// Determine the path order, to prepare for construction of a tree from the path.
		/// </summary>
		/// <param name="size"></param>
		/// <param name="nodes"></param>
		/// <param name="parent"></param>
		private static void ConstructPath(int size, BitArray nodes, short parent) {
			short pathRoot = -1;
			short[] pathOrder = new short[size];

			// Get the root of the path
			for (short i = 0; i < _graphSize; i++) {
				if (nodes[i] && _adjacencyList[i].Count(n => nodes[n]) == 1) {
					pathRoot = i;
					break;
				}
			}
			// Set the path order
			pathOrder[0] = pathRoot;

			short previousNode = -1;
			short currentNode  = pathRoot;
			short currentIndex = 1;
			while (currentIndex < size) {
				short nextNode = _adjacencyList[currentNode].Find(n => nodes[n] && n != previousNode);
				previousNode = currentNode;
				currentNode = nextNode;
				
				pathOrder[currentIndex++] = currentNode;
			}

			ConstructPathDivide(parent, 0, size, pathOrder);
		}

		/// <summary>
		/// Construct the path based on the path order
		/// </summary>
		/// <param name="parent"></param>
		/// <param name="start"></param>
		/// <param name="end"></param>
		/// <param name="nodeOrder"></param>
		[SuppressMessage("ReSharper", "TailRecursiveCall")]
		private static void ConstructPathDivide(int parent, int start, int end, IReadOnlyList<short> nodeOrder) {
			if (start == end) return;

			int m = (start + end) >> 1;
			_nodeLocations[nodeOrder[m]] = (short) (parent + 1);
			ConstructPathDivide(nodeOrder[m], start, m, nodeOrder);
			ConstructPathDivide(nodeOrder[m], m + 1, end, nodeOrder);
		}

		/// <summary>
		/// Construct the tree-depth tree based on the tree-ranks
		/// </summary>
		/// <param name="tree"></param>
		/// <param name="treeRank"></param>
		private static void ConstructSubTree(Tree tree, short[] treeRank) {
			foreach (Tree child in tree.children) {
				if (treeRank[child.root] > treeRank[tree.root]) {
					short root = (short) (tree.root + 1);

					// Find out how high we should put the child
					
					short curParent = _nodeLocations[root - 1];
					while (// Make sure that `curRoot` is in the tree
						   curParent != 0
					       && treeRank[curParent - 1] != 0
						   // Check if the node should move up
					       && treeRank[child.root] > treeRank[curParent - 1]) {
						root = curParent;

						curParent = _nodeLocations[curParent - 1];
					}

					_nodeLocations[child.root] = _nodeLocations[root - 1];
					_nodeLocations[root - 1]   = (short) (child.root + 1);
				}
				else _nodeLocations[child.root] = (short) (tree.root + 1);

				ConstructSubTree(child, treeRank);
			}
		}


		#endregion

		#region Connected components

		/// <summary>
		/// Calculate the connected components of a set of nodes
		/// </summary>
		/// <param name="from"></param>
		/// <param name="nodes"></param>
		/// <returns></returns>
		public static List<SubGraph> ConnectedComponents(short from, BitArray nodes) {
			// Initialize arrays
			_visited.SetAll(false);
			List<SubGraph> connectedComponents = new List<SubGraph>();

			foreach (short node in _adjacencyList[from]) {
				if (!nodes[node] || _visited[node]) continue;
				
				(short size, short edges, short depth) = DFSUtil(node, nodes);
				// Collect visited nodes
				BitArray newCC = new BitArray(_visited);
				connectedComponents.ForEach(cc => newCC.Xor(cc.nodes));
				
				connectedComponents.Add(
					new SubGraph(newCC, size, (short) (edges >> 1), depth)
				);
			}

			return connectedComponents;
		}

		/// <summary>
		/// Run DFS on a set of nodes starting from the start node.
		/// </summary>
		/// <param name="startNode"></param>
		/// <param name="nodes"></param>
		/// <returns></returns>
		private static (short size, short edges, short depth) DFSUtil(int startNode, BitArray nodes) {
			_visited[startNode] = true;

			// Keep track of the size of the sub-graph
			short totalSize  = 1;
			short totalEdges = 0;
			short maxDepth   = 0;
			foreach (int i in _adjacencyList[startNode]) {
				if (!nodes[i]) continue;

				if (!_visited[i]) { // Not visited this node, so start DFS here
					(short size, short edges, short depth) = DFSUtil(i, nodes);

					totalSize  += size;
					totalEdges += (short) (1 + edges);
					if (depth > maxDepth) maxDepth = depth;
				}
				else { // Already visited this node, but still count the edge.
					totalEdges += 1;
				}
			}

			return (totalSize, totalEdges, (short) (maxDepth + 1));
		}

		#endregion

		#region Optimizations

		/// <summary>
		/// Check if a sub-graph forms a path
		/// </summary>
		/// <param name="nodes"></param>
		/// <returns></returns>
		private static bool IsPath(BitArray nodes) {
			for (int i = 0; i < _graphSize; i++) {
				if (!nodes[i]) continue;

				// Get the degree of this node
				short count = (short) _adjacencyList[i].Count(n => nodes[n]);

				if (count > 2) return false;
			}

			return true;
		}


		#region Node Ranking of trees

		/// <summary>
		/// Check if a sub-graph is a tree
		/// </summary>
		/// <param name="graph">The input graph.<br /> NOTE: This has to be a connected graph.</param>
		/// <returns></returns>
		private static bool IsTree(SubGraph graph) {
			// Check if the graph of n vertices has n - 1 edges.
			return graph.edges == graph.size - 1;
		}

		/// <summary>
		/// Convert a <see cref="BitArray"/> of nodes into a <see cref="Tree"/>
		/// </summary>
		/// <param name="nodes"></param>
		/// <param name="root"></param>
		/// <param name="from"></param>
		/// <returns></returns>
		private static Tree CreateTree(BitArray nodes, short root, short from = -1) {
			// Create `Tree`s for the children
			List<Tree> children = new List<Tree>(_adjacencyList[root].Count);
			foreach (short child in _adjacencyList[root]) {
				// Skip when the child is not in the graph, or if we came from this node.
				if (!nodes[child] || child == from) continue;

				children.Add(CreateTree(nodes, child, root));
			}

			return new Tree(root, children.ToArray());
		}

		private static CriticalList CriticalRankTree(Tree tree, short[] treeRank) {
			if (tree.children.Length == 0) {
				treeRank[tree.root] = 1;

				return new CriticalList(1);
			}
			
			CriticalList[] criticalLists = new CriticalList[tree.children.Length];
			for (int i = 0; i < criticalLists.Length; i++) {
				criticalLists[i] = CriticalRankTree(tree.children[i], treeRank);
			}

			return RootRank(tree.root, criticalLists, treeRank);
		}

		private static CriticalList RootRank(short root, CriticalList[] criticalLists, short[] treeRank) {
			short[] t = criticalLists.Select(l => l.GetMax()).ToArray();
			short a = t.Max();
			short c = (short) (a + 1);
			short avail = c;
			CriticalList L = new CriticalList();

			// While not ranked
			while (treeRank[root] == 0) {
				c--;

				short cCount = 0;
				short cIndex = -1;
				for (short i = 0; i < t.Length; i++) {
					// ReSharper disable once InvertIf
					if (t[i] == c) {
						cCount++;
						cIndex = i;
					} 
				}

				if ((cCount >= 2)
				    || (c == 0 && criticalLists.Length == 1)
					) {
					treeRank[root] = avail;
					
					L.AddAvail(avail);
					return L;
				}
				else if (cCount == 1) {
					L.Add(c);
					criticalLists[cIndex].DeleteMax();
					t[cIndex] = criticalLists[cIndex].GetMax();
				}
				else if (cCount == 0) {
					avail = c;
				}
			}

			return L;
		}

		#endregion


		#endregion

		/// <summary>
		/// Get the first node of a bit array
		/// </summary>
		/// <param name="nodes"></param>
		/// <returns>The first index where a 1 can be found</returns>
		private static int FirstNode(BitArray nodes) {
			for (int i = 0; i < nodes.Length; i++) {
				if (nodes[i]) return i;
			}

			return -1;
		}
	}

	internal class BitArrayEqualityComparer : IEqualityComparer<BitArray> {
		public bool Equals(BitArray x, BitArray y) {
			if (x == null || y == null || x.Count != y.Count) return false;

			for (int i = 0; i < x.Count; i++) {
				if (x[i] != y[i]) return false;
			}

			return true;
		}

		/// <summary>
		/// Get hash code of a <see cref="BitArray"/>
		/// https://stackoverflow.com/questions/3125676/generating-a-good-hash-code-gethashcode-for-a-bitarray
		/// </summary>
		/// <param name="array"></param>
		/// <returns></returns>
		public int GetHashCode(BitArray array) {
			UInt32 hash = 17;
			int bitsRemaining = array.Length;
			foreach (int value in array.GetInternalValues()) {
				UInt32 cleanValue = (UInt32)value;
				if (bitsRemaining < 32) {
					//clear any bits that are beyond the end of the array
					int bitsToWipe = 32 - bitsRemaining;
					cleanValue <<= bitsToWipe;
					cleanValue >>= bitsToWipe;
				}

				hash = hash * 23 + cleanValue;
				bitsRemaining -= 32;
			}
			return (int) hash;
		}
	}

	public static class BitArrayExtensions {
		static FieldInfo _internalArrayGetter = GetInternalArrayGetter();

		static FieldInfo GetInternalArrayGetter() {
			return typeof(BitArray).GetField("m_array", BindingFlags.NonPublic | BindingFlags.Instance);
		}

		static int[] GetInternalArray(BitArray array) {
			return (int[]) _internalArrayGetter.GetValue(array);
		}

		public static IEnumerable<int> GetInternalValues(this BitArray array) {
			return GetInternalArray(array);
		}

	}

	/// <summary>
	/// Basic data structure for a tree
	/// </summary>
	internal struct Tree {
		public short root;
		public Tree[] children;

		public Tree(short root, Tree[] children) {
			this.root     = root;
			this.children = children;
		}
	}

	/// <summary>
	/// Basic datastructure for a subgraph
	/// </summary>
	internal struct SubGraph {
		public BitArray nodes;
		public short size;
		public short edges;

		/// <summary>
		/// Size of a path in this graph
		/// </summary>
		public short pathSize;

		public SubGraph(BitArray nodes, short size, short edges, short pathSize = 1) {
			this.nodes = nodes;
			this.size  = size;
			this.edges = edges;

			this.pathSize = pathSize;
		}
	}

	internal class CriticalList {

		private readonly List<short> _list = new List<short>();
		private short _startIndex = 0;

		public CriticalList() {
		}

		public CriticalList(short startValue) {
			this.Add(startValue);
		}

		public short GetMax() {
			return this._startIndex < this._list.Count
				? this._list[this._startIndex]
				: (short) 0;
		}

		public void DeleteMax() {
			this._startIndex++;
		}

		/// <summary>
		/// Add value to end of the list
		/// </summary>
		/// <param name="value"></param>
		public void Add(short value) {
			this._list.Add(value);
		}

		public void AddAvail(short avail) {
			for (int i = this._startIndex; i < this._list.Count; i++) {
				if (this._list[i] > avail) continue;

				// Clear rest of the list
				this._list.RemoveRange(i, this._list.Count - i);
				break;
			}

			// Add the avail to the list
			this.Add(avail);
		}

	}
}
