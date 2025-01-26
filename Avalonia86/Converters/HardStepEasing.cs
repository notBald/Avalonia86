using Avalonia.Animation.Easings;
using System;

namespace Avalonia86.Converters;

public class HardStepEasing : Easing
{
    private double _step = .25;

    public int NSteps
    {
        get => (int) (1.0 / _step);
        set => _step = 1.0 / value;
    }

    public override double Ease(double progress)
    {
        return Math.Floor(progress / _step) * _step;
    }
}
