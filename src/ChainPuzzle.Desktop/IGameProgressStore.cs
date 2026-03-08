namespace ChainPuzzle.Desktop;

internal interface IGameProgressStore
{
    GameProgressDocument Load();

    void Save(GameProgressDocument document);
}
