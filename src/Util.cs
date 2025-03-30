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
}