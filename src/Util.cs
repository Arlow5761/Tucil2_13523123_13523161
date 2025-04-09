using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace ImageCompressor.Util;

public struct Vector2Int
{
    public int x;
    public int y;

    public Vector2Int(int x, int y)
    {
        this.x = x;
        this.y = y;
    }

    public static Vector2Int operator +(Vector2Int a, Vector2Int b) => new Vector2Int(a.x + b.x, a.y + b.y);
    public static Vector2Int operator -(Vector2Int a, Vector2Int b) => new Vector2Int(a.x - b.x, a.y - b.y);
    public static Vector2Int operator *(Vector2Int v, int s) => new Vector2Int(v.x * s, v.y * s);
    public static Vector2Int operator *(int s, Vector2Int v) => v * s;
    public static Vector2Int operator /(Vector2Int v, int s) => new Vector2Int(v.x / s, v.y / s);
    public static bool operator ==(Vector2Int a, Vector2Int b) => a.x == b.x && a.y == b.y;
    public static bool operator !=(Vector2Int a, Vector2Int b) => a.x != b.x || a.y != b.y;

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        if (obj is not Vector2Int) return false;

        Vector2Int v = (Vector2Int) obj;

        return v.x == x && v.y == y;
    }

    public override int GetHashCode()
    {
        return x.GetHashCode() ^ y.GetHashCode();
    }
}

public struct Region2Int
{
    public Vector2Int start;
    public Vector2Int end;

    public Region2Int(Vector2Int start, Vector2Int end)
    {
        this.start = start;
        this.end = end;
    }

    public Region2Int(int startX, int startY, int endX, int endY)
    {
        this.start = new Vector2Int(startX, startY);
        this.end = new Vector2Int(endX, endY);
    }

    public Vector2Int size { get => end - start + new Vector2Int(1, 1); }
    public int area { get => size.x * size.y; }
    public bool isValid { get => start.x <= end.x && start.y <= end.y; }

    public static bool operator ==(Region2Int a, Region2Int b) => a.start == b.start && a.end == b.end;
    public static bool operator !=(Region2Int a, Region2Int b) => a.start != b.start || a.end != b.end;

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        if (obj is not Region2Int) return false;

        Region2Int r = (Region2Int) obj;

        return r.start == start && r.end == end;
    }

    public override int GetHashCode()
    {
        return start.GetHashCode() ^ end.GetHashCode();
    }
}

public struct ImageRegion
{
    public Region2Int region;
    public double error;

    public ImageRegion(Region2Int region, double error = 0d)
    {
        this.region = region;
        this.error = error;
    }
}

public class QuadCache<CachedType>
{
    public QuadCache(int width, int height)
    {
        cache = new CacheUnit[width, height];
    }
    
    public bool TryGetCache(Region2Int region, out CachedType?[] fetchedCache)
    {
        fetchedCache = new CachedType?[4];

        Region2Int region1 = new(region.start, region.start + region.size / 2 - new Vector2Int(1, 1));
        Region2Int region2 = new(region.start + new Vector2Int(region.size.x / 2, 0), region.start + new Vector2Int(region.size.x - 1, region.size.y / 2 - 1));
        Region2Int region3 = new(region.start + new Vector2Int(0, region.size.y / 2), region.start + new Vector2Int(region.size.x / 2 - 1, region.size.y - 1));
        Region2Int region4 = new(region.start + region.size / 2, region.end);

        if (cache[region1.start.x, region1.start.y].end != region1.end
        || cache[region2.start.x, region2.start.y].end != region2.end
        || cache[region3.start.x, region3.start.y].end != region3.end
        || cache[region4.start.x, region4.start.y].end != region4.end)
        return false;

        fetchedCache[0] = cache[region1.start.x, region1.start.y].info;
        fetchedCache[1] = cache[region2.start.x, region2.start.y].info;
        fetchedCache[2] = cache[region3.start.x, region3.start.y].info;
        fetchedCache[3] = cache[region4.start.x, region4.start.y].info;

        return true;
    }

    public void SetCache(Region2Int region, CachedType? cached)
    {
        cache[region.start.x, region.start.y].end = region.end;
        cache[region.start.x, region.start.y].info = cached;
    }

    private CacheUnit[,] cache;

    private struct CacheUnit
    {
        public Vector2Int end;
        public CachedType? info;
    }
}