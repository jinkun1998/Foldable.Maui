namespace Foldable.Maui.Models;

public class FoldInfoChangedEventArgs : EventArgs
{
    public FoldInfo Info { get; }

    public FoldInfoChangedEventArgs(FoldInfo info)
    {
        Info = info;
    }
}
