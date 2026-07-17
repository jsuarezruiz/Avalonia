using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Media;
using Avalonia.Styling;

namespace ControlCatalog.Pages
{
    /// <summary>
    /// A reusable dialog-oriented transition that combines fade, scale, vertical movement,
    /// and a small overshoot while preserving the visual's authored transform and opacity.
    /// </summary>
    internal sealed class ScaleLiftPageTransition : IPageTransition
    {
        public TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(460);

        public TimeSpan ExitDuration { get; set; } = TimeSpan.FromMilliseconds(220);

        public double EntranceScale { get; set; } = 0.9;

        public double EntranceOffset { get; set; } = 32;

        public double OvershootScale { get; set; } = 1.015;

        public double ExitScale { get; set; } = 0.96;

        public double ExitOffset { get; set; } = 18;

        public Easing EntranceEasing { get; set; } = new CubicEaseOut();

        public Easing ExitEasing { get; set; } = new CubicEaseIn();

        public async Task Start(
            Visual? from,
            Visual? to,
            bool forward,
            CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested || ReferenceEquals(from, to))
                return;

            var tasks = new List<Task>(2);
            VisualState? fromState = from is null ? null : Capture(from);
            VisualState? toState = to is null ? null : Capture(to);
            var completed = false;

            try
            {
                if (from is not null && fromState is { } outgoingState)
                {
                    Prepare(from);
                    tasks.Add(CreateExitAnimation(outgoingState.Opacity, forward)
                        .RunAsync(from, cancellationToken));
                }

                if (to is not null && toState is { } incomingState)
                {
                    Prepare(to);
                    to.Opacity = 0;
                    to.IsVisible = true;
                    tasks.Add(CreateEntranceAnimation(incomingState.Opacity, forward)
                        .RunAsync(to, cancellationToken));
                }

                await Task.WhenAll(tasks);
                completed = true;
            }
            finally
            {
                if (to is not null && toState is { } incomingState)
                {
                    Restore(to, incomingState);
                    if (completed)
                        to.IsVisible = true;
                }

                if (from is not null && fromState is { } outgoingState)
                {
                    Restore(from, outgoingState);
                    if (completed)
                        from.IsVisible = false;
                }
            }
        }

        private Animation CreateEntranceAnimation(double opacity, bool forward)
        {
            var offset = forward ? EntranceOffset : -EntranceOffset;
            return new Animation
            {
                Duration = Duration,
                Easing = EntranceEasing,
                FillMode = FillMode.Forward,
                Children =
                {
                    Frame(0, 0, EntranceScale, offset),
                    Frame(0.76, opacity, OvershootScale, forward ? -2 : 2),
                    Frame(1, opacity, 1, 0)
                }
            };
        }

        private Animation CreateExitAnimation(double opacity, bool forward)
        {
            var offset = forward ? -ExitOffset : ExitOffset;
            return new Animation
            {
                Duration = ExitDuration,
                Easing = ExitEasing,
                FillMode = FillMode.Forward,
                Children =
                {
                    Frame(0, opacity, 1, 0),
                    Frame(1, 0, ExitScale, offset)
                }
            };
        }

        private static KeyFrame Frame(double cue, double opacity, double scale, double offset) =>
            new()
            {
                Cue = new Cue(cue),
                Setters =
                {
                    new Setter(Visual.OpacityProperty, opacity),
                    new Setter(ScaleTransform.ScaleXProperty, scale),
                    new Setter(ScaleTransform.ScaleYProperty, scale),
                    new Setter(TranslateTransform.YProperty, offset)
                }
            };

        private static VisualState Capture(Visual visual) =>
            new(
                visual.RenderTransform,
                visual.RenderTransformOrigin,
                visual.Opacity,
                visual.IsVisible);

        private static void Prepare(Visual visual)
        {
            visual.RenderTransformOrigin = RelativePoint.Center;
            visual.RenderTransform = new TransformGroup
            {
                Children =
                {
                    new ScaleTransform(),
                    new TranslateTransform()
                }
            };
        }

        private static void Restore(Visual visual, VisualState state)
        {
            visual.RenderTransform = state.RenderTransform;
            visual.RenderTransformOrigin = state.RenderTransformOrigin;
            visual.Opacity = state.Opacity;
            visual.IsVisible = state.IsVisible;
        }

        private readonly record struct VisualState(
            ITransform? RenderTransform,
            RelativePoint RenderTransformOrigin,
            double Opacity,
            bool IsVisible);
    }
}
