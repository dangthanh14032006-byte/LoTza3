using UnityEngine;

/// <summary>
/// Lưới 8x8 của Block Blast. Tự vẽ lưới bằng SpriteRenderer, không cần prefab.
/// Quy ước: row 0 nằm dưới cùng, col 0 nằm bên trái.
/// </summary>
public class GridManager : MonoBehaviour
{
    public const int Size = 8;

    [Header("Giao diện")]
    public float cellSize = 1f;
    public float gap = 0.08f;
    public Color emptyColor = new Color(0.16f, 0.20f, 0.33f);

    bool[,] filled = new bool[Size, Size];
    SpriteRenderer[,] cells = new SpriteRenderer[Size, Size];
    static Sprite squareSprite;

    void Awake()
    {
        BuildGrid();
    }

    // Tạo sprite hình vuông trắng 1x1 đơn vị (không cần import ảnh)
    public static Sprite GetSquareSprite()
    {
        if (squareSprite == null)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            squareSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
        return squareSprite;
    }

    void BuildGrid()
    {
        for (int r = 0; r < Size; r++)
        {
            for (int c = 0; c < Size; c++)
            {
                var go = new GameObject($"Cell_{r}_{c}");
                go.transform.SetParent(transform, false);
                go.transform.position = CellToWorld(r, c);
                go.transform.localScale = Vector3.one * (cellSize - gap);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = GetSquareSprite();
                sr.color = emptyColor;
                cells[r, c] = sr;
            }
        }
    }

    // Tâm của ô (r, c) trong tọa độ thế giới
    public Vector3 CellToWorld(int r, int c)
    {
        float offset = (Size - 1) * cellSize * 0.5f;
        return transform.position + new Vector3(c * cellSize - offset, r * cellSize - offset, 0f);
    }

    // Đổi vị trí thế giới thành ô gần nhất. Trả về false nếu nằm ngoài lưới.
    public bool WorldToCell(Vector3 world, out int r, out int c)
    {
        float offset = (Size - 1) * cellSize * 0.5f;
        Vector3 local = world - transform.position;
        c = Mathf.RoundToInt((local.x + offset) / cellSize);
        r = Mathf.RoundToInt((local.y + offset) / cellSize);
        return r >= 0 && r < Size && c >= 0 && c < Size;
    }

    // Kiểm tra có đặt được khối (shape) với góc dưới-trái tại (row, col) không
    public bool CanPlace(bool[,] shape, int row, int col)
    {
        for (int r = 0; r < shape.GetLength(0); r++)
        {
            for (int c = 0; c < shape.GetLength(1); c++)
            {
                if (!shape[r, c]) continue;
                int nr = row + r, nc = col + c;
                if (nr < 0 || nr >= Size || nc < 0 || nc >= Size) return false;
                if (filled[nr, nc]) return false;
            }
        }
        return true;
    }

    // Kiểm tra còn chỗ nào trên lưới đặt được khối này không (dùng cho Game Over)
    public bool CanPlaceAnywhere(bool[,] shape)
    {
        for (int r = 0; r < Size; r++)
            for (int c = 0; c < Size; c++)
                if (CanPlace(shape, r, c)) return true;
        return false;
    }

    // Đặt khối lên lưới, trả về số ô đã đặt (để tính điểm)
    public int Place(bool[,] shape, int row, int col, Color color)
    {
        int count = 0;
        for (int r = 0; r < shape.GetLength(0); r++)
        {
            for (int c = 0; c < shape.GetLength(1); c++)
            {
                if (!shape[r, c]) continue;
                filled[row + r, col + c] = true;
                cells[row + r, col + c].color = color;
                count++;
            }
        }
        return count;
    }

    // Xóa các hàng/cột đã đầy, trả về số hàng + cột đã xóa (để tính combo)
    public int ClearFullLines()
    {
        bool[] fullRow = new bool[Size];
        bool[] fullCol = new bool[Size];
        int lines = 0;

        for (int i = 0; i < Size; i++)
        {
            fullRow[i] = true;
            fullCol[i] = true;
            for (int j = 0; j < Size; j++)
            {
                if (!filled[i, j]) fullRow[i] = false;
                if (!filled[j, i]) fullCol[i] = false;
            }
            if (fullRow[i]) lines++;
            if (fullCol[i]) lines++;
        }

        // Xóa sau khi đã xác định hết, để hàng và cột giao nhau cùng bị xóa
        for (int r = 0; r < Size; r++)
        {
            for (int c = 0; c < Size; c++)
            {
                if (fullRow[r] || fullCol[c])
                {
                    Burst(r, c, cells[r, c].color);   // hiệu ứng nổ
                    filled[r, c] = false;
                    cells[r, c].color = emptyColor;
                }
            }
        }

        if (lines > 0) CameraShake.Shake(0.04f + 0.05f * lines, 0.2f);
        return lines;
    }

    // Hiệu ứng nổ tại ô (r, c): một vệt sáng phóng to + các mảnh vỡ bay ra
    void Burst(int r, int c, Color col)
    {
        Vector3 pos = CellToWorld(r, c);

        BlockParticle.Spawn(pos, Color.Lerp(col, Color.white, 0.5f),
            Vector2.zero, cellSize - gap, 0.3f, 0f, true);

        for (int i = 0; i < 6; i++)
        {
            float ang = Random.Range(0f, Mathf.PI * 2f);
            float spd = Random.Range(2.5f, 6f);
            Vector2 vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * spd + Vector2.up * 2f;
            BlockParticle.Spawn(pos, Color.Lerp(col, Color.white, 0.35f), vel,
                Random.Range(0.12f, 0.28f), Random.Range(0.45f, 0.8f),
                Random.Range(-360f, 360f), false);
        }
    }
}

// ============================================================
// Một mảnh vỡ / vệt sáng. Tự bay, nhỏ dần, mờ dần rồi tự xóa.
// ============================================================
public class BlockParticle : MonoBehaviour
{
    Vector2 velocity;
    float life, spin, size, t;
    bool pop;
    Color color;
    SpriteRenderer sr;

    public static void Spawn(Vector3 pos, Color color, Vector2 velocity,
                             float size, float life, float spin, bool pop)
    {
        var go = new GameObject("Particle");
        go.transform.position = pos;
        var p = go.AddComponent<BlockParticle>();
        p.velocity = velocity;
        p.size = size;
        p.life = life;
        p.spin = spin;
        p.pop = pop;
        p.color = color;
        p.sr = go.AddComponent<SpriteRenderer>();
        p.sr.sprite = GridManager.GetSquareSprite();
        p.sr.color = color;
        p.sr.sortingOrder = 8;
        go.transform.localScale = Vector3.one * size;
    }

    void Update()
    {
        t += Time.deltaTime;
        float k = t / life;
        if (k >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        if (pop)
        {
            transform.localScale = Vector3.one * size * (1f + 0.6f * k);
        }
        else
        {
            velocity.y -= 14f * Time.deltaTime;   // trọng lực
            transform.position += (Vector3)(velocity * Time.deltaTime);
            transform.Rotate(0f, 0f, spin * Time.deltaTime);
            transform.localScale = Vector3.one * size * (1f - 0.6f * k);
        }

        sr.color = new Color(color.r, color.g, color.b, 1f - k);
    }
}

// ============================================================
// Rung camera nhẹ khi xóa hàng. Tự gắn vào Main Camera khi cần.
// ============================================================
public class CameraShake : MonoBehaviour
{
    Vector3 basePos;
    float duration, timer, strength;

    void Awake()
    {
        basePos = transform.position;
    }

    public static void Shake(float strength, float duration)
    {
        var cam = Camera.main;
        if (cam == null) return;
        var s = cam.GetComponent<CameraShake>();
        if (s == null) s = cam.gameObject.AddComponent<CameraShake>();
        s.strength = strength;
        s.duration = duration;
        s.timer = duration;
    }

    void LateUpdate()
    {
        if (timer > 0f)
        {
            timer -= Time.deltaTime;
            float k = Mathf.Max(0f, timer / duration);
            transform.position = basePos + (Vector3)(Random.insideUnitCircle * strength * k);
        }
        else
        {
            transform.position = basePos;
        }
    }
}