using System;
using System.Collections.Generic;
using System.Linq;

namespace _86BoxManager.Tools;

public class ProgressCalculator
{
    private readonly double[] _percentages;

    /// <summary>
    /// Initializes a new instance of the ProgressCalculator class.
    /// </summary>
    /// <param name="percentages">A dictionary containing the percentage allocation for each operation.</param>
    public ProgressCalculator(double[] percentages)
    {
        _percentages = percentages;
    }

    /// <summary>
    /// Calculates the progress percentage for a given operation, including the cumulative progress of previous operations.
    /// </summary>
    /// <param name="operation">The operation number (e.g., Operation.Download86Box).</param>
    /// <param name="progress">The current progress value (e.g., bytes downloaded or files processed).</param>
    /// <param name="total">The total value for the operation (e.g., total bytes or total files).</param>
    /// <returns>The cumulative progress percentage for the specified operation.</returns>
    /// <exception cref="ArgumentException">Thrown when an invalid operation is specified or required parameters are missing.</exception>
    public double CalculateProgress(int operation, double progress, double total)
    {
        // Calculate the progress percentage for the current operation
        double currentProgressPercentage = (progress / total) * _percentages[operation];

        // Calculate the cumulative progress of previous operations
        double cumulativePreviousPercentage = 0;
        for (int c = 0; c < operation; c++)
            cumulativePreviousPercentage += _percentages[c];

        // Add the current progress percentage to the cumulative previous percentage
        double totalProgressPercentage = cumulativePreviousPercentage + currentProgressPercentage;

        return totalProgressPercentage;
    }


}
