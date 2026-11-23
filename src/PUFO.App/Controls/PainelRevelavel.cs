using System.Windows;
using System.Windows.Controls;
using Size = System.Windows.Size;

namespace PUFO.App.Controls;

/// <summary>Reveals measured content without disconnecting its visual tree during a collapse.</summary>
public sealed class PainelRevelavel : Decorator
{
    public static readonly DependencyProperty ProgressoProperty = DependencyProperty.Register(
        nameof(Progresso), typeof(double), typeof(PainelRevelavel),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public double Progresso
    {
        get => (double)GetValue(ProgressoProperty);
        set => SetValue(ProgressoProperty, value);
    }

    protected override Size MeasureOverride(Size constraint)
    {
        if (Child is null || Progresso <= 0) return default;
        Child.Measure(constraint);
        return new Size(Child.DesiredSize.Width, Child.DesiredSize.Height * Progresso);
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        if (Progresso > 0 && Child is not null)
            Child.Arrange(new Rect(0, 0, arrangeSize.Width, Child.DesiredSize.Height));
        return arrangeSize;
    }
}
