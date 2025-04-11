namespace ImageCompressor.ErrorCalculation;

using System.Diagnostics.CodeAnalysis;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Util;

public abstract class ErrorCalculator
{
    protected Image<Rgba32>? image = null;

    // This function should be overloaded when implementing a cache to reset the internal cache
    public virtual void LoadImage(Image<Rgba32> image)
    {
        this.image = image;
    }

    public abstract string Name { get; }
    public abstract double CalculateError(Region2Int region);
}

public class MaxPixelDifferenceCalculator : ErrorCalculator
{
    public override string Name { get => "Max Pixel Difference"; }

    public override void LoadImage(Image<Rgba32> image)
    {
        base.LoadImage(image);
    }

    public override double CalculateError(Region2Int region)
    {
        byte minR = byte.MaxValue;
        byte minG = byte.MaxValue;
        byte minB = byte.MaxValue;

        byte maxR = byte.MinValue;
        byte maxG = byte.MinValue;
        byte maxB = byte.MinValue;
        
        for (int j = region.start.y; j <= region.end.y; j++)
        {
            for (int i = region.start.x; i < region.end.x; i++)
            {
                Rgba32 c = image![i, j];

                if (c.R > maxR) maxR = c.R;
                if (c.G > maxG) maxG = c.G;
                if (c.B > maxB) maxB = c.B;

                if (c.R < minR) minR = c.R;
                if (c.G < minG) minG = c.G;
                if (c.B < minB) minB = c.B;
            }
        }

        return (maxR + maxG + maxB - minR - minG - minB) / (255.0d * 3.0d);
    }
}

public class VarianceCalculator : ErrorCalculator
{
    public override string Name { get => "Variance"; }

    public override double CalculateError(Region2Int region)
    {
        if (image == null)
            return 0;

        long sumR = 0, sumG = 0, sumB = 0;
        int count = 0;

        for (int j = region.start.y; j <= region.end.y; j++)
        {
            for (int i = region.start.x; i <= region.end.x; i++)
            {
                Rgba32 c = image![i, j];
                sumR += c.R;
                sumG += c.G;
                sumB += c.B;
                count++;
            }
        }

        if (count == 0)
            return 0;

        double meanR = (double)sumR / count;
        double meanG = (double)sumG / count;
        double meanB = (double)sumB / count;

        double sqDiffR = 0, sqDiffG = 0, sqDiffB = 0;
        for (int j = region.start.y; j <= region.end.y; j++)
        {
            for (int i = region.start.x; i <= region.end.x; i++)
            {
                Rgba32 c = image![i, j];
                sqDiffR += Math.Pow(c.R - meanR, 2);
                sqDiffG += Math.Pow(c.G - meanG, 2);
                sqDiffB += Math.Pow(c.B - meanB, 2);
            }
        }

        // Compute average variance for each color channel
        double varianceR = sqDiffR / count;
        double varianceG = sqDiffG / count;
        double varianceB = sqDiffB / count;

        // Max deviation possible is 127.5 and the variance is 127.5^2 = 16256.25.
        double maxVariance = 16256.25;

        double normalizedVarianceR = varianceR / maxVariance;
        double normalizedVarianceG = varianceG / maxVariance;
        double normalizedVarianceB = varianceB / maxVariance;

        double normalizedVariance = (normalizedVarianceR + normalizedVarianceG + normalizedVarianceB) / 3.0;

        return Math.Min(1.0, Math.Max(0.0, normalizedVariance));
    }
}

public class MeanAbsoluteDeviationCalculator : ErrorCalculator
{
    public override string Name { get => "Mean Absolute Deviation (MAD)"; }

    public override double CalculateError(Region2Int region)
    {
        if (image == null) return 0;

        long sumR = 0, sumG = 0, sumB = 0;
        int count = 0;
        for (int j = region.start.y; j <= region.end.y; j++)
        {
            for (int i = region.start.x; i <= region.end.x; i++)
            {
                Rgba32 c = image[i, j];
                sumR += c.R;
                sumG += c.G;
                sumB += c.B;
                count++;
            }
        }

        if (count == 0) return 0;

        double meanR = (double)sumR / count;
        double meanG = (double)sumG / count;
        double meanB = (double)sumB / count;

        double absDiffR = 0, absDiffG = 0, absDiffB = 0;
        for (int j = region.start.y; j <= region.end.y; j++)
        {
            for (int i = region.start.x; i <= region.end.x; i++)
            {
                Rgba32 c = image[i, j];
                absDiffR += Math.Abs(c.R - meanR);
                absDiffG += Math.Abs(c.G - meanG);
                absDiffB += Math.Abs(c.B - meanB);
            }
        }

        double madR = absDiffR / count;
        double madG = absDiffG / count;
        double madB = absDiffB / count;

        double madRGB = (madR + madG + madB) / 3.0;

        double normalized = madRGB / 127.5;

        if (normalized < 0) normalized = 0;
        if (normalized > 1) normalized = 1;

        return normalized;
    }
}

public class EntropyCalculator : ErrorCalculator
{
    public override string Name { get => "Entropy"; }

    public override double CalculateError(Region2Int region)
    {
        if (image == null) return 0;

        int[] freqR = new int[256];
        int[] freqG = new int[256];
        int[] freqB = new int[256];

        int count = 0;
        for (int j = region.start.y; j <= region.end.y; j++)
        {
            for (int i = region.start.x; i <= region.end.x; i++)
            {
                Rgba32 c = image[i, j];
                freqR[c.R]++;
                freqG[c.G]++;
                freqB[c.B]++;
                count++;
            }
        }

        if (count == 0) return 0;

        double hR = CalculateChannelEntropy(freqR, count);
        double hG = CalculateChannelEntropy(freqG, count);
        double hB = CalculateChannelEntropy(freqB, count);

        double hRGB = (hR + hG + hB) / 3.0;

        double normalized = hRGB / 8.0;

        if (normalized < 0) normalized = 0;
        if (normalized > 1) normalized = 1;

        return normalized;
    }

    private double CalculateChannelEntropy(int[] freq, int totalCount)
    {
        double h = 0.0;
        for (int i = 0; i < 256; i++)
        {
            if (freq[i] == 0) continue;

            double p = (double)freq[i] / totalCount;
            // sum= -p * log2(p)
            h += -p * Math.Log2(p);
        }
        return h;
    }
}

// Katanya seluruh Error Calculation disini, jadi yowes ini juga disini, help code nya panjang T^T
public class SSIMCalculator : ErrorCalculator
{
    public override string Name { get => "SSIM"; }

    public override double CalculateError(Region2Int region)
    {
        // SSIM disini berarti self-comparison
        if (image == null) return 0;

        // Typical SSIM constants for 8-bit images
        const double L = 255.0;     // max pixel value
        const double k1 = 0.01;
        const double k2 = 0.03;
        double C1 = (k1 * L) * (k1 * L);  // (0.01*255)^2 ~ 6.5025
        double C2 = (k2 * L) * (k2 * L);  // (0.03*255)^2 ~ 58.5225

        int count = 0;
        long sumR = 0, sumG = 0, sumB = 0;
        for (int j = region.start.y; j <= region.end.y; j++)
        {
            for (int i = region.start.x; i <= region.end.x; i++)
            {
                Rgba32 c = image[i, j];
                sumR += c.R;
                sumG += c.G;
                sumB += c.B;
                count++;
            }
        }
        if (count == 0) return 0;

        double meanR = (double)sumR / count;
        double meanG = (double)sumG / count;
        double meanB = (double)sumB / count;

        // Compute variances (for single-image self-comparison, cross-cov ~ var)
        double varR = 0, varG = 0, varB = 0;
        for (int j = region.start.y; j <= region.end.y; j++)
        {
            for (int i = region.start.x; i <= region.end.x; i++)
            {
                Rgba32 c = image[i, j];
                varR += Math.Pow(c.R - meanR, 2);
                varG += Math.Pow(c.G - meanG, 2);
                varB += Math.Pow(c.B - meanB, 2);
            }
        }
        varR /= count;
        varG /= count;
        varB /= count;

        // for this (self  comparison) cross-cov = var.
        double covR = varR, covG = varG, covB = varB;

        double ssimR = ComputeSSIM(meanR, meanR, varR, varR, covR, C1, C2);
        double ssimG = ComputeSSIM(meanG, meanG, varG, varG, covG, C1, C2);
        double ssimB = ComputeSSIM(meanB, meanB, varB, varB, covB, C1, C2);

        double ssimRGB = (ssimR + ssimG + ssimB) / 3.0;

        if (ssimRGB < 0) ssimRGB = 0;
        if (ssimRGB > 1) ssimRGB = 1;

        return ssimRGB;
    }

    private double ComputeSSIM(
        double muX, double muY,
        double varX, double varY,
        double covXY,
        double C1, double C2
    )
    {
        double numerator   = (2.0 * muX * muY + C1) * (2.0 * covXY + C2);
        double denominator = (muX * muX + muY * muY + C1) * (varX + varY + C2);

        if (denominator == 0.0) return 1.0; // mudah-mudahan gak perlu, but just in case

        return numerator / denominator;
    }
}
