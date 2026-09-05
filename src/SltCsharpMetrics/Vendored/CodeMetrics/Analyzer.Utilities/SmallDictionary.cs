using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Analyzer.Utilities;

/// <summary>
/// Copied from https://github.com/dotnet/roslyn/blob/main/src/Compilers/Core/Portable/Collections/SmallDictionary.cs
/// Dictionary designed to hold small number of items.
/// Compared to the regular Dictionary, average overhead per-item is roughly the same, but
/// unlike regular dictionary, this one is based on an AVL tree and as such does not require
/// rehashing when items are added.
/// It does require rebalancing, but that is allocation-free.
///
/// Major caveats:
///  1) There is no Remove method. (can be added, but we do not seem to use Remove that much)
///  2) foreach [keys|values|pairs] may allocate a small array.
///  3) Performance is no longer O(1). At a certain count it becomes slower than regular Dictionary.
///     In comparison to regular Dictionary on my machine:
///        On trivial number of elements (5 or so) it is more than 2x faster.
///        The break even count is about 120 elements for read and 55 for write operations (with unknown initial size).
///        At UShort.MaxValue elements, this dictionary is 6x slower to read and 4x slower to write
///
/// Generally, this dictionary is a win if number of elements is small, not known beforehand or both.
///
/// If the size of the dictionary is known at creation and it is likely to contain more than 10 elements,
/// then regular Dictionary is a better choice.
/// </summary>
internal sealed class SmallDictionary<K, V> : IEnumerable<KeyValuePair<K, V>>, IEnumerable where K : notnull
{
	private abstract class Node
	{
		public K Key;

		public V Value;

		public virtual Node? Next => null;

		protected Node(K key, V value)
		{
			Key = key;
			Value = value;
		}
	}

	private sealed class NodeLinked : Node
	{
		public override Node Next { get; }

		public NodeLinked(K key, V value, Node next)
			: base(key, value)
		{
			Next = next;
		}
	}

	private sealed class AvlNodeHead : AvlNode
	{
		public Node next;

		public override Node Next => next;

		public AvlNodeHead(int hashCode, K key, V value, Node next)
			: base(hashCode, key, value)
		{
			this.next = next;
		}
	}

	private abstract class HashedNode : Node
	{
		public int HashCode;

		public sbyte Balance;

		protected HashedNode(int hashCode, K key, V value)
			: base(key, value)
		{
			HashCode = hashCode;
		}
	}

	private class AvlNode : HashedNode
	{
		public AvlNode? Left;

		public AvlNode? Right;

		public AvlNode(int hashCode, K key, V value)
			: base(hashCode, key, value)
		{
		}
	}

	internal readonly struct KeyCollection : IEnumerable<K>, IEnumerable
	{
		public struct Enumerator
		{
			private readonly Stack<AvlNode>? _stack;

			private Node? _next;

			private Node? _current;

			public readonly K Current => _current.Key;

			public Enumerator(SmallDictionary<K, V> dict)
			{
				this = default(Enumerator);
				AvlNode root = dict._root;
				if (root != null)
				{
					if (root.Left == root.Right)
					{
						_next = root;
						return;
					}
					_stack = new Stack<AvlNode>(dict.HeightApprox());
					_stack.Push(root);
				}
			}

			public bool MoveNext()
			{
				if (_next != null)
				{
					_current = _next;
					_next = _next.Next;
					return true;
				}
				if (_stack == null || _stack.Count == 0)
				{
					return false;
				}
				AvlNode avlNode = (AvlNode)(_current = _stack.Pop());
				_next = avlNode.Next;
				PushIfNotNull(_stack, avlNode.Left);
				PushIfNotNull(_stack, avlNode.Right);
				return true;
				static void PushIfNotNull(Stack<AvlNode> stack, AvlNode? child)
				{
					if (child != null)
					{
						stack.Push(child);
					}
				}
			}
		}

		public sealed class EnumerableCore : IEnumerator<K>, IEnumerator, IDisposable
		{
			private Enumerator _e;

			K IEnumerator<K>.Current => _e.Current;

			object IEnumerator.Current => _e.Current;

			public EnumerableCore(Enumerator e)
			{
				_e = e;
			}

			void IDisposable.Dispose()
			{
			}

			bool IEnumerator.MoveNext()
			{
				return _e.MoveNext();
			}

			void IEnumerator.Reset()
			{
				throw new NotSupportedException();
			}
		}

		private readonly SmallDictionary<K, V> _dict;

		public KeyCollection(SmallDictionary<K, V> dict)
		{
			_dict = dict;
		}

		public Enumerator GetEnumerator()
		{
			return new Enumerator(_dict);
		}

		IEnumerator<K> IEnumerable<K>.GetEnumerator()
		{
			return new EnumerableCore(GetEnumerator());
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			throw new NotImplementedException();
		}
	}

	internal readonly struct ValueCollection : IEnumerable<V>, IEnumerable
	{
		public struct Enumerator
		{
			private readonly Stack<AvlNode>? _stack;

			private Node? _next;

			private Node? _current;

			public readonly V Current => _current.Value;

			public Enumerator(SmallDictionary<K, V> dict)
			{
				this = default(Enumerator);
				AvlNode root = dict._root;
				if (root != null)
				{
					if (root.Left == root.Right)
					{
						_next = root;
						return;
					}
					_stack = new Stack<AvlNode>(dict.HeightApprox());
					_stack.Push(root);
				}
			}

			public bool MoveNext()
			{
				if (_next != null)
				{
					_current = _next;
					_next = _next.Next;
					return true;
				}
				if (_stack == null || _stack.Count == 0)
				{
					return false;
				}
				AvlNode avlNode = (AvlNode)(_current = _stack.Pop());
				_next = avlNode.Next;
				PushIfNotNull(_stack, avlNode.Left);
				PushIfNotNull(_stack, avlNode.Right);
				return true;
				static void PushIfNotNull(Stack<AvlNode> stack, AvlNode? child)
				{
					if (child != null)
					{
						stack.Push(child);
					}
				}
			}
		}

		public sealed class EnumerableCore : IEnumerator<V>, IEnumerator, IDisposable
		{
			private Enumerator _e;

			V IEnumerator<V>.Current => _e.Current;

			object? IEnumerator.Current => _e.Current;

			public EnumerableCore(Enumerator e)
			{
				_e = e;
			}

			void IDisposable.Dispose()
			{
			}

			bool IEnumerator.MoveNext()
			{
				return _e.MoveNext();
			}

			void IEnumerator.Reset()
			{
				throw new NotImplementedException();
			}
		}

		private readonly SmallDictionary<K, V> _dict;

		public ValueCollection(SmallDictionary<K, V> dict)
		{
			_dict = dict;
		}

		public Enumerator GetEnumerator()
		{
			return new Enumerator(_dict);
		}

		IEnumerator<V> IEnumerable<V>.GetEnumerator()
		{
			return new EnumerableCore(GetEnumerator());
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			throw new NotImplementedException();
		}
	}

	public struct Enumerator
	{
		private readonly Stack<AvlNode>? _stack;

		private Node? _next;

		private Node? _current;

		public readonly KeyValuePair<K, V> Current => new KeyValuePair<K, V>(_current.Key, _current.Value);

		public Enumerator(SmallDictionary<K, V> dict)
		{
			this = default(Enumerator);
			AvlNode root = dict._root;
			if (root != null)
			{
				if (root.Left == root.Right)
				{
					_next = root;
					return;
				}
				_stack = new Stack<AvlNode>(dict.HeightApprox());
				_stack.Push(root);
			}
		}

		public bool MoveNext()
		{
			if (_next != null)
			{
				_current = _next;
				_next = _next.Next;
				return true;
			}
			if (_stack == null || _stack.Count == 0)
			{
				return false;
			}
			AvlNode avlNode = (AvlNode)(_current = _stack.Pop());
			_next = avlNode.Next;
			PushIfNotNull(_stack, avlNode.Left);
			PushIfNotNull(_stack, avlNode.Right);
			return true;
			static void PushIfNotNull(Stack<AvlNode> stack, AvlNode? child)
			{
				if (child != null)
				{
					stack.Push(child);
				}
			}
		}
	}

	public sealed class EnumerableCore : IEnumerator<KeyValuePair<K, V>>, IEnumerator, IDisposable
	{
		private Enumerator _e;

		KeyValuePair<K, V> IEnumerator<KeyValuePair<K, V>>.Current => _e.Current;

		object IEnumerator.Current => _e.Current;

		public EnumerableCore(Enumerator e)
		{
			_e = e;
		}

		void IDisposable.Dispose()
		{
		}

		bool IEnumerator.MoveNext()
		{
			return _e.MoveNext();
		}

		void IEnumerator.Reset()
		{
			throw new NotImplementedException();
		}
	}

	private AvlNode? _root;

	public readonly IEqualityComparer<K> Comparer;

	public static readonly SmallDictionary<K, V> Empty = new SmallDictionary<K, V>(null);

	public bool IsEmpty => _root == null;

	public V this[K key]
	{
		get
		{
			if (!TryGetValue(key, out var value))
			{
				throw new KeyNotFoundException($"Could not find key {key}");
			}
			return value;
		}
		set
		{
			Insert(GetHashCode(key), key, value, add: false);
		}
	}

	public KeyCollection Keys => new KeyCollection(this);

	public ValueCollection Values => new ValueCollection(this);

	public SmallDictionary()
		: this((IEqualityComparer<K>)EqualityComparer<K>.Default)
	{
	}

	public SmallDictionary(IEqualityComparer<K> comparer)
	{
		Comparer = comparer;
	}

	public SmallDictionary(SmallDictionary<K, V> other, IEqualityComparer<K> comparer)
		: this(comparer)
	{
		Enumerator enumerator = other.GetEnumerator();
		while (enumerator.MoveNext())
		{
			KeyValuePair<K, V> current = enumerator.Current;
			Add(current.Key, current.Value);
		}
	}

	private bool CompareKeys(K k1, K k2)
	{
		return Comparer.Equals(k1, k2);
	}

	private int GetHashCode(K k)
	{
		return Comparer.GetHashCode(k);
	}

	public void Remove(K key)
	{
		_root = Remove(_root, GetHashCode(key));
	}

	private static AvlNode? Remove(AvlNode? currentNode, int hashCode)
	{
		if (currentNode == null)
		{
			return null;
		}
		int hashCode2 = currentNode.HashCode;
		if (hashCode2 > hashCode)
		{
			currentNode.Left = Remove(currentNode.Left, hashCode);
		}
		else if (hashCode2 < hashCode)
		{
			currentNode.Right = Remove(currentNode.Right, hashCode);
		}
		else if (currentNode.Left == null || currentNode.Right == null)
		{
			AvlNode avlNode = null;
			avlNode = ((avlNode != currentNode.Left) ? currentNode.Left : currentNode.Right);
			currentNode = ((avlNode != null) ? avlNode : null);
		}
		else
		{
			AvlNode avlNode2 = MinValueNode(currentNode.Right);
			currentNode.HashCode = avlNode2.HashCode;
			currentNode.Value = avlNode2.Value;
			currentNode.Key = avlNode2.Key;
			currentNode.Right = Remove(currentNode.Right, avlNode2.HashCode);
		}
		if (currentNode == null)
		{
			return null;
		}
		currentNode.Balance = (sbyte)(Height(currentNode.Left) - Height(currentNode.Right));
		return currentNode.Balance switch
		{
			-2 => (currentNode.Right.Balance <= 0) ? LeftSimple(currentNode) : LeftComplex(currentNode), 
			2 => (currentNode.Left.Balance >= 0) ? RightSimple(currentNode) : RightComplex(currentNode), 
			_ => currentNode, 
		};
	}

	private static AvlNode MinValueNode(AvlNode node)
	{
		AvlNode avlNode = node;
		while (avlNode.Left != null)
		{
			avlNode = avlNode.Left;
		}
		return avlNode;
	}

	private static int Height(AvlNode? node)
	{
		if (node == null)
		{
			return 0;
		}
		int val = Height(node.Left);
		int val2 = Height(node.Right);
		return 1 + Math.Max(val, val2);
	}

	public bool TryGetValue(K key, [MaybeNullWhen(false)] out V value)
	{
		if (_root != null)
		{
			return TryGetValue(GetHashCode(key), key, out value);
		}
		value = default(V);
		return false;
	}

	public void Add(K key, V value)
	{
		Insert(GetHashCode(key), key, value, add: true);
	}

	public bool ContainsKey(K key)
	{
		V value;
		return TryGetValue(key, out value);
	}

	[Conditional("DEBUG")]
	internal void AssertBalanced()
	{
	}

	private bool TryGetValue(int hashCode, K key, [MaybeNullWhen(false)] out V value)
	{
		AvlNode avlNode = _root;
		while (true)
		{
			if (avlNode.HashCode > hashCode)
			{
				avlNode = avlNode.Left;
			}
			else
			{
				if (avlNode.HashCode >= hashCode)
				{
					break;
				}
				avlNode = avlNode.Right;
			}
			if (avlNode == null)
			{
				value = default(V);
				return false;
			}
		}
		if (CompareKeys(avlNode.Key, key))
		{
			value = avlNode.Value;
			return true;
		}
		return GetFromList(avlNode.Next, key, out value);
	}

	private bool GetFromList(Node? next, K key, [MaybeNullWhen(false)] out V value)
	{
		while (next != null)
		{
			if (CompareKeys(key, next.Key))
			{
				value = next.Value;
				return true;
			}
			next = next.Next;
		}
		value = default(V);
		return false;
	}

	private void Insert(int hashCode, K key, V value, bool add)
	{
		AvlNode avlNode = _root;
		if (avlNode == null)
		{
			_root = new AvlNode(hashCode, key, value);
			return;
		}
		AvlNode avlNode2 = null;
		AvlNode avlNode3 = avlNode;
		AvlNode avlNode4 = null;
		while (true)
		{
			int hashCode2 = avlNode.HashCode;
			if (avlNode.Balance != 0)
			{
				avlNode4 = avlNode2;
				avlNode3 = avlNode;
			}
			if (hashCode2 > hashCode)
			{
				if (avlNode.Left == null)
				{
					avlNode = (avlNode.Left = new AvlNode(hashCode, key, value));
					break;
				}
				avlNode2 = avlNode;
				avlNode = avlNode.Left;
				continue;
			}
			if (hashCode2 < hashCode)
			{
				if (avlNode.Right == null)
				{
					avlNode = (avlNode.Right = new AvlNode(hashCode, key, value));
					break;
				}
				avlNode2 = avlNode;
				avlNode = avlNode.Right;
				continue;
			}
			HandleInsert(avlNode, avlNode2, key, value, add);
			return;
		}
		AvlNode avlNode5 = avlNode3;
		do
		{
			if (avlNode5.HashCode < hashCode)
			{
				avlNode5.Balance--;
				avlNode5 = avlNode5.Right;
			}
			else
			{
				avlNode5.Balance++;
				avlNode5 = avlNode5.Left;
			}
		}
		while (avlNode5 != avlNode);
		AvlNode avlNode6;
		switch (avlNode3.Balance)
		{
		case -2:
			avlNode6 = ((avlNode3.Right.Balance < 0) ? LeftSimple(avlNode3) : LeftComplex(avlNode3));
			break;
		case 2:
			avlNode6 = ((avlNode3.Left.Balance > 0) ? RightSimple(avlNode3) : RightComplex(avlNode3));
			break;
		default:
			return;
		}
		if (avlNode4 == null)
		{
			_root = avlNode6;
		}
		else if (avlNode3 == avlNode4.Left)
		{
			avlNode4.Left = avlNode6;
		}
		else
		{
			avlNode4.Right = avlNode6;
		}
	}

	private static AvlNode LeftSimple(AvlNode unbalanced)
	{
		AvlNode right = unbalanced.Right;
		unbalanced.Right = right.Left;
		right.Left = unbalanced;
		unbalanced.Balance = 0;
		right.Balance = 0;
		return right;
	}

	private static AvlNode RightSimple(AvlNode unbalanced)
	{
		AvlNode left = unbalanced.Left;
		unbalanced.Left = left.Right;
		left.Right = unbalanced;
		unbalanced.Balance = 0;
		left.Balance = 0;
		return left;
	}

	private static AvlNode LeftComplex(AvlNode unbalanced)
	{
		AvlNode right = unbalanced.Right;
		AvlNode left = right.Left;
		right.Left = left.Right;
		left.Right = right;
		unbalanced.Right = left.Left;
		left.Left = unbalanced;
		sbyte balance = left.Balance;
		left.Balance = 0;
		if (balance < 0)
		{
			right.Balance = 0;
			unbalanced.Balance = 1;
		}
		else
		{
			right.Balance = (sbyte)(-balance);
			unbalanced.Balance = 0;
		}
		return left;
	}

	private static AvlNode RightComplex(AvlNode unbalanced)
	{
		AvlNode left = unbalanced.Left;
		AvlNode right = left.Right;
		left.Right = right.Left;
		right.Left = left;
		unbalanced.Left = right.Right;
		right.Right = unbalanced;
		sbyte balance = right.Balance;
		right.Balance = 0;
		if (balance < 0)
		{
			left.Balance = 1;
			unbalanced.Balance = 0;
		}
		else
		{
			left.Balance = 0;
			unbalanced.Balance = (sbyte)(-balance);
		}
		return right;
	}

	private void HandleInsert(AvlNode node, AvlNode? parent, K key, V value, bool add)
	{
		Node node2 = node;
		do
		{
			if (CompareKeys(node2.Key, key))
			{
				if (add)
				{
					throw new InvalidOperationException();
				}
				node2.Value = value;
				return;
			}
			node2 = node2.Next;
		}
		while (node2 != null);
		AddNode(node, parent, key, value);
	}

	private void AddNode(AvlNode node, AvlNode? parent, K key, V value)
	{
		if (node is AvlNodeHead avlNodeHead)
		{
			NodeLinked next = new NodeLinked(key, value, avlNodeHead.next);
			avlNodeHead.next = next;
			return;
		}
		AvlNodeHead avlNodeHead2 = new AvlNodeHead(node.HashCode, key, value, node);
		avlNodeHead2.Balance = node.Balance;
		avlNodeHead2.Left = node.Left;
		avlNodeHead2.Right = node.Right;
		if (parent == null)
		{
			_root = avlNodeHead2;
		}
		else if (node == parent.Left)
		{
			parent.Left = avlNodeHead2;
		}
		else
		{
			parent.Right = avlNodeHead2;
		}
	}

	public Enumerator GetEnumerator()
	{
		return new Enumerator(this);
	}

	IEnumerator<KeyValuePair<K, V>> IEnumerable<KeyValuePair<K, V>>.GetEnumerator()
	{
		return new EnumerableCore(GetEnumerator());
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		throw new NotImplementedException();
	}

	private int HeightApprox()
	{
		int num = 0;
		for (AvlNode avlNode = _root; avlNode != null; avlNode = avlNode.Left)
		{
			num++;
		}
		return num + num / 2;
	}
}
