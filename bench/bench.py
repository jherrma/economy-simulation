# Representative inner loop of the economy-simulation tick:
# per household per tick, score ~120 candidate units, sort descending, walk applying two tests.
import time, math

HH, TICKS, CAND = 800, 100, 120

def run():
    state = 0x2545F4914F6CDD1D
    def rnd():
        nonlocal state
        state ^= (state << 13) & 0xFFFFFFFFFFFFFFFF
        state ^= state >> 7
        state ^= (state << 17) & 0xFFFFFFFFFFFFFFFF
        return (state >> 11) * (1.0 / 9007199254740992.0)
    bought = 0
    for t in range(TICKS):
        for h in range(HH):
            lam = 1.0 + 1.5 * rnd()
            budget = 2000.0 + 1000.0 * rnd()
            cands = []
            for c in range(CAND):
                joy = 20.0 + 480.0 * rnd()
                status = 200.0 * 1.3 * (rnd() - 0.5)
                sigma = 0.8 * rnd()
                value = joy + sigma * status
                if c % 5 < 2:                      # ~40% services: divisible, alpha applies
                    value *= math.pow(1.0 + c % 4, -0.6)
                price = 30.0 + 900.0 * rnd()
                dur = 1.0 + (c % 60)
                cost = price / dur + 0.0025 * price + (180.0 if c % 37 == 0 else 0.0)
                cands.append((value / cost, cost))
            cands.sort(key=lambda x: -x[0])
            for score, cost in cands:
                if score <= lam: break
                if cost > budget: continue
                budget -= cost
                bought += 1
    return bought

t0 = time.perf_counter()
n = run()
dt = time.perf_counter() - t0
print(f"python  {dt:8.3f} s   bought={n}")
