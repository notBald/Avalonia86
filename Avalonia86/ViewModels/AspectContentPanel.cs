using Avalonia;
using Avalonia.Controls;
using System;

namespace Avalonia86.ViewModels;

/// <summary>
/// A custom layout panel designed for a specific UI scenario: Enforcing the width of an image to 4 / 3 aspect ratio.
/// 
/// Behavior:
/// - Hosts exactly 1 child:
///     - The child is typically an image.
/// - Image sizing rules:
///     - If <see cref="DoAspectCorrection"/> returns true, the image is forced to 4:3 aspect ratio.
///     - Other images are given their natural aspect ratio.
///     - Images are sized up to the size of the container, with no cropping.
///      - I.e. if the image is sized up so either the left/right edges meets the container, or top/bottom.
/// 
/// Notes:
/// - Intended for one-off use; not a general-purpose layout container.
/// </summary>
public class AspectContentPanel : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count != 1)
            return new Size();

        var child = Children[0];

        // Let the child report its natural DesiredSize (if any) without constraints.
        child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var ds = child.DesiredSize;

        // Choose target aspect ratio
        double targetAspect = ChooseAspect(child, availableSize, ds);

        // Compute intended contained size inside the available panel size.
        // Use child's DesiredSize as a preferred baseline only if both are finite and > 0.
        Size? preferred = IsFinitePositive(ds) ? ds : (Size?)null;
        var intended = FitWithin(availableSize, targetAspect, preferred);

        // Re-measure the child with the intended size
        child.Measure(intended);

        // Our desired size is what we intend to occupy (bounded by available)
        return intended;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count != 1)
            return finalSize;

        var child = Children[0];

        // Reuse the child's already computed DesiredSize as a hint
        var ds = child.DesiredSize;

        // Choose aspect ratio using the *final* panel size as the container fallback.
        double targetAspect = ChooseAspect(child, finalSize, ds);

        // Contain-fit into the final panel size
        Size? preferred = IsFinitePositive(ds) ? ds : (Size?)null;
        var arrangedSize = FitWithin(finalSize, targetAspect, preferred);

        // Center within the panel
        double x = (finalSize.Width - arrangedSize.Width) / 2.0;
        double y = (finalSize.Height - arrangedSize.Height) / 2.0;

        child.Arrange(new Rect(new Point(x, y), arrangedSize));
        return finalSize;
    }

    /// <summary>
    /// Implements the 3-step aspect selection:
    /// 1) Force 4:3 if DoAspectCorrection(child) is true.
    /// 2) Else use child's "natural" aspect from DesiredSize (if available).
    /// 3) Else container (panel) aspect from available/final size.
    /// </summary>
    private double ChooseAspect(Control child, Size containerSize, Size childDesired)
    {
        if (DoAspectCorrection(child))
            return 4.0 / 3.0;

        if (IsFinitePositive(childDesired))
        {
            double ar = childDesired.Width / childDesired.Height;
            if (ar > 0) return ar;
        }

        // Fallback to the panel (container) aspect
        var containerAR = GetAspect(containerSize);
        return containerAR > 0 ? containerAR : 1.0; // safe default
    }

    /// <summary>
    /// Contain-fit a box of given aspect (width/height) within 'bounds'.
    /// If both dimensions of 'bounds' are infinite, derive from 'preferred' if possible,
    /// otherwise use an arbitrary baseline (height=100).
    /// </summary>
    private static Size FitWithin(Size bounds, double aspect, Size? preferred)
    {
        if (aspect <= 0)
            return new Size(0, 0);

        bool wInf = double.IsPositiveInfinity(bounds.Width);
        bool hInf = double.IsPositiveInfinity(bounds.Height);

        if (wInf && hInf)
        {
            // Completely unconstrained: use preferred if available, adjusted to aspect.
            if (preferred is Size p && IsFinitePositive(p))
            {
                double pAspect = p.Width / p.Height;
                if (pAspect > 0)
                {
                    if (Math.Abs(pAspect - aspect) < 1e-6)
                        return p;

                    // Adjust preferred to the target aspect in a stable way.
                    if (aspect > pAspect)
                    {
                        // Wider target => keep width, adjust height.
                        double w = p.Width;
                        double h = w / aspect;
                        return new Size(w, h);
                    }
                    else
                    {
                        // Taller target => keep height, adjust width.
                        double h = p.Height;
                        double w = h * aspect;
                        return new Size(w, h);
                    }
                }
            }
            // Last resort arbitrary baseline
            double baseH = 100.0;
            return new Size(baseH * aspect, baseH);
        }

        if (wInf && !hInf)
        {
            double h = bounds.Height;
            double w = h * aspect;
            return new Size(w, h);
        }

        if (!wInf && hInf)
        {
            double w = bounds.Width;
            double h = w / aspect;
            return new Size(w, h);
        }

        // Both finite: classic contain-fit based on aspect comparison.
        double containerAR = GetAspect(bounds);
        if (containerAR <= 0)
            return new Size(0, 0);

        if (aspect > containerAR)
        {
            // Image is wider than container => constrain by width
            double w = bounds.Width;
            double h = w / aspect;
            return new Size(w, h);
        }
        else
        {
            // Image is taller (or equal) => constrain by height
            double h = bounds.Height;
            double w = h * aspect;
            return new Size(w, h);
        }
    }

    private static double GetAspect(Size s)
    => (s.Width > 0 && s.Height > 0) ? (s.Width / s.Height) : 0.0;

    private static bool IsFinitePositive(Size s)
        => s.Width > 0 && s.Height > 0 &&
           !double.IsInfinity(s.Width) && !double.IsInfinity(s.Height) &&
           !double.IsNaN(s.Width) && !double.IsNaN(s.Height);

    /// <summary>
    /// Determines whether aspect ratio correction should be applied to the image.
    /// 
    /// This method checks if the control's DataContext is a MainModel, and if a Machine
    /// and a SelectedImage are present. If so, it inspects the filename of the selected image.
    /// 
    /// PNG images are forced into a 4:3 aspect ratio because they typically represent
    /// screenshots or assets from CGA, EGA, or VGA games, and there is no reliable way
    /// to determine their true aspect ratio from metadata or pixel dimensions alone.
    /// 
    /// JPEG and other formats are assumed to be user-supplied and are displayed using
    /// their natural aspect ratio. A PNG image supplied by the user will also be forced
    /// into 4:3, as the format itself does not provide sufficient information to infer
    /// the correct aspect ratio.
    /// </summary>
    private bool DoAspectCorrection(Control ctrl)
    {
        if (ctrl.DataContext is MainModel m && m.Machine is not null && m.Machine.SelectedImage is not null)
        {
            // We assume that if SelectedImage is not null, then SelectedImageIndex and Images
            // are valid and consistent, since SelectedImage is derived from that list.
            var name = m.Machine.Images[m.Machine.SelectedImageIndex] ?? "";

            return name.EndsWith(".png", StringComparison.InvariantCultureIgnoreCase);
        }

        return false;
    }
}
