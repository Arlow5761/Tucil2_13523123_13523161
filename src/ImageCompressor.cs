namespace ImageCompressor;

using System.Drawing;
using System.Drawing.Imaging;
using ErrorCalculation;

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
        
        if (compressionPercentage <= 0.0d)
        {
            CompressByThreshold((0, 0), (rawImage.Size.Width - 1, rawImage.Size.Height - 1));
        }
        else
        {
            CompressByPercentage();
        }

        rawImage.Save(outPath, ImageFormat.Png);
    }

    private void CompressByThreshold((long x, long y) regionStart, (long x, long y) regionEnd)
    {
        if (regionStart.x >= regionEnd.x || regionStart.y >= regionEnd.y) return;

        long regionWidth = regionEnd.x - regionStart.x + 1;
        long regionHeight = regionEnd.y - regionStart.y + 1;
        Color[,] pixels = new Color[regionWidth, regionHeight];

        for (long i = regionStart.x; i <= regionEnd.x; i++)
        {
            for (long j = regionStart.y; j <= regionEnd.y; j++)
            {
                pixels[i, j] = rawImage!.GetPixel((int) i, (int) j);
            }
        }

        double error = errorCalculator.CalculateError(pixels);

        if (error < threshold)
        {
            Color averagePixel = GetAveragePixel(regionStart, regionEnd);

            for (long i = regionStart.x; i <= regionEnd.x; i++)
            {
                for (long j = regionStart.y; j <= regionEnd.y; j++)
                {
                    rawImage!.SetPixel((int) i, (int) j, averagePixel);
                }
            }
        }
        else if (regionWidth >= 2 * minBlockSize && regionHeight >= 2 * minBlockSize)
        {
            CompressByThreshold(regionStart, (regionEnd.x / 2, regionEnd.y / 2));
            CompressByThreshold((regionStart.x / 2 + 1, regionStart.y), (regionEnd.x, regionEnd.y / 2));
            CompressByThreshold((regionStart.x, regionStart.y / 2 + 1), (regionEnd.x / 2, regionEnd.y));
            CompressByThreshold((regionStart.x / 2 + 1, regionStart.y / 2 + 1), (regionEnd.x, regionEnd.y));
        }
    }

    private void CompressByPercentage()
    {

    }

    private Color GetAveragePixel((long x, long y) regionStart, (long x, long y) regionEnd)
    {
        long regionWidth = regionEnd.x - regionStart.x + 1;
        long regionHeight = regionEnd.y - regionStart.y + 1;

        long r = 0;
        long g = 0;
        long b = 0;

        for (long i = regionStart.x; i <= regionEnd.x; i++)
        {
            for (long j = regionStart.y; j <= regionEnd.y; j++)
            {
                Color pixel = rawImage!.GetPixel((int) i, (int) j);
                r += pixel.R;
                g += pixel.G;
                b += pixel.B;
            }
        }

        long regionSize = regionWidth * regionHeight;

        r /= regionSize;
        g /= regionSize;
        b /= regionSize;

        return Color.FromArgb((int) r, (int) g, (int) b);
    }
}
