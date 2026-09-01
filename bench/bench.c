#include <stdio.h>
#include <stdlib.h>
#include <math.h>
#include <time.h>
#define HH 800
#define TICKS 100
#define CAND 120
typedef struct { double score, cost; } cand;
static unsigned long long state = 0x2545F4914F6CDD1DULL;
static inline double rnd(void) {
    state ^= state << 13; state ^= state >> 7; state ^= state << 17;
    return (double)(state >> 11) * (1.0 / 9007199254740992.0);
}
static int cmp(const void *a, const void *b) {
    double x = ((const cand*)a)->score, y = ((const cand*)b)->score;
    return (x < y) - (x > y);
}
int main(void) {
    struct timespec t0, t1; clock_gettime(CLOCK_MONOTONIC, &t0);
    long long bought = 0; cand cands[CAND];
    for (int t = 0; t < TICKS; t++) for (int h = 0; h < HH; h++) {
        double lam = 1.0 + 1.5 * rnd();
        double budget = 2000.0 + 1000.0 * rnd();
        for (int c = 0; c < CAND; c++) {
            double joy = 20.0 + 480.0 * rnd();
            double status = 200.0 * 1.3 * (rnd() - 0.5);
            double sigma = 0.8 * rnd();
            double value = joy + sigma * status;
            if (c % 5 < 2) value *= pow(1.0 + (c % 4), -0.6);
            double price = 30.0 + 900.0 * rnd();
            double dur = 1.0 + (c % 60);
            double cost = price / dur + 0.0025 * price + ((c % 37 == 0) ? 180.0 : 0.0);
            cands[c].score = value / cost; cands[c].cost = cost;
        }
        qsort(cands, CAND, sizeof(cand), cmp);
        for (int c = 0; c < CAND; c++) {
            if (cands[c].score <= lam) break;
            if (cands[c].cost > budget) continue;
            budget -= cands[c].cost; bought++;
        }
    }
    clock_gettime(CLOCK_MONOTONIC, &t1);
    printf("c       %8.3f s   bought=%lld\n",
        (t1.tv_sec - t0.tv_sec) + (t1.tv_nsec - t0.tv_nsec) / 1e9, bought);
    return 0;
}
