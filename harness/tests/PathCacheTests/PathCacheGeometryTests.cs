using Xunit;
using PeterHan.FastTrack.PathPatches;

public class PathCacheGeometryTests {
    // Window rooted at (0,0), 10x10: half-open [0,10) x [0,10), matching the convention
    // PathCacher.CheckCache's center calc and the old InvalidateRegion AABB overlap test
    // both use.
    [Theory]
    [InlineData(5, 5, true)]    // inside
    [InlineData(0, 0, true)]    // top-left corner (inclusive)
    [InlineData(9, 9, true)]    // bottom-right inside (root+size-1)
    [InlineData(10, 5, false)]  // x == root+width (exclusive)
    [InlineData(5, 10, false)]  // y == root+height (exclusive)
    [InlineData(-1, 5, false)]  // left of window
    public void CellInWindow_window0_0_10x10(int cx, int cy, bool expected) {
        Assert.Equal(expected, PathCacheGeometry.CellInWindow(cx, cy, 0, 0, 10, 10));
    }

    // A window with a non-zero root (the typical case for a bounded probe grid centered
    // away from the map origin) to make sure the offset, not just the size, is honored.
    [Theory]
    [InlineData(100, 100, true)]   // top-left corner of the window
    [InlineData(104, 104, true)]   // interior
    [InlineData(107, 104, true)]   // root+size-1 on x (inclusive bottom-right edge)
    [InlineData(108, 104, false)]  // root+size on x (exclusive)
    [InlineData(99, 104, false)]   // one cell left of root
    [InlineData(104, 99, false)]   // one cell above root
    public void CellInWindow_offsetRoot_100_100_8x8(int cx, int cy, bool expected) {
        Assert.Equal(expected, PathCacheGeometry.CellInWindow(cx, cy, 100, 100, 8, 8));
    }
}
