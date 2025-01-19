using UtilLib.Span;

namespace ImageLib.Jpg
{
	ref struct SpanHuffmanTable<T>
	{
		public HuffmanTableType type;
		public SpanTree<T> tree;

		public SpanHuffmanTable(HuffmanTableType type, SpanTree<T> tree)
		{
			this.type = type;
			this.tree = tree;
		}

		public T ReadValue(scoped ref SpanBitReader buffer, bool doPrint = false)
		{
			ref var node = ref tree.GetNode(tree.GetRoot());
			if (doPrint)
				Console.WriteLine($"Root: {tree.GetRoot()}, R: {node.right}, L: {node.left}");

			while (node.right != -1 || node.left != -1)
			{
				var bit = buffer.ReadBit();
				if (doPrint)
					Console.WriteLine($"\tBit: {(bit ? 1 : 0)}");

				if (bit)
				{
					if (doPrint)
						Console.WriteLine($"Chose: {node.right}");

					node = ref tree.GetNode(node.right);
				}
				else
				{
					if (doPrint)
						Console.WriteLine($"Chose: {node.left}");

					node = ref tree.GetNode(node.left);
				}

				//if (doPrint)
				//	Console.WriteLine($"R: {node.right != -1}, L: {node.left != -1}");
			}

			return node.value;
		}

		public void AddDHT(scoped Span<byte> lengths, scoped Span<T> elements)
			=> AddDHT(lengths, elements, 0, 0);
		
		void AddDHT(scoped Span<byte> lengths, scoped Span<T> elements, int depth, int element)
		{
			//TODO: Mabye verify huff tree does not have value at root, would be wierd.

			int maxDepth = 15;
			for (int i = 15; i >= 0; i--)
			{
				if (lengths[i] != 0)
					break;

				maxDepth--;
			}

			Console.WriteLine($"MaxDepth: {maxDepth}");

			ref var node = ref tree.CreateNode(out var nodeIdx);
			node.left = AddDHT(lengths, elements, depth, maxDepth, nodeIdx, ref element);
			node.right = AddDHT(lengths, elements, depth, maxDepth, nodeIdx, ref element);
		}
		
		int AddDHT(scoped Span<byte> lengths, scoped Span<T> elements, int depth, int maxDepth, int parent, ref int element)
		{
			if (lengths[depth] > 0)
			{
				ref var node = ref tree.CreateNode(out var nodeIdx);
				node.value = elements[element];

				element++;

				lengths[depth]--;

				return nodeIdx;
			}
			else if (depth < maxDepth)
			{
				ref var node = ref tree.CreateNode(out var nodeIdx);
				node.left = AddDHT(lengths, elements, depth + 1, maxDepth, nodeIdx, ref element);
				node.right = AddDHT(lengths, elements, depth + 1, maxDepth, nodeIdx, ref element);

				return nodeIdx;
			}

			return -1;
		}
	}
}
