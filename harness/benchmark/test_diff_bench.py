import os, unittest
from diff_bench import summarize

HERE = os.path.dirname(__file__)

class TestDiffBench(unittest.TestCase):
    def test_summarize_mean_and_gc(self):
        s = summarize(os.path.join(HERE, "fixtures", "a.log"))
        self.assertEqual(s["frames"], 4)
        self.assertEqual(s["mean_ms"], 20.0)
        self.assertEqual(s["gc_growth_mb"], 3.0)   # 103 - 100
        self.assertEqual(s["gen0"], 1)             # 11 - 10

    def test_b_is_faster(self):
        a = summarize(os.path.join(HERE, "fixtures", "a.log"))
        b = summarize(os.path.join(HERE, "fixtures", "b.log"))
        self.assertLess(b["mean_ms"], a["mean_ms"])
        self.assertLess(b["gc_growth_mb"], a["gc_growth_mb"])

    def test_empty_raises(self):
        with self.assertRaises(ValueError):
            summarize(os.path.join(HERE, "fixtures", "empty.log"))

    def test_p95_is_not_just_max(self):
        s = summarize(os.path.join(HERE, "fixtures", "p95.log"))
        self.assertEqual(s["frames"], 21)
        self.assertEqual(s["p95_ms"], 50.0)   # int(0.95*21)=19 -> 20th sorted value = 50, not the 99 max

    def test_skip_drops_warmup(self):
        full = summarize(os.path.join(HERE, "fixtures", "warmup.log"))
        trimmed = summarize(os.path.join(HERE, "fixtures", "warmup.log"), skip=1)
        self.assertEqual(full["frames"], 4)        # 9999 load frame + 3 steady
        self.assertEqual(trimmed["frames"], 3)     # load frame dropped
        self.assertEqual(trimmed["mean_ms"], 16.0) # steady state, not swamped by 9999
        self.assertLess(trimmed["mean_ms"], full["mean_ms"])

    def test_skip_all_raises(self):
        with self.assertRaises(ValueError):
            summarize(os.path.join(HERE, "fixtures", "warmup.log"), skip=99)

if __name__ == "__main__":
    unittest.main()
