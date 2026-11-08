namespace ResinGrove.Core;

/// <summary>Calendar-based production, capped without banking time while full or inactive.</summary>
public sealed class ResinProduction
{
    public double Amount { get; private set; }
    public double LastHours { get; private set; }

    public ResinProduction(double amount, double lastHours)
    {
        Amount = double.IsFinite(amount) ? Math.Max(0, amount) : 0;
        LastHours = double.IsFinite(lastHours) ? lastHours : 0;
    }

    public void Advance(double nowHours, double hoursPerResin, int capacity, bool active)
    {
        if (!double.IsFinite(nowHours)) return;
        double elapsed = Math.Max(0, nowHours - LastHours);
        LastHours = nowHours;
        if (active && double.IsFinite(hoursPerResin) && hoursPerResin > 0)
            Amount += elapsed / hoursPerResin;
        Amount = Math.Clamp(Amount, 0, capacity);
    }

    public int Collect()
    {
        int count = (int)Math.Floor(Amount + 1e-9);
        Amount = Math.Max(0, Amount - count);
        return count;
    }
}
