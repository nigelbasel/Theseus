namespace Theseus.App.ViewModels;

/// <summary>A ruling on a resource, made by the arbiter or by Nigel, with Nigel's overturn.</summary>
public sealed class LockRulingViewModel
{
    public LockRulingViewModel(string name, string text, Action<string> overturn)
    {
        Name = name;
        Text = text;
        OverturnCommand = new RelayCommand(_ => overturn(Name));
    }

    public string Name { get; }

    public string Text { get; }

    public RelayCommand OverturnCommand { get; }
}
