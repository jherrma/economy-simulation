using System.Diagnostics;

const int HH = 800, TICKS = 100, CAND = 120;

ulong state = 0x2545F4914F6CDD1DUL;
double Rnd()
{
    state ^= state << 13;
    state ^= state >> 7;
    state ^= state << 17;
    return (state >> 11) * (1.0 / 9007199254740992.0);
}

var sw = Stopwatch.StartNew();
long bought = 0;
var cands = new Cand[CAND];
Comparison<Cand> byScoreDesc = static (a, b) => b.Score.CompareTo(a.Score);

for (int t = 0; t < TICKS; t++)
{
    for (int h = 0; h < HH; h++)
    {
        double lam = 1.0 + 1.5 * Rnd();
        double budget = 2000.0 + 1000.0 * Rnd();

        for (int c = 0; c < CAND; c++)
        {
            double joy = 20.0 + 480.0 * Rnd();
            double status = 200.0 * 1.3 * (Rnd() - 0.5);
            double sigma = 0.8 * Rnd();
            double value = joy + sigma * status;
            if (c % 5 < 2) value *= Math.Pow(1.0 + (c % 4), -0.6);
            double price = 30.0 + 900.0 * Rnd();
            double dur = 1.0 + (c % 60);
            double cost = price / dur + 0.0025 * price + (c % 37 == 0 ? 180.0 : 0.0);
            cands[c] = new Cand(value / cost, cost);
        }

        Array.Sort(cands, byScoreDesc);

        for (int c = 0; c < CAND; c++)
        {
            var cd = cands[c];
            if (cd.Score <= lam) break;
            if (cd.Cost > budget) continue;
            budget -= cd.Cost;
            bought++;
        }
    }
}
sw.Stop();
Console.WriteLine($"csharp  {sw.Elapsed.TotalSeconds,8:F3} s   bought={bought}");

readonly record struct Cand(double Score, double Cost);
