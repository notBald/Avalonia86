using System;
using Avalonia;
using Avalonia.Controls;

namespace Avalonia86.ViewModels;

/// <summary>
/// A custom layout panel designed for a specific UI scenario: stacking an image on top of a content area,
/// with adaptive behavior based on available space and image characteristics.
/// 
/// Behavior:
/// - Hosts exactly two children:
///     - The first child is treated as the "image container."
///     - The second child is the "content area."
/// - Image sizing rules:
///     - If <see cref="DoAspectCorrection"/> returns true (typically for PNG images), the image is forced to 4:3 aspect ratio.
///     - Otherwise, the image uses its natural aspect ratio.
///     - The image height is constrained by:
///         - An absolute minimum (<see cref="AbsoluteMinImageHeight"/>).
///         - A relative minimum ratio (<see cref="MinImageHeightRatio"/> of the panel height).
///         - A relative maximum ratio (<see cref="ImageMaxHeightRatio"/> of the panel height).
/// - Content behavior:
///     - If the remaining space after sizing the image is less than <see cref="CollapseThreshold"/>, the content collapses.
///     - Otherwise, the content occupies the remaining vertical space.
/// 
/// Notes:
/// - This panel assumes exactly two children and does not support arbitrary child collections.
/// - Intended for one-off use; not a general-purpose layout container.
/// </summary>
public class ConditionalContentPanel : Panel
{
    public static readonly StyledProperty<double> CollapseThresholdProperty =
        AvaloniaProperty.Register<ConditionalContentPanel, double>(
            nameof(CollapseThreshold),
            100,
            validate: v => !double.IsNaN(v) && !double.IsInfinity(v) && v >= 0);

    /// <summary>
    /// If the available space falls below this threshold, the content control is collapsed 
    /// and the image takes over the full height of the panel.
    /// </summary>
    public double CollapseThreshold
    {
        get => GetValue(CollapseThresholdProperty);
        set => SetValue(CollapseThresholdProperty, value);
    }


    public static readonly StyledProperty<double> ImageMaxHeightRatioProperty =
        AvaloniaProperty.Register<ConditionalContentPanel, double>(
            nameof(ImageMaxHeightRatio),
            0.40, // 40% by default
            validate: v => !double.IsNaN(v) && !double.IsInfinity(v) && v >= 0 && v <= 1);

    /// <summary>
    /// Maximum image height as a fraction of the total container height (0..1).
    /// </summary>
    public double ImageMaxHeightRatio
    {
        get => GetValue(ImageMaxHeightRatioProperty);
        set => SetValue(ImageMaxHeightRatioProperty, value);
    }

    public static readonly StyledProperty<double> MinImageHeightRatioProperty =
            AvaloniaProperty.Register<ConditionalContentPanel, double>(
                nameof(MinImageHeightRatio),
                0.2,
                validate: v => !double.IsNaN(v) && !double.IsInfinity(v) && v >= 0 && v <= 1);

    /// <summary>
    /// The minimum height ratio (relative to the total available height) that the image
    /// should occupy. This prevents the image from becoming too small when space is limited,
    /// ensuring it remains visually meaningful in the layout.
    /// </summary>
    public double MinImageHeightRatio
    {
        get => GetValue(MinImageHeightRatioProperty);
        set => SetValue(MinImageHeightRatioProperty, value);
    }


    /// <summary>
    /// An absolute fallback minimum height (in pixels) for the image.
    /// Used in conjunction with MinImageHeightRatio to ensure the image never collapses
    /// below a usable size, even in extremely constrained layouts.
    /// </summary>
    private const int AbsoluteMinImageHeight = 10;

    static ConditionalContentPanel()
    {
        AffectsMeasure<ConditionalContentPanel>(
            CollapseThresholdProperty,
            MinImageHeightRatioProperty,
            ImageMaxHeightRatioProperty);

        AffectsArrange<ConditionalContentPanel>(
            CollapseThresholdProperty,
            MinImageHeightRatioProperty,
            ImageMaxHeightRatioProperty);
    }

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

    protected override Size MeasureOverride(Size availableSize)
    {
        // The panel expects exactly two children. If not, be defensive and return a zero size.
        if (Children.Count < 2)
        {
            if (Children.Count == 1)
            {
                var lone = Children[0];
                var w = Math.Max(0, availableSize.Width - lone.Margin.Left - lone.Margin.Right);
                var h = Math.Max(0, availableSize.Height - lone.Margin.Top - lone.Margin.Bottom);
                lone.Measure(new Size(w, h));
            }
            return new Size();
        }

        // Get references to the image and content controls.
        var image = Children[0];
        var content = Children[1];

        // Measure the image with infinite height to determine its natural size.
        var imageMeasureWidth = Math.Max(0, availableSize.Width - image.Margin.Left - image.Margin.Right);
        image.Measure(new Size(imageMeasureWidth, double.PositiveInfinity));

        // Use the helper method to compute all necessary layout sizes and the collapse decision.
        var layout = ComputeLayoutForMeasure(
            availableSize,
            image.DesiredSize,
            image.Margin,
            content.Margin,
            doAspectCorrection: DoAspectCorrection(image));

        // Based on the layout decision, measure the content control.
        if (layout.CollapseContent)
        {
            // If content is collapsed, measure it with a zero size.
            content.Measure(new Size(0, 0));
        }
        else
        {
            // Measure content with the calculated available space.
            double contentMeasureWidth = Math.Max(0, availableSize.Width - content.Margin.Left - content.Margin.Right);
            content.Measure(new Size(contentMeasureWidth, Math.Max(0, layout.ContentHeightNoMargins))); // <- clamp
        }

        // The desired size of the panel is the full available width and the combined height of
        // the image (including its margins) plus the measured content (including its margins).
        double totalHeight = layout.ImageHeightNoMargins + image.Margin.Top + image.Margin.Bottom;
        if (!layout.CollapseContent)
        {
            totalHeight += content.DesiredSize.Height + content.Margin.Top + content.Margin.Bottom;
        }

        return new Size(availableSize.Width, totalHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // If there are fewer than two children, there's nothing to arrange.
        if (Children.Count < 2)
        {
            // Still call Arrange on the child (if one exists), as per layout protocol.
            if (Children.Count == 1)
                Children[0].Arrange(new Rect(finalSize));
            return finalSize;
        }

        // Get references to the image and content controls.
        var image = Children[0];
        var content = Children[1];

        var imageMargin = image.Margin;
        var contentMargin = content.Margin;

        // Use the helper method to make the layout decision, ensuring consistency with MeasureOverride.
        // It's crucial to pass the *finalSize* from the parent container to ensure consistency.
        var layout = ComputeLayoutForMeasure(
            finalSize,
            image.DesiredSize,
            imageMargin,
            contentMargin,
            doAspectCorrection: DoAspectCorrection(image));

        // Arrange the image control based on the calculated height.
        double imageWidthNoMargins = Math.Max(0, finalSize.Width - imageMargin.Left - imageMargin.Right);
        double imageArrangeHeight = Math.Max(0, layout.ImageHeightNoMargins);

        var imageRect = new Rect(
            imageMargin.Left,
            imageMargin.Top,
            imageWidthNoMargins,
            imageArrangeHeight);

        image.Arrange(imageRect);

        // Arrange the content control.
        if (layout.CollapseContent)
        {
            // Collapse content out of view (conventionally at panel bottom)
            content.Arrange(new Rect(0, finalSize.Height, 0, 0));
        }
        else
        {
            // Position depends on image's arranged bounds + image bottom margin + content top margin.
            double contentTop = image.Bounds.Bottom + imageMargin.Bottom + contentMargin.Top;

            double contentWidthNoMargins = Math.Max(0, finalSize.Width - contentMargin.Left - contentMargin.Right);

            // Clamp height to what's actually remaining in the panel after margins.
            double remaining = Math.Max(0, finalSize.Height - contentTop - contentMargin.Bottom);
            double contentHeight = Math.Min(Math.Max(0, layout.ContentHeightNoMargins), remaining);

            var contentRect = new Rect(
                contentMargin.Left,
                contentTop,
                contentWidthNoMargins,
                contentHeight);

            content.Arrange(contentRect);
        }

        return finalSize;
    }

    /// <summary>
    /// Calculates the image and content heights (excluding margins), preserving the original behavior:
    /// - The image's min-height (AbsoluteMinImageHeight and MinImageHeightRatio) is used
    ///   only for the collapse decision.
    /// - When there IS enough space for content, the image height used is Min(aspectHeight, imageDesired.Height).
    /// - When there is NOT enough space, content collapses and the image consumes all remaining height.
    /// 
    /// Returns:
    ///   CollapseContent          -> whether content should be collapsed
    ///   ImageHeightNoMargins     -> final image height to use (excluding margins)
    ///   ContentHeightNoMargins   -> final content height to use (excluding margins; 0 if collapsed)
    /// </summary>
    private (bool CollapseContent,
         double ImageHeightNoMargins,
         double ContentHeightNoMargins)
    ComputeLayoutForMeasure(
        Size availableSize,
        Size imageDesired,
        Thickness imageMargin,
        Thickness contentMargin,
        bool doAspectCorrection)
    {
        double availWidth = SanitizeNonNegative(availableSize.Width);
        double availHeight = SanitizeNonNegative(availableSize.Height);

        double imageWidthNoMargins = Math.Max(0, availWidth - imageMargin.Left - imageMargin.Right);

        double aspectHeight;
        if (doAspectCorrection)
        {
            aspectHeight = imageWidthNoMargins * 3.0 / 4.0;
        }
        else
        {
            if (imageDesired.Width > 0 && IsFinite(imageDesired.Width))
                aspectHeight = imageWidthNoMargins * imageDesired.Height / imageDesired.Width;
            else
                aspectHeight = IsFinite(imageDesired.Height) ? imageDesired.Height : 0;
        }
        if (!IsFinite(aspectHeight) || aspectHeight < 0)
            aspectHeight = 0;

        double naturalHeight = (IsFinite(imageDesired.Height) && imageDesired.Height > 0) ? imageDesired.Height : 0;

        // Min image height (absolute + ratio if container height is finite)
        double minImageHeight = AbsoluteMinImageHeight;
        if (IsFinite(availHeight))
            minImageHeight = Math.Max(minImageHeight, availHeight * MinImageHeightRatio);

        // --- Collapse decision image height
        double decisionImageHeight = Math.Max(minImageHeight, Math.Min(aspectHeight, naturalHeight));

        // --- Cap decision height by ImageMaxHeightRatio 
        double maxImageHeightByRatio =
            IsFinite(availHeight) ? Math.Max(0, availHeight * ImageMaxHeightRatio) : double.PositiveInfinity;

        decisionImageHeight = Math.Min(decisionImageHeight, maxImageHeightByRatio);

        // --- Clamp decision height to container’s usable height 
        var maxByContainer = IsFinite(availHeight)
            ? Math.Max(0, availHeight - (imageMargin.Top + imageMargin.Bottom))
            : double.PositiveInfinity;

        decisionImageHeight = Math.Min(decisionImageHeight, maxByContainer);

        double remainingForContentNoMargins =
            availHeight
            - (imageMargin.Top + decisionImageHeight + imageMargin.Bottom)
            - (contentMargin.Top + contentMargin.Bottom);

        bool collapse = remainingForContentNoMargins < CollapseThreshold;

        if (collapse)
        {
            // Image fills the panel (we intentionally do NOT cap here to avoid leaving empty space;
            // change to Math.Min(..., maxImageHeightByRatio) if you want the cap even when collapsed)
            double imageH = Math.Max(0, availHeight - (imageMargin.Top + imageMargin.Bottom));
            return (true, imageH, 0);
        }
        else
        {
            // Non-collapsed: enforce min and max ratios (to mirror Arrange)
            double imageH = Math.Max(minImageHeight, Math.Min(aspectHeight, naturalHeight));
            imageH = Math.Min(imageH, maxImageHeightByRatio); // NEW: apply cap for actual image height

            double contentH =
                Math.Max(0,
                    availHeight
                    - (imageMargin.Top + imageH + imageMargin.Bottom)
                    - (contentMargin.Top + contentMargin.Bottom));

            return (false, imageH, contentH);
        }
    }

    private static bool IsFinite(double d) => !double.IsNaN(d) && !double.IsInfinity(d);

    private static double SanitizeNonNegative(double d)
    {
        if (double.IsNaN(d) || double.IsInfinity(d)) return d; // keep Infinity if the parent provides it
        return Math.Max(0, d);
    }
}
