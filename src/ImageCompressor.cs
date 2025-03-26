namespace ImageCompressor;

using System.Drawing;
using System.Drawing.Imaging;
using ErrorCalculation;

class ImageCompressor
{
    static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");
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
        
        if (compressionPercentage == 0.0d)
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
        long regionWidth = regionEnd.x - regionStart.x + 1;
        long regionHeight = regionEnd.y - regionStart.y + 1;
        Color[,] pixels = new Color[regionWidth, regionHeight];

        for (long i = regionStart.x; i <= regionEnd.x; i++)
        {
            for (long j = regionStart.y; j <= regionEnd.y; j++)
            {
                pixels[i, j] = rawImage.GetPixel((int) i, (int) j);
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
                    rawImage.SetPixel((int) i, (int) j, averagePixel);
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
                Color pixel = rawImage.GetPixel((int) i, (int) j);
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
