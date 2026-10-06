namespace Theseus.App.ViewModels;

/// <summary>A Testcontainers container no lock holder accounts for, as the locks panel lists it.</summary>
public sealed class UnownedContainerViewModel : ObservableObject
{
    private string _age = string.Empty;
    private string _status = string.Empty;
    private bool _isConfirmingRemove;

    /// <summary>Creates the entry, with Nigel's way of removing it.</summary>
    public UnownedContainerViewModel(string id, string name, string image, Action<UnownedContainerViewModel> remove)
    {
        Id = id;
        Name = name;
        Image = image;
        AskRemoveCommand = new RelayCommand(_ => IsConfirmingRemove = true);
        ConfirmRemoveCommand = new RelayCommand(_ =>
        {
            IsConfirmingRemove = false;
            remove(this);
        });
        KeepCommand = new RelayCommand(_ => IsConfirmingRemove = false);
    }

    /// <summary>Docker's id.</summary>
    public string Id { get; }

    /// <summary>The container's name.</summary>
    public string Name { get; }

    /// <summary>The image it runs.</summary>
    public string Image { get; }

    /// <summary>How long ago it was created.</summary>
    public string Age
    {
        get => _age;
        set => Set(ref _age, value);
    }

    /// <summary>Docker's status line.</summary>
    public string Status
    {
        get => _status;
        set => Set(ref _status, value);
    }

    /// <summary>Whether Remove has been pressed and is waiting for a second press.</summary>
    public bool IsConfirmingRemove
    {
        get => _isConfirmingRemove;
        set
        {
            if (Set(ref _isConfirmingRemove, value))
            {
                Raise(nameof(CanAskRemove));
            }
        }
    }

    /// <summary>Whether the first Remove button shows.</summary>
    public bool CanAskRemove => !IsConfirmingRemove;

    /// <summary>Asks before removing, because a run outside the lock cannot be told apart from a leftover.</summary>
    public RelayCommand AskRemoveCommand { get; }

    /// <summary>Removes the container.</summary>
    public RelayCommand ConfirmRemoveCommand { get; }

    /// <summary>Leaves it.</summary>
    public RelayCommand KeepCommand { get; }
}
