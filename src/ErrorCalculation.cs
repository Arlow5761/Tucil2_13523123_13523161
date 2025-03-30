namespace ImageCompressor.ErrorCalculation;

using System.Drawing;

public abstract class ErrorCalculator
{
    public abstract string Name { get; }
    public abstract double CalculateError(Color[,] pixels);
}

public class MaxPixelDifferenceCalculator : ErrorCalculator
{
    public override string Name { get => "Max Pixel Difference"; }

    public override double CalculateError(Color[,] pixels)
    {
        uint minR = uint.MaxValue;
        uint minG = uint.MaxValue;
        uint minB = uint.MaxValue;

        uint maxR = uint.MinValue;
        uint maxG = uint.MinValue;
        uint maxB = uint.MinValue;

        for (int i = 0; i < pixels.GetLength(0); i++)
        {
            for (int j = 0; j < pixels.GetLength(1); j++)
            {
                Color c = pixels[i, j];

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