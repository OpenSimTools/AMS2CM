namespace Core.State;

public interface IStatePersistence
{
    public InternalState ReadState();
    public void WriteState(InternalState state);
}
