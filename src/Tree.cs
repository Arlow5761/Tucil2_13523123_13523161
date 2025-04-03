namespace ImageCompressor.Tree;

using ErrorCalculation;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Util;

public class QuadTree
{
    public int leavesCount { get => leafNodes.Count; }

    public QuadTree(Image<Rgba32> source, int minBlockSize, ErrorCalculator errorCalculator)
    {
        this.sourceImage = source;
        this.errorCalculator = errorCalculator;
        this.rootNode = new Node() { content = new ImageRegion(new Region2Int(0, 0, source.Width - 1, source.Height - 1)) };
        
        LinkedList<Node> tempLeaves = new LinkedList<Node>([rootNode]);
        LinkedListNode<Node>? currentNode = tempLeaves.First;
        while (currentNode is not null)
        {
            Node node = currentNode.ValueRef;
            Region2Int currentRegion = node.content.region;

            if (currentRegion.area >= 4 * minBlockSize && currentRegion.size.x > 1 && currentRegion.size.y > 1)
            {
                node.children[0] = new Node()
                {
                    parent = node,
                    content = new ImageRegion(new Region2Int(currentRegion.start, currentRegion.start + currentRegion.size / 2 - new Vector2Int(1, 1)))
                };

                node.children[1] = new Node()
                {
                    parent = node,
                    content = new ImageRegion(new Region2Int(currentRegion.start + new Vector2Int(currentRegion.size.x / 2, 0), currentRegion.start + new Vector2Int(currentRegion.size.x - 1, currentRegion.size.y / 2 - 1)))
                };

                node.children[2] = new Node()
                {
                    parent = node,
                    content = new ImageRegion(new Region2Int(currentRegion.start + new Vector2Int(0, currentRegion.size.y / 2), currentRegion.start + new Vector2Int(currentRegion.size.x / 2 - 1, currentRegion.size.y - 1)))
                };

                node.children[3] = new Node()
                {
                    parent = node,
                    content = new ImageRegion(new Region2Int(currentRegion.start + currentRegion.size / 2, currentRegion.end))
                };

                tempLeaves.AddLast(node.children[0]!);
                tempLeaves.AddLast(node.children[1]!);
                tempLeaves.AddLast(node.children[2]!);
                tempLeaves.AddLast(node.children[3]!);

                currentNode = currentNode.Next;

                tempLeaves.Remove(currentNode!.Previous!);

                continue;
            }

            currentNode = currentNode.Next;
        }

        this.leafNodes = tempLeaves.ToList();

        for (int i = 0; i < leafNodes.Count; i++)
        {
            leafNodes[i].content.error = errorCalculator.CalculateError(leafNodes[i].content.region);
        }

        SortLeaves();
    }

    public ImageRegion Pop()
    {
        Node poppedNode = leafNodes[0];

        leafNodes[0] = leafNodes[leafNodes.Count - 1];
        leafNodes.RemoveAt(leafNodes.Count - 1);

        for (int i = 2; i <= leafNodes.Count; i *= 2)
        {
            if (i < leafNodes.Count && leafNodes[i].content.error < leafNodes[i - 1].content.error)
            {
                i++;
            }

            if (leafNodes[i / 2 - 1].content.error > leafNodes[i - 1].content.error)
            {
                (leafNodes[i / 2 - 1], leafNodes[i - 1]) = (leafNodes[i - 1], leafNodes[i / 2 - 1]);
            }
            else
            {
                break;
            }
        }
        
        if (poppedNode.parent is null)
        {
            return poppedNode.content;
        }

        Node parentNode = poppedNode.parent;
        bool childrenless = true;

        for (int i = 0; i < 4; i++)
        {
            if (parentNode.children[i] == poppedNode)
            {
                parentNode.children[i] = null;
            }
            else if (parentNode.children[i] is not null)
            {
                childrenless = false;
            }
        }

        if (childrenless)
        {
            parentNode.content.error = errorCalculator.CalculateError(parentNode.content.region);
            InsertLeafNode(parentNode);
        }

        return poppedNode.content;
    }

    private Image<Rgba32> sourceImage;
    private ErrorCalculator errorCalculator;
    private Node rootNode;
    private List<Node> leafNodes;

    private void SortLeaves()
    {
        for (int i = leafNodes.Count - 1; i > 0; i--)
        {
            int parentIndex = (i + 1) / 2 - 1;

            if (leafNodes[parentIndex].content.error > leafNodes[i].content.error)
            {
                (leafNodes[parentIndex], leafNodes[i]) = (leafNodes[i], leafNodes[parentIndex]);
            }
        }
    }

    private void InsertLeafNode(Node node)
    {
        leafNodes.Add(node);

        for (int i = leafNodes.Count; i > 1 && leafNodes[i - 1].content.error < leafNodes[i / 2 - 1].content.error; i /= 2)
        {
            (leafNodes[i / 2 - 1], leafNodes[i - 1]) = (leafNodes[i / 2 - 1], leafNodes[i - 1]);
        }
    }

    private class Node
    {
        public ImageRegion content;
        public Node? parent = null;
        public Node?[] children = [null, null, null, null];
    }
}