using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace PUFO.App.Controls;

public sealed class SliderSincronizado : Slider
{
    public SliderSincronizado()
    {
        AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler((_, _) => EmArrasto = true), true);
        AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler((_, _) => EmArrasto = false), true);
    }

    internal bool EmArrasto { get; private set; }
    internal bool Atualizando { get; private set; }
    internal void CancelarArrasto()
    {
        if (GetTemplateChild("PART_Track") is Track { Thumb: { IsDragging: true } thumb })
            thumb.CancelDrag();
    }

    internal bool Sincronizar(int valor)
    {
        if (EmArrasto || IsMouseCaptureWithin) return false;
        if (Value == valor) return true;
        Atualizando = true;
        try { Value = valor; }
        finally { Atualizando = false; }
        return true;
    }
}
