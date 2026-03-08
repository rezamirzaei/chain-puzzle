namespace ChainPuzzle.Desktop;

internal interface ISettingsStore
{
    SettingsDocument Load();

    void Save(SettingsDocument document);
}
