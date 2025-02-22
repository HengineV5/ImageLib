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

		public T ReadValue(scoped ref SpanBitReader buffer)
		{
			ref var node = ref tree.GetNode(tree.GetRoot());
			while (node.right != -1 || node.left != -1)
			{
				if (buffer.ReadBit())
				{
					node = ref tree.GetNode(node.right);
				}
				else
				{
					node = ref tree.GetNode(node.left);
				}
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
