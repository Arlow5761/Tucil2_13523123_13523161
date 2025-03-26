namespace ImageCompressor.LinkedNodes;

public class Node<T>
{
    public T content;
    public Node<T>? next;

    public Node(T content) : this(content, null) {}

    public Node(T content, Node<T>? next)
    {
        this.content = content;
        this.next = next;
    }
}

public class InvertedTree<T>
{
    public List<Node<T>> leafNodes;

    public InvertedTree(Node<T> rootNode)
    {
        leafNodes = [rootNode];
    }

    public void AddLeaf(Node<T> newLeafNode, Node<T> targetLeafNode)
    {
        // make sure node is actually a leaf node
        for (int i = 0; i < leafNodes.Count; i++)
        {
            Node<T> leafNode = leafNodes[i];

            if (leafNode == targetLeafNode)
            {
                leafNodes.RemoveAt(i);
                newLeafNode.next = leafNode;
                leafNodes.Add(newLeafNode);
                return;
            }
        }
    }

    public void RemoveLeaf(Node<T> leafNode)
    {
        // do not remove root node
        if (leafNodes.Count <= 1) return;

        // make sure node is actually a leaf node
        bool isLeaf = false;

        for (int i = 0; i < leafNodes.Count; i++)
        {
            if (leafNodes[i] == leafNode)
            {
                isLeaf = true;
                leafNodes.RemoveAt(i);
                break;
            }
        }

        if (!isLeaf) return;

        // mark parent as leaf node if parent exists
        // and no other leaf nodes point to it
        Node<T>? parent = leafNode.next;

        if (parent is not null)
        {
            for (int i = 0; i < leafNodes.Count; i++)
            {
                if (leafNodes[i].next == parent) return;
            }

            leafNodes.Add(parent);
        }
    }
}