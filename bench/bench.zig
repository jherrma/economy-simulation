const std = @import("std");

const HH = 800;
const TICKS = 100;
const CAND = 120;

const Cand = struct { score: f64, cost: f64 };

var state: u64 = 0x2545F4914F6CDD1D;
fn rnd() f64 {
    state ^= state << 13;
    state ^= state >> 7;
    state ^= state << 17;
    return @as(f64, @floatFromInt(state >> 11)) * (1.0 / 9007199254740992.0);
}

fn gt(_: void, a: Cand, b: Cand) bool {
    return a.score > b.score;
}

pub fn main() void {
    var bought: i64 = 0;
    var cands: [CAND]Cand = undefined;
    var t: usize = 0;
    while (t < TICKS) : (t += 1) {
        var h: usize = 0;
        while (h < HH) : (h += 1) {
            const lam = 1.0 + 1.5 * rnd();
            var budget = 2000.0 + 1000.0 * rnd();
            var c: usize = 0;
            while (c < CAND) : (c += 1) {
                const joy = 20.0 + 480.0 * rnd();
                const status = 200.0 * 1.3 * (rnd() - 0.5);
                const sigma = 0.8 * rnd();
                var value = joy + sigma * status;
                if (c % 5 < 2) value *= std.math.pow(f64, 1.0 + @as(f64, @floatFromInt(c % 4)), -0.6);
                const price = 30.0 + 900.0 * rnd();
                const dur = 1.0 + @as(f64, @floatFromInt(c % 60));
                var cost = price / dur + 0.0025 * price;
                if (c % 37 == 0) cost += 180.0;
                cands[c] = .{ .score = value / cost, .cost = cost };
            }
            std.mem.sort(Cand, &cands, {}, gt);
            for (cands) |cd| {
                if (cd.score <= lam) break;
                if (cd.cost > budget) continue;
                budget -= cd.cost;
                bought += 1;
            }
        }
    }
    std.debug.print("zig     bought={d}\n", .{bought});
}
