using System;

namespace Avalonia.Controls;

/// <summary>
/// Defines a stable height at which a <see cref="BottomSheet"/> can rest.
/// </summary>
public abstract class BottomSheetDetent
{
    /// <summary>A content-sized detent.</summary>
    public static BottomSheetDetent Content { get; } = new ContentBottomSheetDetent();

    /// <summary>A detent occupying half of the available height.</summary>
    public static BottomSheetDetent Medium { get; } = new RatioBottomSheetDetent("medium", 0.5);

    /// <summary>A detent occupying all of the available height.</summary>
    public static BottomSheetDetent Expanded { get; } = new RatioBottomSheetDetent("expanded", 1);

    /// <summary>
    /// Initializes a detent with a stable identifier.
    /// </summary>
    protected BottomSheetDetent(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("A detent ID cannot be empty or whitespace.", nameof(id));

        Id = id;
    }

    /// <summary>Gets the stable detent identifier.</summary>
    public string Id { get; }

    /// <summary>
    /// Resolves this detent to a height.
    /// </summary>
    /// <param name="availableHeight">The height available in the owning top level.</param>
    /// <param name="contentHeight">The naturally measured sheet height.</param>
    /// <returns>
    /// A finite height. Custom detents must return a positive value; the built-in
    /// content detent may return zero when the sheet has no measurable content.
    /// The sheet clamps the result to the available height.
    /// </returns>
    protected internal abstract double GetHeight(double availableHeight, double contentHeight);

    /// <summary>Creates a fixed-height detent.</summary>
    public static BottomSheetDetent FromHeight(string id, double height)
    {
        if (!double.IsFinite(height) || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));

        return new FixedBottomSheetDetent(id, height);
    }

    /// <summary>Creates a proportional detent.</summary>
    public static BottomSheetDetent FromRatio(string id, double ratio)
    {
        if (!double.IsFinite(ratio) || ratio <= 0 || ratio > 1)
            throw new ArgumentOutOfRangeException(nameof(ratio));

        return new RatioBottomSheetDetent(id, ratio);
    }

    private sealed class ContentBottomSheetDetent : BottomSheetDetent
    {
        public ContentBottomSheetDetent()
            : base("content")
        {
        }

        protected internal override double GetHeight(double availableHeight, double contentHeight)
            => Math.Min(availableHeight, contentHeight);
    }

    private sealed class FixedBottomSheetDetent : BottomSheetDetent
    {
        private readonly double _height;

        public FixedBottomSheetDetent(string id, double height)
            : base(id)
        {
            _height = height;
        }

        protected internal override double GetHeight(double availableHeight, double contentHeight)
            => Math.Min(availableHeight, _height);
    }

    private sealed class RatioBottomSheetDetent : BottomSheetDetent
    {
        private readonly double _ratio;

        public RatioBottomSheetDetent(string id, double ratio)
            : base(id)
        {
            _ratio = ratio;
        }

        protected internal override double GetHeight(double availableHeight, double contentHeight)
            => availableHeight * _ratio;
    }
}
