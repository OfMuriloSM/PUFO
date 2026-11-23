using System.Windows;
using System.Windows.Controls;
using Size = System.Windows.Size;

namespace PUFO.App.Controls;

/// <summary>Measures content independently from the animated shell's current bounds.</summary>
public sealed class MedidorConteudo : Decorator
{
    protected override Size MeasureOverride(Size constraint)
    {
        if (Child is null) return default;
        Child.Measure(new Size(Math.Max(16, SystemParameters.PrimaryScreenWidth - 50),
            Math.Max(16, SystemParameters.WorkArea.Height - 42)));
        return Child.DesiredSize;
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        if (Child is not null) Child.Arrange(new Rect((arrangeSize.Width - Child.DesiredSize.Width) / 2, 0, Child.DesiredSize.Width, Child.DesiredSize.Height));
        return arrangeSize;
    }
}
