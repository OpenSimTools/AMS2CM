namespace Core.State;

internal interface IStatePersistence
{
    public InternalState ReadState();
    public void WriteState(InternalState state);
}
