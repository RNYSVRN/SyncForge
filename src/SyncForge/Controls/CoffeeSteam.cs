using System.Numerics;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace SyncForge.Controls;

/// <summary>
/// Aufsteigender Dampf über der Kaffeetasse (Symbol "Unterstützen"). Drei kleine Schwaden steigen versetzt auf,
/// schwanken leicht und verblassen. Läuft über die Composition-Engine (GPU), ohne XAML-Layout zu belasten.
/// Koordinaten beziehen sich auf das 16×16-Raster des Symbols; in einer Viewbox skaliert der Dampf mit.
/// Sind in Windows die Animationseffekte ausgeschaltet, steht der Dampf still.
/// </summary>
public static class CoffeeSteam
{
    /// <summary>Mittelpunkte der Schwaden über der Tassenöffnung (Tasse innen: x = 3 … 11).</summary>
    private static readonly float[] StrandX = [4.7f, 7f, 9.3f];
    private static readonly TimeSpan Duration = TimeSpan.FromSeconds(2.7);

    /// <summary>
    /// Hängt den Dampf an <paramref name="host"/> (16×16-Fläche). Die Farbe kommt von <paramref name="colorSource"/>
    /// (Vordergrund eines Symbols bzw. Füllung eines Pfads) und wird bei Design- oder Zustandswechseln nachgeführt.
    /// </summary>
    public static void Attach(UIElement host, FrameworkElement colorSource, Func<Brush?> brush)
    {
        var compositor = ElementCompositionPreview.GetElementVisual(host).Compositor;
        var color = compositor.CreateColorBrush(ResolveColor(brush()));
        var container = compositor.CreateContainerVisual();
        container.Size = new Vector2(16, 16);

        var animate = new UISettings().AnimationsEnabled;
        for (var i = 0; i < StrandX.Length; i++)
        {
            container.Children.InsertAtTop(CreateStrand(compositor, color, StrandX[i], i, animate));
        }
        ElementCompositionPreview.SetElementChildVisual(host, container);

        void Refresh() => color.Color = ResolveColor(brush());
        colorSource.ActualThemeChanged += (_, _) => Refresh();
        if (colorSource is Microsoft.UI.Xaml.Controls.IconElement icon)
            icon.RegisterPropertyChangedCallback(Microsoft.UI.Xaml.Controls.IconElement.ForegroundProperty, (_, _) => Refresh());
        if (colorSource is Microsoft.UI.Xaml.Shapes.Shape shape)
            shape.RegisterPropertyChangedCallback(Microsoft.UI.Xaml.Shapes.Shape.FillProperty, (_, _) => Refresh());
        colorSource.Loaded += (_, _) => Refresh();
    }

    private static ShapeVisual CreateStrand(Compositor compositor, CompositionColorBrush color, float x, int index, bool animate)
    {
        // Eine Schwade: schmale, abgerundete Kapsel (1,3 × 3 px).
        var geometry = compositor.CreateRoundedRectangleGeometry();
        geometry.Size = new Vector2(1.3f, 3f);
        geometry.CornerRadius = new Vector2(0.65f, 0.65f);
        var sprite = compositor.CreateSpriteShape(geometry);
        sprite.FillBrush = color;

        var visual = compositor.CreateShapeVisual();
        visual.Size = new Vector2(1.3f, 3f);
        visual.Shapes.Add(sprite);
        visual.CenterPoint = new Vector3(0.65f, 1.5f, 0);
        var left = x - 0.65f;

        if (!animate)
        {
            visual.Offset = new Vector3(left, 1.2f + (index % 2) * 0.8f, 0);
            visual.Opacity = 0.55f;
            return visual;
        }

        var delay = TimeSpan.FromSeconds(Duration.TotalSeconds / StrandX.Length * index);
        var linear = compositor.CreateLinearEasingFunction();
        var easeOut = compositor.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0.6f), new Vector2(0.4f, 1f));

        // Aufsteigen mit leichtem Schwanken (Tassenrand liegt bei y = 6,5).
        var rise = compositor.CreateVector3KeyFrameAnimation();
        rise.InsertKeyFrame(0f, new Vector3(left, 3.3f, 0), linear);
        rise.InsertKeyFrame(0.35f, new Vector3(left + 0.45f, 2.0f, 0), easeOut);
        rise.InsertKeyFrame(0.7f, new Vector3(left - 0.35f, 0.8f, 0), easeOut);
        rise.InsertKeyFrame(1f, new Vector3(left + 0.2f, 0f, 0), easeOut);

        // Erscheinen, dann nach oben verblassen.
        var fade = compositor.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(0f, 0f, linear);
        fade.InsertKeyFrame(0.2f, 0.85f, linear);
        fade.InsertKeyFrame(0.65f, 0.5f, linear);
        fade.InsertKeyFrame(1f, 0f, linear);

        // Dampf dehnt sich beim Aufsteigen etwas aus.
        var grow = compositor.CreateVector3KeyFrameAnimation();
        grow.InsertKeyFrame(0f, new Vector3(0.8f, 0.8f, 1f), linear);
        grow.InsertKeyFrame(1f, new Vector3(1.25f, 1.15f, 1f), linear);

        // Leichtes Pendeln: aus geraden Strichen werden geschwungene Schwaden.
        var sway = compositor.CreateScalarKeyFrameAnimation();
        sway.InsertKeyFrame(0f, -14f, linear);
        sway.InsertKeyFrame(0.35f, 16f, easeOut);
        sway.InsertKeyFrame(0.7f, -12f, easeOut);
        sway.InsertKeyFrame(1f, 8f, easeOut);

        foreach (var animation in new KeyFrameAnimation[] { rise, fade, grow, sway })
        {
            animation.Duration = Duration;
            animation.DelayTime = delay;
            animation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            animation.IterationBehavior = AnimationIterationBehavior.Forever;
        }
        visual.StartAnimation("Offset", rise);
        visual.StartAnimation("Opacity", fade);
        visual.StartAnimation("Scale", grow);
        visual.StartAnimation("RotationAngleInDegrees", sway);
        return visual;
    }

    private static Color ResolveColor(Brush? brush) => brush is SolidColorBrush solid ? solid.Color : Colors.Gray;
}
