namespace ImageCompressor.ErrorCalculation;

using System.Drawing;
using Util;

public abstract class ErrorCalculator
{
    protected Bitmap? image = null;

    // This function should be overloaded when implementing a cache to reset the internal cache
    public virtual void LoadImage(Bitmap image)
    {
        this.image = image;
    }

    public abstract string Name { get; }
    public abstract double CalculateError(Region2Int region);
}

public class MaxPixelDifferenceCalculator : ErrorCalculator
{
    public override string Name { get => "Max Pixel Difference"; }

    public override double CalculateError(Region2Int region)
    {
        uint minR = uint.MaxValue;
        uint minG = uint.MaxValue;
        uint minB = uint.MaxValue;

        uint maxR = uint.MinValue;
        uint maxG = uint.MinValue;
        uint maxB = uint.MinValue;

        for (int i = region.start.x; i <= region.end.x; i++)
        {
            for (int j = region.start.y; j < region.end.y; j++)
            {
                Color c = image!.GetPixel(i, j);

                if (c.R > maxR) maxR = c.R;
                if (c.G > maxG) maxG = c.G;
                if (c.B > maxB) maxB = c.B;

                if (c.R < minR) minR = c.R;
                if (c.G < minG) minG = c.G;
                if (c.B < minB) minB = c.B;
            }
        }

        return (maxR + maxG + maxB - minR - minG - minB) / (256.0d * 3.0d);
    }
}