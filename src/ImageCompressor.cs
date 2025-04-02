namespace ImageCompressor;

using System.Drawing;
using System.Drawing.Imaging;
using Tree;
using ErrorCalculation;
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
        new MaxPixelDifferenceCalculator(),
        new VarianceCalculator(),
        new MeanAbsoluteDeviationCalculator(),
        new EntropyCalculator(),
        //new SSIMCalculator()
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
    private QuadTree? tree = null;

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
            CompressByThreshold(new(new(0, 0), new(rawImage.Size.Width - 1, rawImage.Size.Height - 1)));
        }
        else
        {
            CompressByPercentage();
        }

        rawImage.Save(outPath, ImageFormat.Png);
    }

    private void CompressByThreshold(Region2Int region)
    {
        if (region.area <= 1) return;

        double error = errorCalculator.CalculateError(region);
        
        if (error < threshold)
        {
            NormalizeRegion(region);
        }
        else if (region.area >= 4 * minBlockSize && region.size.x > 1 && region.size.y > 1)
        {
            CompressByThreshold(new(region.start, region.start + region.size / 2 - new Vector2Int(1, 1)));
            CompressByThreshold(new(region.start + new Vector2Int(region.size.x / 2, 0), region.start + new Vector2Int(region.size.x - 1, region.size.y / 2 - 1)));
            CompressByThreshold(new(region.start + new Vector2Int(0, region.size.y / 2), region.start + new Vector2Int(region.size.x / 2 - 1, region.size.y - 1)));
            CompressByThreshold(new(region.start + region.size / 2, region.end));
        }
    }

    private void CompressByPercentage()
    {
        tree = new QuadTree(rawImage!, minBlockSize, errorCalculator);

        int pixelsInImage = rawImage!.Width * rawImage!.Height;
        int uncompressedPixels = pixelsInImage;
        int targetSize = (int) (originalSize * compressionPercentage);

        MemoryStream imageStream = new((int) originalSize);
        rawImage.Save(imageStream, ImageFormat.Png);

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

                NormalizeRegion(region);
            }
            while (uncompressedPixels > targetUncompressedPixels);

            if (tree.leavesCount <= 1) break;

            imageStream.SetLength(0);
            rawImage.Save(imageStream, ImageFormat.Png);
        }
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
