namespace ImageCompressor.ErrorCalculation;

using System.Drawing;

public abstract class ErrorCalculator
{
    public abstract string Name { get; }
    public abstract double CalculateError(Color[][] pixels);
}