namespace ImageCompressor;

using SixLabors.ImageSharp;
using Tree;
using ErrorCalculation;
using Util;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats;
using System.Diagnostics;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;

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
            Console.WriteLine("Choose an error calculation method.");
            Console.WriteLine("Possible values are:");
            
            for (int i = 0; i < availableErrorCalculators.Length; i++)
            {
                Console.WriteLine("> " + availableErrorCalculators[i].Name);
            }

            string selection = Console.ReadLine() ?? "";
            bool validSelection = false;

            for (int i = 0; i < availableErrorCalculators.Length; i++)
            {
                if (selection == availableErrorCalculators[i].Name)
                {
                    compressor.errorCalculator = availableErrorCalculators[i];
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

            if (!double.TryParse(rawInput, out compressor.compressionTarget))
            {
                continue;
            }

            if (compressor.compressionTarget < 0d || compressor.compressionTarget > 1d)
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

        do
        {
            Console.WriteLine("Enter an absolute output path for GIF:");
            compressor.gifPath = Console.ReadLine() ?? "";
        }
        while (compressor.gifPath == "");

        Console.WriteLine("\nCompressing. Please Wait...\n");

        compressor.Run();

        Console.WriteLine("Compression completed in " + compressor.executionTime.ToString() + " milliseconds");
        Console.WriteLine("Original size: " + compressor.originalSize.ToString() +" bytes");
        Console.WriteLine("Compressed size: " + compressor.newSize.ToString() + " bytes");
        Console.WriteLine("Compression percentage: " + compressor.compressionPercentage.ToString() + "%");
        Console.WriteLine("Quadtree max depth: " + compressor.treeDepth.ToString());
        Console.WriteLine("Quadtree nodes: " + compressor.treeNodes.ToString());
    }

    static ErrorCalculator[] availableErrorCalculators = {
        new MaxPixelDifferenceCalculator(),
        new VarianceCalculator(),
        new MeanAbsoluteDeviationCalculator(),
        new EntropyCalculator(),
        new SSIMCalculator()
    };

    static int framerate = 10;

    public string imagePath;
    public ErrorCalculator errorCalculator;
    public double threshold;
    public int minBlockSize;
    public double compressionTarget;
    public string outPath;
    public string gifPath;

    public double executionTime = 0;
    public long originalSize = 0;
    public long newSize = 0;
    public double compressionPercentage = 0;
    public int treeDepth = 0;
    public int treeNodes = 0;

    private byte[]? rawData = null;
    private Image<Rgba32>? sourceImage = null;
    public Image<Rgba32>? outImage = null;
    public Image<Rgba32>? gif = null;
    private IImageEncoder? encoder = null;
    private QuadTree? tree = null;

    public ImageCompressor()
    {
        imagePath = "";
        errorCalculator = availableErrorCalculators[0];
        threshold = 0;
        minBlockSize = 0;
        compressionTarget = 0;
        outPath = "";
        gifPath = "";
    }

    public void Run()
    {
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        rawData = File.ReadAllBytes(imagePath);
        originalSize = rawData.LongLength;

        sourceImage = Image.Load<Rgba32>(imagePath);
        outImage = Image.Load<Rgba32>(imagePath);

        encoder = outImage.Configuration.ImageFormatsManager.GetEncoder(outImage.Metadata.DecodedImageFormat!);

        errorCalculator.LoadImage(sourceImage);

        tree = new QuadTree(sourceImage!, minBlockSize, errorCalculator);
        
        if (compressionTarget <= 0.0d)
        {
            CompressByThreshold();
        }
        else
        {
            compressionTarget = 1 - compressionTarget;
            CompressByPercentage();
        }

        GenerateGif();

        outImage.Save(outPath, encoder);
        gif.SaveAsGif(gifPath);

        stopwatch.Stop();

        executionTime = stopwatch.Elapsed.TotalMilliseconds;

        rawData = File.ReadAllBytes(outPath);
        newSize = rawData.LongLength;

        compressionPercentage = (1d - (double) newSize / originalSize) * 100d;
    }

    private void CompressByThreshold()
    {
        RecursiveCompress(tree!.rootNode, 1);
    }

    private void RecursiveCompress(QuadTree.Node? node, int depth)
    {
        if (node == null) return;

        Region2Int region = node.content.region;

        treeNodes += 1;

        if (region.area <= 1) return;

        double error = errorCalculator.CalculateError(region);
        
        if (error < threshold)
        {
            NormalizeRegion(region, sourceImage!.Frames.RootFrame, outImage!.Frames.RootFrame);
            treeDepth = Math.Max(depth, treeDepth);
            node.compressed = true;
        }
        else if (region.area >= 4 * minBlockSize && region.size.x > 1 && region.size.y > 1)
        {
            RecursiveCompress(node.children[0], depth + 1);
            RecursiveCompress(node.children[1], depth + 1);
            RecursiveCompress(node.children[2], depth + 1);
            RecursiveCompress(node.children[3], depth + 1);
        }
        else
        {
            treeDepth = Math.Max(depth, treeDepth);
        }
    }

    private void CompressByPercentage()
    {
        treeNodes = tree!.treeNodes;
        treeDepth = tree.treeDepth;

        int pixelsInImage = sourceImage!.Width * sourceImage!.Height;
        int uncompressedPixels = pixelsInImage;
        int targetSize = (int) (originalSize * compressionTarget);

        MemoryStream imageStream = new((int) originalSize);
        outImage!.Save(imageStream, encoder!);

        while (imageStream.Length > targetSize)
        {
            int targetUncompressedPixels = uncompressedPixels - (int) ((imageStream.Length - targetSize) * uncompressedPixels / imageStream.Length);

            //Console.WriteLine(uncompressedPixels.ToString() + " > " + targetUncompressedPixels.ToString() + " | " + tree.leavesCount.ToString());

            do
            {
                if (tree.leavesCount <= 1) break;

                Region2Int region = tree.Pop().region;
                
                if (region.area < 4 * minBlockSize || region.size.x <= 1 || region.size.y <= 1)
                {
                    uncompressedPixels -= region.area - 1;
                }
                else
                {
                    uncompressedPixels -= 3;
                }

                NormalizeRegion(region, sourceImage!.Frames.RootFrame, outImage!.Frames.RootFrame);
            }
            while (uncompressedPixels > targetUncompressedPixels);

            if (tree.leavesCount <= 1) break;

            imageStream.SetLength(0);
            outImage.Save(imageStream, encoder!);
        }
    }

    private void GenerateGif()
    {
        gif = sourceImage!.Clone();
        gif.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay = framerate;
        gif.Metadata.GetGifMetadata().RepeatCount = 0;

        List<QuadTree.Node> currentNodes = new List<QuadTree.Node>([tree!.rootNode]);
        List<QuadTree.Node> nextNodes = new List<QuadTree.Node>(currentNodes.Count * 4);
        int currentDepth = 0;

        while (currentNodes.Count > 0)
        {
            ImageFrame<Rgba32> currentFrame = gif.Frames[currentDepth];

            for (int i = 0; i < currentNodes.Count; i++)
            {
                QuadTree.Node currentNode = currentNodes[i];

                NormalizeRegion(currentNode.content.region, sourceImage.Frames.RootFrame, currentFrame);

                if (currentNode.compressed) continue;

                for (int j = 0; j < currentNode.children.Length; j++)
                {
                    QuadTree.Node? childNode = currentNode.children[j];

                    if (childNode is null) continue;

                    nextNodes.Add(childNode);
                }
            }

            currentNodes = nextNodes;
            nextNodes = new List<QuadTree.Node>(currentNodes.Count * 4);
            currentDepth++;

            gif.Frames.AddFrame(gif.Frames[currentDepth - 1]);
            gif.Frames[currentDepth].Metadata.GetGifMetadata().FrameDelay = framerate;
        }
    }

    private void NormalizeRegion(Region2Int region, ImageFrame<Rgba32> sourceImage, ImageFrame<Rgba32> outImage)
    {
        if (region.area <= 1) return;

        int regionWidth = region.end.x - region.start.x + 1;
        int regionHeight = region.end.y - region.start.y + 1;
        int regionSize = regionWidth * regionHeight;

        int r = 0;
        int g = 0;
        int b = 0;

        for (int j = region.start.y; j <= region.end.y; j++)
        {
            for (int i = region.start.x; i <= region.end.x; i++)
            {
                Rgba32 pixel = sourceImage![i, j];
                r += pixel.R;
                g += pixel.G;
                b += pixel.B;
            }
        }

        r /= regionSize;
        g /= regionSize;
        b /= regionSize;

        //colorCache.SetCache(region, new ColorCache() { averageColor = new Rgba32((byte) r, (byte) g, (byte) b), pixelCount = regionSize });

        Rgba32 color = new Rgba32((byte) r, (byte) g, (byte) b);

        for (int j = region.start.y; j <= region.end.y; j++)
        {
            for (int i = region.start.x; i <= region.end.x; i++)
            {
                outImage![i, j] = color;
            }
        }
    }

    private struct ColorCache
    {
        public Rgba32 averageColor;
        public int pixelCount;
    }
}
