namespace AdivinaQue.PackTool.Analysis;

public static class Entropy
{
    public static double Compute(double p)
    {
        if (p <= 0 || p >= 1)
        {
            return 0;
        }

        return -(p * Math.Log2(p)) - ((1 - p) * Math.Log2(1 - p));
    }
}
