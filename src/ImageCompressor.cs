namespace ImageCompressor;

using SixLabors.ImageSharp;
using Tree;
using ErrorCalculation;
using Util;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats;

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

        do
        {
            Console.WriteLine("Enter an absolute output path for GIF:");
            compressor.gifPath = Console.ReadLine() ?? "";
        }
        while (compressor.gifPath == "");

        Console.WriteLine("\nCompressing. Please Wait...\n");

        compressor.Run();
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
    public double compressionPercentage;
    public string outPath;
    public string gifPath;

    private byte[]? rawData = null;
    private long originalSize = 0;
    private Image<Rgba32>? sourceImage = null;
    public Image<Rgba32>? outImage = null;
    public Image<Rgba32>? gif = null;
    private IImageEncoder? encoder = null;
    private QuadTree? tree = null;
    private int gifThrottle = 0;

    public ImageCompressor()
    {
        imagePath = "";
        errorCalculator = availableErrorCalculators[0];
        threshold = 0;
        minBlockSize = 0;
        compressionPercentage = 0;
        outPath = "";
        gifPath = "";
    }

    public void Run()
    {
        rawData = File.ReadAllBytes(imagePath);
        originalSize = rawData.LongLength;

        sourceImage = Image.Load<Rgba32>(imagePath);
        outImage = Image.Load<Rgba32>(imagePath);
        gif = Image.Load<Rgba32>(imagePath);

        outImage.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay = framerate;
        gif.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay = framerate;
        gif.Metadata.GetGifMetadata().RepeatCount = 0;

        encoder = outImage.Configuration.ImageFormatsManager.GetEncoder(outImage.Metadata.DecodedImageFormat!);

        errorCalculator.LoadImage(sourceImage);

        gifThrottle = sourceImage!.Width * sourceImage!.Height / 32;
        
        if (compressionPercentage <= 0.0d)
        {
            CompressByThreshold(new(new(0, 0), new(sourceImage.Size.Width - 1, sourceImage.Size.Height - 1)));
        }
        else
        {
            CompressByPercentage();
        }

        gif.Frames.AddFrame(outImage.Frames.RootFrame);

        outImage.Save(outPath, encoder);
        gif.SaveAsGif(gifPath);
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
        tree = new QuadTree(sourceImage!, minBlockSize, errorCalculator);

        int pixelsInImage = sourceImage!.Width * sourceImage!.Height;
        int uncompressedPixels = pixelsInImage;
        int targetSize = (int) (originalSize * compressionPercentage);

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

                NormalizeRegion(region);
            }
            while (uncompressedPixels > targetUncompressedPixels);

            if (tree.leavesCount <= 1) break;

            imageStream.SetLength(0);
            outImage.Save(imageStream, encoder!);
        }
    }

    private void NormalizeRegion(Region2Int region)
    {
        if (region.area <= 1) return;

        int regionWidth = region.end.x - region.start.x + 1;
        int regionHeight = region.end.y - region.start.y + 1;

        int r = 0;
        int g = 0;
        int b = 0;

        for (int i = region.start.x; i <= region.end.x; i++)
        {
            for (int j = region.start.y; j <= region.end.y; j++)
            {
                Rgba32 pixel = sourceImage![i, j];
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
                outImage![i, j] = new Rgba32((byte) r, (byte) g, (byte) b);
            }
        }

        gifThrottle -= region.area;

        if (gifThrottle <= 0)
        {
            gif!.Frames.AddFrame(outImage!.Frames.RootFrame);
            gifThrottle = sourceImage!.Width * sourceImage!.Height / 32;
        }
    }
}
