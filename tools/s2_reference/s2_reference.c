/*
 * S2 (carpisma) referans uygulamasi — dort konfigurasyonun dogrulamasinda
 * "dogru cevap" olarak kullanilir. Motor yok; ayni algoritma, ayni RNG,
 * ayni float32 islem sirasi.
 *
 * Derleme (FMA birlestirmesini kapat; motor uygulamalari da FMA kullanmaz):
 *   gcc -O2 -ffp-contract=off -o s2_reference s2_reference.c -lm
 *   cl /O2 /fp:precise s2_reference.c          (Windows, MSVC)
 *
 * Kullanim:
 *   s2_reference <N> <run_index> <frames> [bruteforce 0|1] > cikti.csv
 * Cikti: frame,pairs,responses[,pairs_bruteforce]
 *
 * Algoritma (her kare, bu sirayla):
 *   1. Hareket: S1 ile ayni (pos += vel*dt, duvarda hiz isareti degisir ve
 *      pozisyon sinira kistirilir).
 *   2. Broad-phase: uniform grid, hucre = 2r. Her kare sifirdan, counting sort
 *      (hucre basina sayim -> prefix sum -> indeks dizisi).
 *   3. Narrow-phase: entity i, kendi hucresi + 8 komsu (satir satir: dy=-1..1,
 *      dx=-1..1), hucre icinde indeks sirasiyla, yalnizca j > i; d2 < (2r)^2.
 *   4. Tepki: kesisen ve yaklasan ciftte (vrel . d < 0) esit kutleli elastik
 *      carpisma: normal bilesenler degis tokus edilir (anlik, Gauss-Seidel).
 *   5. Kare basina kesisen cift sayisi (pairs) ve tepki verilen cift sayisi
 *      (responses).
 */
#include <math.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

/* ---- S2 ortak parametreleri (dort konfigurasyonda ayni sayilar) ---- */
static const float MIN_X = 10.0f, MIN_Y = 10.0f, MAX_X = 1270.0f, MAX_Y = 710.0f;
static const float RADIUS = 3.375f;           /* S1 parity entity'si 6.75 -> r = 3.375 */
static const float CELL = 6.75f;              /* hucre = 2r */
static const float D2_MAX = 45.5625f;         /* (2r)^2, ikilik tabanda tam */
static const float SPEED = 300.0f;
static const float TWO_PI = 6.28318530718f;
static const int BASE_SEED = 1000;
static const float FIXED_DT = 1.0f / 60.0f;   /* dogrulama modu */

/* ---- ortak RNG: murmur3 fmix32 ile karistirilmis seed + xorshift32 ---- */
static uint32_t rng_state;

static void rng_seed(int64_t seed) {
	uint32_t h = (uint32_t)seed;
	h ^= h >> 16; h *= 0x85ebca6bu; h ^= h >> 13; h *= 0xc2b2ae35u; h ^= h >> 16;
	rng_state = (h != 0u) ? h : 0x9e3779b9u;
}

static float rng_next(void) {
	rng_state ^= rng_state << 13;
	rng_state ^= rng_state >> 17;
	rng_state ^= rng_state << 5;
	return (float)(rng_state & 0x00FFFFFFu) / 16777216.0f;
}

static float rng_range(float lo, float hi) { return lo + (hi - lo) * rng_next(); }

/* ---- dunya ---- */
static int n;
static float *px, *py, *vx, *vy;
static int cols, rows, ncells;
static int *cell_of, *cell_start, *cell_fill, *sorted_idx;

static void spawn(int count, int run_index) {
	n = count;
	px = malloc(sizeof(float) * n); py = malloc(sizeof(float) * n);
	vx = malloc(sizeof(float) * n); vy = malloc(sizeof(float) * n);
	rng_seed(BASE_SEED + run_index);
	for (int i = 0; i < n; ++i) {
		px[i] = rng_range(MIN_X, MAX_X);
		py[i] = rng_range(MIN_Y, MAX_Y);
		float a = rng_range(0.0f, TWO_PI);
		vx[i] = (float)cos((double)a) * SPEED;
		vy[i] = (float)sin((double)a) * SPEED;
		(void)rng_range(0.3f, 1.0f); /* renk r, g, b: RNG akisi hizali kalsin */
		(void)rng_range(0.3f, 1.0f);
		(void)rng_range(0.3f, 1.0f);
	}
	cols = (int)ceilf((MAX_X - MIN_X) / CELL);
	rows = (int)ceilf((MAX_Y - MIN_Y) / CELL);
	ncells = cols * rows;
	cell_of = malloc(sizeof(int) * n);
	sorted_idx = malloc(sizeof(int) * n);
	cell_start = malloc(sizeof(int) * (ncells + 1));
	cell_fill = malloc(sizeof(int) * ncells);
}

static void move(float dt) {
	for (int i = 0; i < n; ++i) {
		float x = px[i] + vx[i] * dt;
		float y = py[i] + vy[i] * dt;
		if (x < MIN_X) { x = MIN_X; vx[i] = -vx[i]; }
		else if (x > MAX_X) { x = MAX_X; vx[i] = -vx[i]; }
		if (y < MIN_Y) { y = MIN_Y; vy[i] = -vy[i]; }
		else if (y > MAX_Y) { y = MAX_Y; vy[i] = -vy[i]; }
		px[i] = x; py[i] = y;
	}
}

static inline int cell_x(float x) {
	int c = (int)((x - MIN_X) / CELL);
	return c < 0 ? 0 : (c >= cols ? cols - 1 : c);
}
static inline int cell_y(float y) {
	int c = (int)((y - MIN_Y) / CELL);
	return c < 0 ? 0 : (c >= rows ? rows - 1 : c);
}

static void collide(int *out_pairs, int *out_responses) {
	/* counting sort */
	memset(cell_start, 0, sizeof(int) * (ncells + 1));
	for (int i = 0; i < n; ++i) {
		int c = cell_y(py[i]) * cols + cell_x(px[i]);
		cell_of[i] = c;
		cell_start[c + 1]++;
	}
	for (int c = 0; c < ncells; ++c) cell_start[c + 1] += cell_start[c];
	memcpy(cell_fill, cell_start, sizeof(int) * ncells);
	for (int i = 0; i < n; ++i) sorted_idx[cell_fill[cell_of[i]]++] = i;

	int pairs = 0, responses = 0;
	for (int i = 0; i < n; ++i) {
		int ci = cell_of[i];
		int cx = ci % cols, cy = ci / cols;
		for (int ny = cy - 1; ny <= cy + 1; ++ny) {
			if (ny < 0 || ny >= rows) continue;
			for (int nx = cx - 1; nx <= cx + 1; ++nx) {
				if (nx < 0 || nx >= cols) continue;
				int c = ny * cols + nx;
				for (int k = cell_start[c]; k < cell_start[c + 1]; ++k) {
					int j = sorted_idx[k];
					if (j <= i) continue;
					float dx = px[j] - px[i];
					float dy = py[j] - py[i];
					float d2 = dx * dx + dy * dy;
					if (d2 >= D2_MAX) continue;
					pairs++;
					float rvx = vx[j] - vx[i];
					float rvy = vy[j] - vy[i];
					float dot = rvx * dx + rvy * dy;
					if (dot < 0.0f && d2 > 0.0f) {
						float f = dot / d2;
						vx[i] += f * dx; vy[i] += f * dy;
						vx[j] -= f * dx; vy[j] -= f * dy;
						responses++;
					}
				}
			}
		}
	}
	*out_pairs = pairs;
	*out_responses = responses;
}

static int count_bruteforce(void) {
	int pairs = 0;
	for (int i = 0; i < n; ++i)
		for (int j = i + 1; j < n; ++j) {
			float dx = px[j] - px[i];
			float dy = py[j] - py[i];
			if (dx * dx + dy * dy < D2_MAX) pairs++;
		}
	return pairs;
}

int main(int argc, char **argv) {
	if (argc < 4) {
		fprintf(stderr, "kullanim: %s <N> <run_index> <frames> [bruteforce 0|1]\n", argv[0]);
		return 2;
	}
	int count = atoi(argv[1]);
	int run_index = atoi(argv[2]);
	int frames = atoi(argv[3]);
	int brute = argc > 4 ? atoi(argv[4]) : 0;
	(void)RADIUS;

	spawn(count, run_index);
	printf(brute ? "frame,pairs,responses,pairs_bruteforce\n" : "frame,pairs,responses\n");
	for (int f = 1; f <= frames; ++f) {
		move(FIXED_DT);
		/* brute force carpisma tepkisinden ONCE, ayni pozisyonlarda sayilir */
		int bp = brute ? count_bruteforce() : 0;
		int pairs, responses;
		collide(&pairs, &responses);
		if (brute) printf("%d,%d,%d,%d\n", f, pairs, responses, bp);
		else printf("%d,%d,%d\n", f, pairs, responses);
	}
	return 0;
}
