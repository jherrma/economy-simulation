package main

import ("cmp"; "fmt"; "math"; "slices"; "time")

const HH, TICKS, CAND = 800, 100, 120

type cand struct{ score, cost float64 }

func main() {
	state := uint64(0x2545F4914F6CDD1D)
	rnd := func() float64 {
		state ^= state << 13; state ^= state >> 7; state ^= state << 17
		return float64(state>>11) * (1.0 / 9007199254740992.0)
	}
	start := time.Now()
	bought := 0
	cands := make([]cand, CAND)
	for t := 0; t < TICKS; t++ {
		for h := 0; h < HH; h++ {
			lam := 1.0 + 1.5*rnd()
			budget := 2000.0 + 1000.0*rnd()
			for c := 0; c < CAND; c++ {
				joy := 20.0 + 480.0*rnd()
				status := 200.0 * 1.3 * (rnd() - 0.5)
				sigma := 0.8 * rnd()
				value := joy + sigma*status
				if c%5 < 2 { value *= math.Pow(1.0+float64(c%4), -0.6) }
				price := 30.0 + 900.0*rnd()
				dur := 1.0 + float64(c%60)
				cost := price/dur + 0.0025*price
				if c%37 == 0 { cost += 180.0 }
				cands[c] = cand{value / cost, cost}
			}
			slices.SortFunc(cands, func(a, b cand) int { return cmp.Compare(b.score, a.score) })
			for _, cd := range cands {
				if cd.score <= lam { break }
				if cd.cost > budget { continue }
				budget -= cd.cost
				bought++
			}
		}
	}
	fmt.Printf("go2     %8.3f s   bought=%d\n", time.Since(start).Seconds(), bought)
}
