namespace ImageCompressor.Tree;

using System.Drawing;
using ErrorCalculation;
using Util;

public class QuadTree
{
    public int leavesCount { get => leafNodes.Count; }

    public QuadTree(Bitmap source, int minBlockSize, ErrorCalculator errorCalculator)
    {
        this.sourceImage = source;
        this.errorCalculator = errorCalculator;
        this.rootNode = new Node() { content = new ImageRegion(new Region2Int(0, 0, source.Width - 1, source.Height - 1)) };
        this.leafNodes = new List<Node>([rootNode]);

        int i = 0;
        while (i < leafNodes.Count)
        {
            Region2Int currentRegion = leafNodes[i].content.region;

            if (currentRegion.area >= 4 * minBlockSize && currentRegion.size.x > 1 && currentRegion.size.y > 1)
            {
                leafNodes[i].children[0] = new Node()
                {
                    parent = leafNodes[i],
                    content = new ImageRegion(new Region2Int(currentRegion.start, currentRegion.start + currentRegion.size / 2 - new Vector2Int(1, 1)))
                };

                leafNodes[i].children[1] = new Node()
                {
                    parent = leafNodes[i],
                    content = new ImageRegion(new Region2Int(currentRegion.start + new Vector2Int(currentRegion.size.x / 2, 0), currentRegion.start + new Vector2Int(currentRegion.size.x - 1, currentRegion.size.y / 2 - 1)))
                };

                leafNodes[i].children[2] = new Node()
                {
                    parent = leafNodes[i],
                    content = new ImageRegion(new Region2Int(currentRegion.start + new Vector2Int(0, currentRegion.size.y / 2), currentRegion.start + new Vector2Int(currentRegion.size.x / 2 - 1, currentRegion.size.y - 1)))
                };

                leafNodes[i].children[3] = new Node()
                {
                    parent = leafNodes[i],
                    content = new ImageRegion(new Region2Int(currentRegion.start + currentRegion.size / 2, currentRegion.end))
                };

                leafNodes.Add(leafNodes[i].children[0]!);
                leafNodes.Add(leafNodes[i].children[1]!);
                leafNodes.Add(leafNodes[i].children[2]!);
                leafNodes.Add(leafNodes[i].children[3]!);

                leafNodes.RemoveAt(i);
                
                continue;
            }

            i++;
        }

        for (i = 0; i < leafNodes.Count; i++)
        {
            leafNodes[i].content.error = errorCalculator.CalculateError(leafNodes[i].content.region);
        }

        SortLeaves(0, leafNodes.Count - 1);
    }

    public ImageRegion Pop()
    {
        Node poppedNode = leafNodes[0];

        leafNodes.RemoveAt(0);
        
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

    private Bitmap sourceImage;
    private ErrorCalculator errorCalculator;
    private Node rootNode;
    private List<Node> leafNodes;

    private void SortLeaves(int start, int end)
    {
        if (start >= end) return;

        int i = start;
        int j = end;

        double pivotError = leafNodes[i].content.error;

        while (i <= j)
        {
            while (i <= j && leafNodes[i].content.error < pivotError)
            {
                i++;
            }

            while (i <= j && leafNodes[j].content.error > pivotError)
            {
                j--;
            }

            if (i > j) break;

            (leafNodes[i], leafNodes[j]) = (leafNodes[j], leafNodes[i]);
            i++;
            j--;
        }
        
        SortLeaves(start, j);
        SortLeaves(i, end);
    }

    private void InsertLeafNode(Node node)
    {
        int i = 0;
        int j = leafNodes.Count;
        int pivot = (i + j) / 2;

        while (i + 1 < j)
        {
            pivot = (i + j) / 2;

            if (node.content.error == leafNodes[pivot].content.error)
            {
                leafNodes.Insert(pivot, node);
                return;
            }
            else if (node.content.error > leafNodes[pivot].content.error)
            {
                i = pivot;
            }
            else if (node.content.error < leafNodes[pivot].content.error)
            {
                j = pivot;
            }
        }

        leafNodes.Insert(pivot, node);
    }

    private class Node
    {
        public ImageRegion content;
        public Node? parent = null;
        public Node?[] children = [null, null, null, null];
    }
}