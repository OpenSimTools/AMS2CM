namespace Core.Utils;

public class PercentOfTotal : IPercent
{
    private readonly double total;
    private double done;

    public PercentOfTotal(int total)
    {
        this.total = total;
    }

    public double Percent => done / total;

    public PercentOfTotal IncrementDone()
    {
        done += 1.0;
        if (done > total)
        {
            done = total;
        }
        return this;
    }

    public PercentOfTotal DoneAll()
    {
        done = total;
        return this;
    }
}
