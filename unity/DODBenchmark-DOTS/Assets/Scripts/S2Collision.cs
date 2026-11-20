using Unity.Collections;

/// <summary>
/// S2 carpisma adimi — DOD: grid entity INDEKSI tutar; pozisyon/hiz SoA
/// dizilerinde (px, py, vx, vy). Burst ile derlenen S2CollisionSystem'den
/// cagrilir (tek is parcacigi, Job System yok).
/// Algoritma tools/s2_reference/s2_reference.c ile birebir ayni (islem sirasi dahil).
/// </summary>
public static class S2Collision
{
    /// <summary>Kesisen cift sayisini dondurur; tepki verilen cift sayisi responses'ta.</summary>
    public static int Collide(int n, int cols, int rows,
        NativeArray<float> px, NativeArray<float> py, NativeArray<float> vx, NativeArray<float> vy,
        NativeArray<int> cellOf, NativeArray<int> sorted, NativeArray<int> cellStart, NativeArray<int> cellFill,
        out int responses)
    {
        int ncells = cols * rows;

        // Broad-phase: counting sort (sayim -> prefix sum -> indeks dizisi)
        for (int c = 0; c <= ncells; c++) cellStart[c] = 0;
        for (int i = 0; i < n; i++)
        {
            int cx = (int)((px[i] - S2Params.MinX) / S2Params.Cell);
            int cy = (int)((py[i] - S2Params.MinY) / S2Params.Cell);
            cx = cx < 0 ? 0 : (cx >= cols ? cols - 1 : cx);
            cy = cy < 0 ? 0 : (cy >= rows ? rows - 1 : cy);
            int cell = cy * cols + cx;
            cellOf[i] = cell;
            cellStart[cell + 1] += 1;
        }
        for (int c = 0; c < ncells; c++) cellStart[c + 1] += cellStart[c];
        for (int c = 0; c < ncells; c++) cellFill[c] = cellStart[c];
        for (int i = 0; i < n; i++)
        {
            int cell = cellOf[i];
            sorted[cellFill[cell]] = i;
            cellFill[cell] += 1;
        }

        // Narrow-phase + tepki: kendi hucresi + 8 komsu, j > i, Gauss-Seidel
        int pairs = 0;
        responses = 0;
        for (int i = 0; i < n; i++)
        {
            int ci = cellOf[i];
            int cx = ci % cols;
            int cy = ci / cols;
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
                        int j = sorted[k];
                        if (j <= i) continue;
                        float dx = px[j] - px[i];
                        float dy = py[j] - py[i];
                        float d2 = dx * dx + dy * dy;
                        if (d2 >= S2Params.D2Max) continue;
                        pairs++;
                        float rvx = vx[j] - vx[i];
                        float rvy = vy[j] - vy[i];
                        float dot = rvx * dx + rvy * dy;
                        if (dot < 0f && d2 > 0f)
                        {
                            // esit kutle, elastik: normal bilesenler degis tokus
                            float f = dot / d2;
                            vx[i] += f * dx;
                            vy[i] += f * dy;
                            vx[j] -= f * dx;
                            vy[j] -= f * dy;
                            responses++;
                        }
                    }
                }
            }
        }
        return pairs;
    }
}
