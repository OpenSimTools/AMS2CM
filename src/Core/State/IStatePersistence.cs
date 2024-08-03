namespace Core.State;

public interface IStatePersistence
{
    public SavedState ReadState();
    public void WriteState(SavedState state);
}
