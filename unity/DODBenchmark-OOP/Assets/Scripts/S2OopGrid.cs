/// <summary>
/// S2 carpisma adimi — OOP: grid nesne REFERANSLARI tutar; pozisyon/hiz her
/// entity'nin kendi nesnesinden okunur/yazilir.
/// Algoritma tools/s2_reference/s2_reference.c ile birebir ayni (islem sirasi dahil):
///   broad-phase: uniform grid (hucre = 2r), counting sort (sayim -> prefix sum
///   -> referans dizisi), dinamik liste yok;
///   narrow-phase: kendi hucresi + 8 komsu (satir satir), hucre icinde indeks
///   sirasiyla, j > i, d2 &lt; (2r)^2;
///   tepki: yaklasan ciftte esit kutleli elastik carpisma (Gauss-Seidel).
/// </summary>
public class S2OopGrid
{
    private readonly int cols;
    private readonly int rows;
    private readonly int ncells;
    private readonly int[] cellStart;   // hucre sayisi + 1 (prefix sum)
    private readonly int[] cellFill;
    private S2OopEntity[] sorted = new S2OopEntity[0];

    public int LastResponses { get; private set; }

    public S2OopGrid()
    {
        cols = (int)System.MathF.Ceiling((S2Params.MaxX - S2Params.MinX) / S2Params.Cell);
        rows = (int)System.MathF.Ceiling((S2Params.MaxY - S2Params.MinY) / S2Params.Cell);
        ncells = cols * rows;
        cellStart = new int[ncells + 1];
        cellFill = new int[ncells];
    }

    /// <summary>entities: spawn sirasinda (entities[i].Index == i). Kesisen cift sayisini dondurur.</summary>
    public int Collide(S2OopEntity[] entities)
    {
        int n = entities.Length;
        if (sorted.Length < n) sorted = new S2OopEntity[n];

        // Broad-phase: counting sort
        System.Array.Clear(cellStart, 0, cellStart.Length);
        for (int i = 0; i < n; i++)
        {
            S2OopEntity e = entities[i];
            int cx = (int)((e.Pos.x - S2Params.MinX) / S2Params.Cell);
            int cy = (int)((e.Pos.y - S2Params.MinY) / S2Params.Cell);
            cx = cx < 0 ? 0 : (cx >= cols ? cols - 1 : cx);
            cy = cy < 0 ? 0 : (cy >= rows ? rows - 1 : cy);
            e.Cell = cy * cols + cx;
            cellStart[e.Cell + 1]++;
        }
        for (int c = 0; c < ncells; c++) cellStart[c + 1] += cellStart[c];
        System.Array.Copy(cellStart, cellFill, ncells);
        for (int i = 0; i < n; i++)
        {
            S2OopEntity e = entities[i];
            sorted[cellFill[e.Cell]++] = e;
        }

        // Narrow-phase + tepki
        int pairs = 0;
        int responses = 0;
        for (int i = 0; i < n; i++)
        {
            S2OopEntity a = entities[i];
            int cx = a.Cell % cols;
            int cy = a.Cell / cols;
            for (int ny = cy - 1; ny <= cy + 1; ny++)
            {
                if (ny < 0 || ny >= rows) continue;
                for (int nx = cx - 1; nx <= cx + 1; nx++)
                {
                    if (nx < 0 || nx >= cols) continue;
                    int c = ny * cols + nx;
                    int end = cellStart[c + 1];
                    for (int k = cellStart[c]; k < end; k++)
                    {
                        S2OopEntity b = sorted[k];
                        if (b.Index <= i) continue;
                        float dx = b.Pos.x - a.Pos.x;
                        float dy = b.Pos.y - a.Pos.y;
                        float d2 = dx * dx + dy * dy;
                        if (d2 >= S2Params.D2Max) continue;
                        pairs++;
                        float rvx = b.Vel.x - a.Vel.x;
                        float rvy = b.Vel.y - a.Vel.y;
                        float dot = rvx * dx + rvy * dy;
                        if (dot < 0f && d2 > 0f)
                        {
                            // esit kutle, elastik: normal bilesenler degis tokus
                            float f = dot / d2;
                            a.Vel.x += f * dx;
                            a.Vel.y += f * dy;
                            b.Vel.x -= f * dx;
                            b.Vel.y -= f * dy;
                            responses++;
                        }
                    }
                }
            }
        }
        LastResponses = responses;
        return pairs;
    }

    /// <summary>Dogrulama: O(N^2) kesisen cift sayisi (yalnizca sayar).</summary>
    public static int CountBruteforce(S2OopEntity[] entities)
    {
        int pairs = 0;
        for (int i = 0; i < entities.Length; i++)
            for (int j = i + 1; j < entities.Length; j++)
            {
                float dx = entities[j].Pos.x - entities[i].Pos.x;
                float dy = entities[j].Pos.y - entities[i].Pos.y;
                if (dx * dx + dy * dy < S2Params.D2Max) pairs++;
            }
        return pairs;
    }
}
