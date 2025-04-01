namespace ImageCompressor;

using System.Drawing;
using System.Drawing.Imaging;
using System.Numerics;
using ErrorCalculation;
using LinkedNodes;
using Util;

class ImageCompressor
{
    static void Main(string[] args)
    {
        ImageCompressor compressor = new ImageCompressor();

        Console.WriteLine("Image Compressor v1.0");

        do
        {
            Console.Write("Enter an absolute image path: ");
            compressor.imagePath = Console.ReadLine() ?? "";
        }
        while (!File.Exists(compressor.imagePath));

        do
        {
            bool validSelection = false;

            Console.WriteLine("Choose an error calculation method.");
            Console.WriteLine("Possible values are:");
            
            for (int i = 0; i < availableErrorCalculators.Length; i++)
            {
                Console.WriteLine("> " + availableErrorCalculators[i].Name);
            }

            string selection = Console.ReadLine() ?? "";

            for (int i = 0; i < availableErrorCalculators.Length; i++)
            {
                if (selection == availableErrorCalculators[i].Name)
                {
                    validSelection = true;
                    break;
                }
            }

            if (validSelection) break;
        }
        while (true);

        do
        {
            Console.Write("Enter desired threshold: ");
            
            string rawInput = Console.ReadLine() ?? "";

            if (!double.TryParse(rawInput, out compressor.threshold))
            {
                continue;
            }

            if (compressor.threshold < 0d || compressor.threshold > 1d)
            {
                continue;
            }

            break;
        }
        while (true);

        do
        {
            Console.Write("Enter minimum block size: ");

            string rawInput = Console.ReadLine() ?? "";

            if (!int.TryParse(rawInput, out compressor.minBlockSize))
            {
                continue;
            }

            if (compressor.minBlockSize < 1)
            {
                continue;
            }

            break;
        }
        while (true);

        do
        {
            Console.Write("Enter desired compression rate: ");

            string rawInput = Console.ReadLine() ?? "";

            if (!double.TryParse(rawInput, out compressor.compressionPercentage))
            {
                continue;
            }

            if (compressor.compressionPercentage < 0d || compressor.compressionPercentage > 1d)
            {
                continue;
            }

            break;
        }
        while (true);

        do
        {
            Console.WriteLine("Enter an absolute output path (should have the same extension as input file):");
            compressor.outPath = Console.ReadLine() ?? "";
        }
        while (compressor.outPath == "");

        compressor.Run();
    }

    static ErrorCalculator[] availableErrorCalculators = {
        new MaxPixelDifferenceCalculator()
    };

    public string imagePath;
    public ErrorCalculator errorCalculator;
    public double threshold;
    public int minBlockSize;
    public double compressionPercentage;
    public string outPath;

    private byte[]? rawData = null;
    private long originalSize = 0;
    private Bitmap? rawImage = null;
    private InvertedTree<ImageRegion>? tree = null;

    public ImageCompressor()
    {
        imagePath = "";
        errorCalculator = availableErrorCalculators[0];
        threshold = 0;
        minBlockSize = 0;
        compressionPercentage = 0;
        outPath = "";
    }

    public void Run()
    {
        rawData = File.ReadAllBytes(imagePath);
        originalSize = rawData.LongLength;
        rawImage = new Bitmap(imagePath);

        errorCalculator.LoadImage((Bitmap) rawImage.Clone());
        
        if (compressionPercentage <= 0.0d)
        {
            CompressByThreshold(new Vector2Int(0, 0), new Vector2Int(rawImage.Size.Width - 1, rawImage.Size.Height - 1));
        }
        else
        {
            CompressByPercentage();
        }

        rawImage.Save(outPath, ImageFormat.Png);
    }

    private void CompressByThreshold(Vector2Int regionStart, Vector2Int regionEnd)
    {
        if (regionStart.x > regionEnd.x || regionStart.y > regionEnd.y || (regionStart.x == regionEnd.x && regionStart.y == regionEnd.y)) return;

        Vector2Int regionSize = regionEnd - regionStart + new Vector2Int(1, 1);
        Color[,] pixels = new Color[regionSize.x, regionSize.y];

        for (int i = 0; i < regionSize.x; i++)
        {
            for (int j = 0; j < regionSize.y; j++)
            {
                pixels[i, j] = rawImage!.GetPixel(regionStart.x + i, regionStart.y + j);
            }
        }

        double error = errorCalculator.CalculateError(new Region2Int(regionStart, regionEnd));
        
        if (error < threshold)
        {
            NormalizeRegion(new(regionStart, regionEnd));
        }
        else if (regionSize.x >= 2 * minBlockSize && regionSize.y >= 2 * minBlockSize)
        {
            CompressByThreshold(regionStart, regionStart + regionSize / 2 - new Vector2Int(1, 1));
            CompressByThreshold(regionStart + new Vector2Int(regionSize.x / 2, 0), regionStart + new Vector2Int(regionSize.x - 1, regionSize.y / 2));
            CompressByThreshold(regionStart + new Vector2Int(0, regionSize.y / 2), regionStart + new Vector2Int(regionSize.x / 2, regionSize.y - 1));
            CompressByThreshold(regionStart + regionSize / 2, regionEnd);
        }
    }

    private void CompressByPercentage()
    {
        tree = new(new(new(new(new(0, 0), new(rawImage!.Size.Width - 1, rawImage!.Size.Height - 1)))));

        int i = 0;

        while (i < tree.leafNodes.Count)
        {
            Region2Int currentRegion = tree.leafNodes[i].content.region;

            if (currentRegion.size.x >= 2 * minBlockSize && currentRegion.size.y >= 2 * minBlockSize)
            {
                tree.AddLeaves([
                    new(new(new(currentRegion.start, currentRegion.start + currentRegion.size / 2 - new Vector2Int(1, 1)))),
                    new(new(new(currentRegion.start + new Vector2Int(currentRegion.size.x / 2, 0), currentRegion.start + new Vector2Int(currentRegion.size.x - 1, currentRegion.size.y / 2)))),
                    new(new(new(currentRegion.start + new Vector2Int(0, currentRegion.size.y / 2), currentRegion.start + new Vector2Int(currentRegion.size.x / 2, currentRegion.size.y - 1)))),
                    new(new(new(currentRegion.start + currentRegion.size / 2, currentRegion.end)))
                ], tree.leafNodes[i]);

                continue;
            }

            i++;
        }

        for (i = 0; i < tree.leafNodes.Count; i++)
        {
            tree.leafNodes[i].content.error = errorCalculator.CalculateError(tree.leafNodes[i].content.region);
        }

        SortLeafNodesByError(tree.leafNodes, 0, tree.leafNodes.Count - 1);

        int pixelsInImage = rawImage.Width * rawImage.Height;
        int uncompressedPixels = pixelsInImage;

        MemoryStream imageStream = new((int) originalSize);
        rawImage.Save(imageStream, ImageFormat.Png);

        while (imageStream.Length > originalSize * compressionPercentage)
        {
            uncompressedPixels = (int) (imageStream.Length  * pixelsInImage / originalSize);

            while (uncompressedPixels > pixelsInImage * compressionPercentage)
            {
                imageStream.SetLength(0);

                if (tree.leafNodes.Count <= 1) break;

                uncompressedPixels -= tree.leafNodes[0].content.region.size.x * tree.leafNodes[0].content.region.size.y;

                NormalizeRegion(tree.leafNodes[0].content.region);
                tree.RemoveLeaf(tree.leafNodes[0]);

                Node<ImageRegion> lastLeaf = tree.leafNodes[tree.leafNodes.Count - 1];
                if (lastLeaf.content.error == 0)
                {
                    lastLeaf.content.error = errorCalculator.CalculateError(lastLeaf.content.region);
                    tree.leafNodes.RemoveAt(tree.leafNodes.Count - 1);
                    InsertLeafNode(lastLeaf, tree.leafNodes);
                }
            }

            if (tree.leafNodes.Count <= 1) break;

            rawImage.Save(imageStream, ImageFormat.Png);
        }
    }

    private void SortLeafNodesByError(List<Node<ImageRegion>> leaves, int start, int end)
    {
        if (start >= end) return;

        int i = start;
        int j = end;

        double pivotError = leaves[i].content.error;

        while (i <= j)
        {
            while (i <= j && leaves[i].content.error < pivotError)
            {
                i++;
            }

            while (i <= j && leaves[j].content.error > pivotError)
            {
                j--;
            }

            if (i > j) break;

            (leaves[i], leaves[j]) = (leaves[j], leaves[i]);
            i++;
            j--;
        }
        
        SortLeafNodesByError(leaves, start, j);
        SortLeafNodesByError(leaves, i, end);
    }

    private void InsertLeafNode(Node<ImageRegion> newLeaf, List<Node<ImageRegion>> leaves)
    {
        int i = 0;
        int j = leaves.Count;
        int pivot = (i + j) / 2;

        while (i + 1 < j)
        {
            pivot = (i + j) / 2;

            if (newLeaf.content.error == leaves[pivot].content.error)
            {
                leaves.Insert(pivot, newLeaf);
                return;
            }
            else if (newLeaf.content.error > leaves[pivot].content.error)
            {
                i = pivot;
            }
            else if (newLeaf.content.error < leaves[pivot].content.error)
            {
                j = pivot;
            }
        }

        leaves.Insert(pivot, newLeaf);
    }

    private void NormalizeRegion(Region2Int region)
    {
        int regionWidth = region.end.x - region.start.x + 1;
        int regionHeight = region.end.y - region.start.y + 1;

        int r = 0;
        int g = 0;
        int b = 0;

        for (int i = region.start.x; i <= region.end.x; i++)
        {
            for (int j = region.start.y; j <= region.end.y; j++)
            {
                Color pixel = rawImage!.GetPixel(i, j);
                r += pixel.R;
                g += pixel.G;
                b += pixel.B;
            }
        }

        int regionSize = regionWidth * regionHeight;

        r /= regionSize;
        g /= regionSize;
        b /= regionSize;

        for (int i = region.start.x; i <= region.end.x; i++)
        {
            for (int j = region.start.y; j <= region.end.y; j++)
            {
                rawImage!.SetPixel(i, j, Color.FromArgb(r, g, b));
            }
        }
    }
}
