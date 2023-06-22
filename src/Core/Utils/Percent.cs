namespace Core.Utils;

public class Percent
{
    private readonly double total;
    private double done;

    public static Percent OfTotal(int total) => new(total);

    private Percent(double total)
    {
        this.total = total;
    }

    public double Increment()
    {
        done += 1.0;
        return done / total;
    }
}
