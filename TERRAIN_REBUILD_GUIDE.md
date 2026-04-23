# Terrain Rebuild Guide — Voronoi Biomes with Smooth Transitions

A step-by-step plan for rebuilding the region-based procedural terrain system from scratch in a new Unity project. Incorporates improvements over the original `LandOfTheConsumers` implementation — especially around inter-region blending.

---

## 0. Goals and design principles

**What you're building:** a world partitioned into biome regions (Voronoi cells, optionally merged), each with its own terrain character (Perlin-based), blended seamlessly at the seams.

**Design principles, up front:**

1. **One authoritative heightmap per region.** All blending writes back into region heightmaps — no separate "edge mesh" overlay. The seam goes away because the seam isn't there.
2. **Distance-field blending, not edge-strip blending.** Use a signed distance to the nearest Voronoi edge to drive the blend. This is continuous across the whole region.
3. **Shared low-frequency base + per-biome high-frequency detail.** Global continuity for free.
4. **Data-driven biomes.** `ScriptableObject` per biome. Blend behavior comes from a `(BiomeA, BiomeB)` pair table.
5. **Everything deterministic from a seed.** Any run is reproducible.
6. **Generate in chunks, off the main thread.** Jobs + Burst or at minimum coroutines + `async`.

---

## 1. Project setup

Create a new Unity project (URP recommended, 2022 LTS or later).

**Packages to install:**
- Burst
- Collections
- Mathematics
- Jobs
- (Optional) URP / HDRP — whichever you're using

**Folder structure:**

```
Assets/
  Scripts/
    Terrain/
      Core/             (seed, world config, enums)
      Regions/          (Voronoi, union-find, region data)
      Heightmap/        (noise, SDF, blending)
      Meshing/          (chunk mesh generation)
      Biomes/            (ScriptableObjects)
      Debug/             (visualizers, gizmos, inspector helpers)
  Settings/
    Biomes/              (BiomeProfile assets)
    BlendRules/          (BiomePairBlend assets)
```

Commit early and often. Tag a clean baseline after each phase.

---

## 1B. Script architecture & execution pipeline

Before writing any terrain code, nail down **who owns what, who calls who, and in what order**. The original codebase tangled because responsibilities drifted — `EdgeManager` knew about meshes, `EdgeSpreader` reached through `EdgePairGenerator` to fetch the visualizer, etc. Fix that up front.

### 1B.1 Layers (dependency direction)

Scripts live in layers. **Dependencies point downward only.** No upward references, no sideways references across layers.

```
[ Layer 5 ]  Presentation     MonoBehaviours, scene lifecycle, prefabs
                │
[ Layer 4 ]  Orchestration    WorldGenerator, phase sequencing, async/await
                │
[ Layer 3 ]  Systems          RegionBuilder, HeightmapBuilder, Blender, Mesher
                │
[ Layer 2 ]  Algorithms       Voronoi, JFA, UnionFind, SDF, FBM, Smoothstep
                │
[ Layer 1 ]  Data             WorldConfig, BiomeProfile, Region, Heightmap,
                              RegionGraph, BlendWeights
                │
[ Layer 0 ]  Primitives       SeededRandom, Coord conversions, math helpers
```

Rule of thumb: **if Layer-3 `HeightmapBuilder` needs to know anything about a MonoBehaviour or a scene, you've crossed a boundary.** Refactor.

### 1B.2 What each script is, concretely

| Script | Layer | Type | Responsibility |
|---|---|---|---|
| `WorldConfig` | 1 | `ScriptableObject` | Seed, sizes, resolutions, fill rate |
| `BiomeProfile` | 1 | `ScriptableObject` | Noise params, height curve, materials |
| `BiomePairBlend` | 1 | `ScriptableObject` | Blend radius & warp per `(A,B)` pair |
| `SeededRandom` | 0 | `struct` | Deterministic sub-seed derivation |
| `CoordSpace` | 0 | `static class` | `WorldToPixel`, `PixelToWorld`, etc. |
| `Region` | 1 | POCO | Bounds, biome, heightmap, SDF, neighbors |
| `RegionGraph` | 1 | POCO | Regions + adjacency list |
| `World` | 1 | POCO | Everything the runtime needs: graph + sampling |
| `VoronoiJob` | 2 | `IJob`/`IJobParallelFor` | Pixel → seed assignment (JFA) |
| `UnionFind` | 2 | `struct` | Merge close seeds |
| `SdfJob` | 2 | `IJobParallelFor` | Chamfer / JFA distance transform |
| `FbmNoise` | 2 | `static class` | Octaved Perlin, Burst-compatible |
| `RegionBuilder` | 3 | `static class` | Phase 2 — Voronoi → `RegionGraph` |
| `BiomeAssigner` | 3 | `static class` | Phase 3 — assign biomes to regions |
| `HeightmapBuilder` | 3 | `static class` | Phase 4 — per-region noise heightmaps |
| `EdgeBlender` | 3 | `static class` | Phase 5–6 — SDF + smoothstep blend |
| `RegionMesher` | 3 | `static class` | Phase 7 — heightmap → Mesh chunks |
| `WorldGenerator` | 4 | `MonoBehaviour` | Orchestrates phases, exposes `GenerateAsync` |
| `WorldRuntime` | 5 | `MonoBehaviour` | Holds the `World`, hands out samples |
| `WorldDebugView` | 5 | `MonoBehaviour` | Textures, gizmos, inspector buttons |
| `TerrainChunkView` | 5 | `MonoBehaviour` | One per chunk, holds `MeshFilter`/`MeshRenderer` |

**Note the pattern:** Layer-3 `Builder` classes are `static` — pure functions, take input data, return output data. No state, no `Instantiate`, no `FindObjectOfType`. That makes them trivially testable and parallelizable.

Only Layer-4 and Layer-5 are `MonoBehaviour`. The whole terrain system could run in a unit test with zero scene.

### 1B.3 Execution pipeline

The linear flow from "press Generate" to "world rendered":

```
WorldGenerator.GenerateAsync(seed)
│
├─► PHASE A — Build RegionGraph
│     RegionBuilder.Build(config, seed)
│       ├─ scatter seeds                    (SeededRandom)
│       ├─ JFA pixel → seed                  (VoronoiJob)
│       ├─ union-find merge                  (UnionFind)
│       ├─ build adjacency                   (scan pixels)
│       └─ extract ordered edges             (polyline walk)
│     → returns RegionGraph
│
├─► PHASE B — Assign biomes
│     BiomeAssigner.Assign(graph, biomes, seed)
│     → mutates graph.Regions[i].Biome
│
├─► PHASE C — Per-region heightmaps
│     for each region in parallel:
│       HeightmapBuilder.Build(region, worldBaseNoise, config)
│         ├─ sample FBM per biome            (FbmNoise)
│         └─ add shared world base           (FbmNoise low-freq)
│     → region.HeightMap populated
│
├─► PHASE D — Distance fields
│     for each region in parallel:
│       region.Sdf = SdfJob.Run(region)
│
├─► PHASE E — Edge blend
│     EdgeBlender.BlendAll(graph, pairBlends)
│       for each region:
│         for each pixel with sdf < maxBlendRadius:
│           sample neighbor heightmap(s), smoothstep, write back
│     → region.HeightMap updated, region.BiomeWeights populated
│
├─► PHASE F — Mesh chunks
│     for each region:
│       for each chunk:
│         RegionMesher.BuildChunk(region, chunkRect)
│     → List<MeshData>
│
└─► PHASE G — Present
      WorldRuntime.Install(world, meshDataList)
        for each meshData:
          spawn TerrainChunkView prefab, assign Mesh + material
```

### 1B.4 Data flow — what each phase produces

Each phase should consume the output of the previous one and produce a well-typed artifact. No shared mutable blackboard.

```
Phase A  →  RegionGraph            (regions + adjacency + edges, no heights)
Phase B  →  RegionGraph            (+ BiomeProfile per region)
Phase C  →  RegionGraph            (+ HeightMap per region, unblended)
Phase D  →  RegionGraph            (+ Sdf per region)
Phase E  →  RegionGraph            (HeightMap mutated, + BiomeWeights)
Phase F  →  List<MeshData>         (chunk meshes, not yet Unity Mesh)
Phase G  →  Scene GameObjects      (TerrainChunkView instances)
```

Phases A–E are **pure** — given the same inputs, they produce the same outputs. Test them with synthetic configs.

Phases F–G touch Unity — isolate them. `MeshData` is a plain struct (vertices, triangles, uvs, colors) that only becomes a `Mesh` in Phase G, on the main thread.

### 1B.5 Async and threading rules

- **Phases A–F** must be main-thread-free except for scheduling jobs. Use `JobHandle.Complete()` inside each phase's entry point; the caller awaits a `Task` wrapping the whole chain.
- **Phase G** must be main thread — Unity `Mesh.SetVertices` etc. are not thread-safe. Use `Mesh.MeshDataArray` + `ApplyAndDisposeWritableMeshData` to build mesh data off-thread and upload on main thread.
- **Yielding between regions** — between chunk construction, `await Task.Yield()` or `yield return null` to keep the frame responsive. Don't generate 10k chunks synchronously.
- **No `static` mutable state.** Every phase takes config and seed as arguments. Otherwise determinism breaks.

### 1B.6 Events and runtime queries

After Phase G completes, `WorldRuntime` fires a single event:

```csharp
public event Action<World> WorldReady;
```

Other systems (minimap, gameplay spawning, navigation) listen to this. They do **not** poll `WorldGenerator` or reach into intermediate phase artifacts.

At runtime, sampling goes through `WorldRuntime`:

```csharp
public float SampleHeight(Vector3 worldPos);
public BiomeType SampleBiome(Vector3 worldPos);
public float SampleBiomeWeight(Vector3 worldPos, BiomeType biome);
```

These look up the owning region from the pixel grid, then read `HeightMap`/`BiomeWeights`. O(1).

### 1B.7 The forbidden patterns (from the original)

Explicitly don't do these — each one caused a bug or confusion in the previous project:

1. **MonoBehaviours reaching into other MonoBehaviours via `GetComponent` to fetch data.** (`EdgeSpreader` → `EdgePairGenerator` → `cellularVisualizer` chain.) Data should arrive as method arguments.
2. **One component spawning the next component on the same GameObject.** Phase orchestration belongs in `WorldGenerator`, not distributed across components.
3. **Queues of GameObject-based work items.** Jobs and lists of structs, not `GameObject.Instantiate` loops.
4. **Debug meshes that are actually load-bearing.** The orange edge-strip became structural. Debug views should be disposable.
5. **Public mutable fields on MonoBehaviours read by other scripts.** Make state private, expose read-only accessors on data classes.
6. **"Manager" classes that do five things.** `EdgeManager` detected, rasterized, expanded, queued, and owned geometry. Split.

### 1B.8 File/folder layout (concrete)

```
Assets/Scripts/Terrain/
├── Core/
│   ├── WorldConfig.cs              (Layer 1, SO)
│   ├── SeededRandom.cs             (Layer 0)
│   └── CoordSpace.cs               (Layer 0)
├── Data/
│   ├── Region.cs                   (Layer 1)
│   ├── RegionGraph.cs              (Layer 1)
│   ├── World.cs                    (Layer 1)
│   └── MeshData.cs                 (Layer 1)
├── Biomes/
│   ├── BiomeProfile.cs             (Layer 1, SO)
│   ├── BiomePairBlend.cs           (Layer 1, SO)
│   └── BiomeType.cs                (Layer 1, enum)
├── Algorithms/
│   ├── VoronoiJob.cs               (Layer 2)
│   ├── UnionFind.cs                (Layer 2)
│   ├── SdfJob.cs                   (Layer 2)
│   └── FbmNoise.cs                 (Layer 2)
├── Systems/
│   ├── RegionBuilder.cs            (Layer 3)
│   ├── BiomeAssigner.cs            (Layer 3)
│   ├── HeightmapBuilder.cs         (Layer 3)
│   ├── EdgeBlender.cs              (Layer 3)
│   └── RegionMesher.cs             (Layer 3)
├── Runtime/
│   ├── WorldGenerator.cs           (Layer 4, MB)
│   ├── WorldRuntime.cs             (Layer 5, MB)
│   └── TerrainChunkView.cs         (Layer 5, MB)
└── Debug/
    ├── WorldDebugView.cs           (Layer 5, MB)
    └── RegionTextureExporter.cs    (Layer 5)
```

Use Assembly Definition files (`.asmdef`) to **enforce** the layering:
- `Terrain.Core` / `Terrain.Data` / `Terrain.Biomes` — no dependencies
- `Terrain.Algorithms` → depends on `Core`, `Data`
- `Terrain.Systems` → depends on `Core`, `Data`, `Biomes`, `Algorithms`
- `Terrain.Runtime` → depends on everything below
- `Terrain.Debug` → depends on everything below

Now Unity will refuse to compile a circular reference or an upward reference. The architecture self-enforces.

### 1B.9 Testing strategy

Each Layer-3 `Builder` gets an edit-mode test:

- `RegionBuilder_ProducesExpectedCellCount_ForKnownSeed`
- `HeightmapBuilder_DeterministicForSameSeed`
- `EdgeBlender_NeighborsMatchAtSharedPixel`
- `SdfJob_ZeroOnBoundary_MaxAtCenter`

Use a 4×4 world config. Each test runs in <100ms. This is feasible only because Layers 1–3 have no Unity scene dependencies.

### 1B.10 Suggested iteration loop

During development, bind a keybind to `WorldDebugView.Regenerate()` — press it in Play mode to re-roll the world. Export the debug textures each time. This tight loop (<2s generate + visual diff) is what makes iterating on blend formulas pleasant.

---

## 1C. Ascending-test build order

**The core rule: no layer gets written until the layer below it is proven in isolation.** Each layer has an exit criterion — a specific, verifiable test that must pass before you start the next layer. Don't move up until the layer below is green.

This is why Section 1B forbids upward references: it makes this incremental build feasible. If Layer 2 referenced Layer 3, you couldn't test Layer 2 without Layer 3 existing.

### Milestone ladder

Each milestone = one commit + one green test suite + one visual confirmation. Do not skip.

```
                                 ┌─────────────────────────────┐
  M7 — Full world renders   ◄──  │ Layer 5  Presentation       │
                                 └─────────────────────────────┘
                                 ┌─────────────────────────────┐
  M6 — Generator orchestrates ◄─ │ Layer 4  Orchestration      │
                                 └─────────────────────────────┘
                                 ┌─────────────────────────────┐
  M5 — Mesher builds a mesh  ◄── │ Layer 3e RegionMesher       │
  M4 — Blend is seamless     ◄── │ Layer 3d EdgeBlender        │
  M3 — Heightmaps look right ◄── │ Layer 3c HeightmapBuilder   │
  M2 — Biomes assigned       ◄── │ Layer 3b BiomeAssigner      │
  M1 — Regions partitioned   ◄── │ Layer 3a RegionBuilder      │
                                 └─────────────────────────────┘
                                 ┌─────────────────────────────┐
  M0b — Algorithms correct   ◄── │ Layer 2  Algorithms         │
                                 └─────────────────────────────┘
                                 ┌─────────────────────────────┐
  M0a — Data roundtrips      ◄── │ Layer 1  Data               │
                                 └─────────────────────────────┘
                                 ┌─────────────────────────────┐
  M0  — Seed is deterministic ◄─ │ Layer 0  Primitives         │
                                 └─────────────────────────────┘
```

### M0 — Primitives (Layer 0)

**Build:** `SeededRandom`, `CoordSpace`.

**Tests (edit-mode):**
- `SeededRandom_SameSeedProducesSameSequence`
- `SeededRandom_DifferentSubSystemsIndependent` — hash("regions") ≠ hash("noise")
- `CoordSpace_WorldToPixelToWorld_Roundtrips` — for a range of positions, no drift
- `CoordSpace_PixelToWorld_KnownValues` — a few hand-computed cases

**Exit criterion:** all pass, sub-millisecond per test.

**Why first:** every layer above uses coord conversion. A bug here corrupts every phase.

### M0a — Data (Layer 1)

**Build:** `WorldConfig`, `BiomeProfile`, `BiomePairBlend`, `Region`, `RegionGraph`, `World`, `MeshData`.

**Tests:**
- `Region_HeightMap_AllocationSize_MatchesBounds`
- `RegionGraph_AddNeighbor_IsSymmetric`
- `BiomeProfile_SerializesAndLoads` — create one, write to disk, read back, compare

**Exit criterion:** all POCOs constructible, serialization round-trips, no Unity scene needed.

**Why:** POCOs with no logic — fast to verify. Getting the shapes right here prevents API churn later.

### M0b — Algorithms (Layer 2)

**Build one at a time, test each:**

**`UnionFind`**
- `UnionFind_FindAfterUnion_ReturnsSameRoot`
- `UnionFind_PathCompression_ReducesDepth`

**`FbmNoise`**
- `FbmNoise_SameCoordinate_SameValue` (determinism)
- `FbmNoise_RangeIsNormalized` — output stays in expected range for typical params

**`VoronoiJob` (JFA)**
- `Voronoi_EveryPixelHasAnOwner`
- `Voronoi_PixelsNearSeed_OwnedBySeed`
- `Voronoi_OutputDeterministic`

**`SdfJob`**
- `Sdf_ZeroOnBoundaryPixels`
- `Sdf_MonotonicallyIncreasesInward`
- `Sdf_MatchesEuclideanForSimpleShapes` — rectangle ground truth

**Exit criterion:** each algorithm passes its tests in isolation. You can write these tests with hand-built `NativeArray<int>` inputs — no `RegionBuilder` needed yet.

**Why this matters most:** algorithms are the hardest to debug once layered into a system. Prove them standalone.

### M1 — RegionBuilder (Layer 3a)

**Build:** `RegionBuilder.Build(config, seed) → RegionGraph`.

**Tests:**
- `RegionBuilder_FixedSeed_ProducesExpectedRegionCount` — hard-coded expected value for a known seed
- `RegionBuilder_AllPixelsAssigned`
- `RegionBuilder_AdjacencySymmetric` — if A lists B, B lists A
- `RegionBuilder_MergeDistanceZero_NoMergeOccurs`
- `RegionBuilder_MergeDistanceLarge_OneRegionDominates`

**Visual confirmation:** a **RegionDebugScene**. One MonoBehaviour that:
1. Calls `RegionBuilder.Build(config, seed)`
2. Renders the region-ID map as a colored `Texture2D` on a quad
3. Re-rolls on spacebar

You should see a Voronoi partition with merged cells. **Do not proceed to M2 until this looks right.**

**Exit criterion:** tests green + debug scene shows plausible regions at multiple seeds.

### M2 — BiomeAssigner (Layer 3b)

**Build:** `BiomeAssigner.Assign(graph, biomes, seed)`.

**Tests:**
- `BiomeAssigner_EveryRegionGetsBiome`
- `BiomeAssigner_DeterministicForSeed`
- `BiomeAssigner_RespectsBiomeAvailability`

**Visual confirmation:** extend **RegionDebugScene** — color each region by its assigned biome color. You should see random but stable biome distribution.

**Exit criterion:** running N times with the same seed yields identical biome colorings; different seeds yield different but valid ones.

### M3 — HeightmapBuilder (Layer 3c)

**Build:** `HeightmapBuilder.Build(region, worldBaseNoise, config)` — populates one region's `HeightMap`.

**Tests:**
- `Heightmap_DeterministicForRegion`
- `Heightmap_RangeWithinBiomeAmplitude`
- `Heightmap_WorldBaseContributionPresent` — disable world base, diff height; should differ by expected magnitude
- `Heightmap_AdjacentRegions_ValuesDifferAtBoundary` — confirms **unblended** state (we want the seam visible here, the next layer fixes it)

**Visual confirmation:** **HeightmapDebugScene**. Spawn a flat mesh per region at height `HeightMap[x,y]`, no blending. You should see distinct biomes with **visible seams at boundaries**. That's correct — seams are the problem M4 solves.

**Exit criterion:** heights look like the biome they should (mountains are jagged, deserts are smooth) AND seams are clearly visible.

### M4 — EdgeBlender (Layer 3d) — the money layer

**Build:** `SdfJob` integration + `EdgeBlender.BlendAll(graph, pairBlends)`.

**Tests:**
- `Blender_HeightAtSharedBoundary_MatchesBetweenNeighbors` — the critical invariant
- `Blender_DeepInteriorUnchanged` — blending far from any edge is a no-op
- `Blender_BiomeWeights_SumToOne`
- `Blender_Idempotent_OnSecondRun` — running it twice doesn't drift

**Visual confirmation:** re-render **HeightmapDebugScene**. Seams from M3 should now be gone. Two adjacent extreme biomes (desert + mountain) should transition naturally.

**Exit criterion:** diff the heightmap texture vs M3's output — changed only within `maxBlendRadius` of edges. Visual seam disappears.

**If M4 fails:** do not move on. This is the feature. Iterate on smoothstep curve, blend radius, domain warp. Compare outputs side-by-side.

### M5 — RegionMesher (Layer 3e)

**Build:** `RegionMesher.BuildChunk(region, chunkRect) → MeshData`.

**Tests:**
- `Mesher_VertexCount_MatchesChunkSize`
- `Mesher_Triangles_FormValidManifold` — every edge used by ≤2 triangles
- `Mesher_AdjacentChunks_SharedEdgeVertices_Match` — no T-junctions
- `Mesher_UVs_InUnitRange`

**Visual confirmation:** **MeshDebugScene** — spawn a single region's chunks with a plain material. Rotate around it. No holes, no spikes, no T-junctions.

**Exit criterion:** one region meshed cleanly in a test scene.

### M6 — WorldGenerator (Layer 4)

**Build:** `WorldGenerator.GenerateAsync(seed)` that orchestrates Phases A–F.

**Tests:**
- `Generator_EndToEnd_CompletesWithoutError`
- `Generator_ProducesSameWorld_ForSameSeed`
- `Generator_AsyncDoesNotBlockMainThread` — measure frame time during generation

**Visual confirmation:** a single `WorldGenerator` MonoBehaviour in a scene, `Generate` button in inspector. Builds a full `World` object. Not yet rendered.

**Exit criterion:** `World` object exists, can query `SampleHeight`, values match what a direct call to `HeightmapBuilder` would return.

### M7 — Presentation (Layer 5)

**Build:** `WorldRuntime`, `TerrainChunkView`, `WorldDebugView`.

**Tests:** mostly visual — by this point unit tests are lower value than frame-by-frame inspection.

**Visual confirmation:** generate a full world, render all chunk meshes, orbit camera. Regenerate with a different seed, confirm it rebuilds cleanly.

**Exit criterion:** the complete world renders, regenerate-on-keypress works, debug textures export cleanly.

### Rules of the ladder

1. **Never write layer N until layer N-1 is green and visually confirmed.** It's tempting to stub upward — don't.
2. **Every milestone has a throwaway debug scene.** Don't try to test M3 in the full-world scene. Make a minimal scene per milestone.
3. **Keep old debug scenes around.** They're your regression tests. If M6 behaves oddly, reopen RegionDebugScene and verify M1 still passes.
4. **Commit at each milestone with a tag** — `m1-regions`, `m2-biomes`, `m3-heightmaps`, `m4-blend`, etc. You can bisect if something regresses.
5. **If a milestone is hard, simplify before optimizing.** A working slow version beats a half-built fast version. Burst comes after M5.
6. **Tests are `[Test]`s in edit mode, not play mode.** They run without entering Play. Sub-second iteration.

### Why this order specifically

- Seed determinism first, because every layer depends on it.
- Voronoi before heightmaps, because biome assignment needs regions.
- Heightmaps **with visible seams** before blending, because you need to see the problem before you can verify the fix.
- Blending before meshing, because mesh generation is expensive and you want the heightmap right before you commit it to triangles.
- Generator last among non-visual layers, because it's the thinnest layer — just wiring.
- Presentation last, because it's the hardest to unit-test and depends on everything.

Build bottom-up. Test each layer in isolation. Only move up when green.

---

## 2. Phase 1 — World configuration & seed

**Build:** `WorldConfig` ScriptableObject and a `DeterministicRandom` wrapper.

### `WorldConfig`
- `int seed`
- `Vector2Int worldSizeInBiomes` (e.g. 16×16)
- `int biomeSize` (pixels per biome, e.g. 128)
- `int pixelsPerUnit` (heightmap resolution, e.g. 2)
- `float biomeSeedFillRate` (e.g. 0.9 — 10% empty = organic spacing)
- `float seedMergeDistance` (world-space distance under which seeds merge into one region)

### `DeterministicRandom`
Wrap `Unity.Mathematics.Random`. Derive sub-seeds for each subsystem:
```
seedRegions = hash(seed, "regions")
seedNoise   = hash(seed, "noise")
seedBiomes  = hash(seed, "biomes")
```
This lets you tweak any one system without re-rolling the others.

**Checkpoint:** A script that spawns a cube scaled to `worldSizeInBiomes * biomeSize`. Visual sanity check.

---

## 3. Phase 2 — Voronoi regions

**Build:** `RegionGraph` — the partition of the world into cells, with neighbor information.

### Step 2.1: Seed points
- For every biome cell in the grid, roll a uniform `[0,1)`. If `< biomeSeedFillRate`, place a seed at a jittered position inside that cell. Otherwise leave empty.
- Empty cells get their pixels assigned to the nearest non-empty seed — organic cell-size variation.

### Step 2.2: Assign pixels to nearest seed
Brute-force is fine for small worlds (`worldSize * biomeSize²` pixels × seed count). For larger worlds, use a uniform grid (jump-flood or a kD-tree).

Job-friendly approach: **Jump Flood Algorithm (JFA)** in a `NativeArray<int>` where each cell stores the owning seed index. JFA is O(n log n) on the pixel count and highly parallelizable.

### Step 2.3: Merge close seeds into regions (union-find)
- Build a union-find over seeds.
- For each pair of seeds within `seedMergeDistance`, `Union(a, b)`.
- After processing, each seed's `Find(i)` is the region ID.

### Step 2.4: Build region adjacency
While scanning the pixel→seed map, for every pixel whose 4-neighbors belong to a different region, record the pair `(regionA, regionB)`. Deduplicate.

### Step 2.5: Extract Voronoi edges
For each adjacent pair:
- Collect boundary pixels (pixels on the frontier between them).
- Order them into a polyline (nearest-neighbor walk from one endpoint).
- Simplify with Douglas–Peucker if you want a cleaner spine.

**Checkpoint:** Write the region ID map to a `Texture2D`, color each region uniquely, render on a quad. You should see a Voronoi partition with some merged cells.

---

## 4. Phase 3 — Biome assignment

**Build:** `BiomeProfile` ScriptableObject + an assignment pass.

### `BiomeProfile` fields
- `string displayName`
- `BiomeType type` (enum: Ocean, Beach, Grassland, Forest, Desert, Mountain, Tundra, ...)
- `AnimationCurve heightCurve` — remaps noise `[0,1]` to a height profile
- `float baseHeight`, `float heightAmplitude`
- Noise settings: `scale`, `octaves`, `persistence`, `lacunarity`, `offset`
- Material/splat references (used later)
- `float preferredElevation` (for assignment by "world band")

### Assignment strategies
Pick one (or combine):

1. **Random from pool** — simplest, what the original did.
2. **Elevation-banded** — sample a world-wide low-frequency Perlin per region centroid; use it as a "continent mask" that biases toward ocean/lowland/highland biomes.
3. **Temperature × Moisture** — two low-frequency noise fields → Whittaker-style biome lookup.

Start with (1), upgrade to (3) once everything else works.

**Checkpoint:** Color the region map by biome type.

---

## 5. Phase 4 — Per-region heightmap

**Build:** `RegionHeightmap` — a 2D `float` array per region.

### Coordinate spaces
- **World pixel space:** 0..worldWidth*pixelsPerUnit in X and Z.
- **Region-local pixel space:** `(x - region.minX, z - region.minZ)`.
- **World units:** divide pixel coords by `pixelsPerUnit`.

Pick one and use it consistently. Bugs in the original came from mixing these.

### Step 4.1: Allocate
For each region, find its axis-aligned bounding box in pixel space. Allocate `HeightMap[boundsW, boundsH]`.

### Step 4.2: Sample noise
For every pixel owned by the region:
```
worldX = (pixelX + region.minX) / pixelsPerUnit
worldZ = (pixelY + region.minZ) / pixelsPerUnit
n = fbm(worldX, worldZ, biome.octaves, biome.persistence, biome.lacunarity, biome.scale, biome.offset)
h = biome.baseHeight + biome.heightCurve.Evaluate(n) * biome.heightAmplitude
HeightMap[x, y] = h
```

### Step 4.3: Shared low-frequency base layer
**This is a big improvement over the original.** In addition to per-region noise, add a single world-wide low-octave Perlin value:
```
h_final = h_per_region + worldBase(worldX, worldZ) * worldBaseAmplitude
```
All regions now share large-scale features. Rivers and ridges can span biomes. Each biome still has its own character on top.

**Checkpoint:** Render region meshes (flat quads colored by height) — you should see distinct biome character with continuous large-scale structure.

---

## 6. Phase 5 — Signed distance field to region edges

**Build:** `RegionSDF` — per-region 2D array of signed distances.

For every pixel in the region:
- Distance to the nearest Voronoi edge *of this region*.
- Positive inside, negative outside (you only need inside, so clamp at 0).

### How to compute
- Run a BFS / chamfer distance transform from the region's boundary pixels.
- Or use JFA in reverse.

You only need distances up to your maximum blend width (e.g., 40 pixels). Cap the BFS at that depth.

**Why this matters:** every blend below uses this SDF. It replaces the "wing spread" approach entirely.

---

## 7. Phase 6 — Edge blending (the main improvement)

**Goal:** Near every Voronoi edge, the heights on both sides should meet seamlessly.

### The blend pass
For every region, for every pixel whose SDF value is less than `blendRadius`:

```
t = smoothstep(0, blendRadius, sdf)   // 0 at edge, 1 deep inside
neighborHeight = sample neighbor region's HeightMap at this world position
h_blended = lerp(neighborHeight, h_own, t)
HeightMap[x, y] = h_blended
```

Key points:

1. **`smoothstep`, not linear** — no crease at the seam.
2. **Sample the *neighbor's* heightmap, not a spine average.** No info is lost. Where two regions meet, each one pulls toward the other and they meet in the middle.
3. **Run this pass on both regions simultaneously.** After the pass, both regions agree on the height at the shared edge.
4. **Pixels with multiple nearby neighbors** (near a Voronoi vertex where 3+ regions meet): weight by `1/distance²` to each neighbor.

### Adaptive blend width
Make `blendRadius` dependent on the biome pair:

```
blendRadius = BiomePairBlend[ (biomeA, biomeB) ].radius
```

Desert↔desert: small (2–4 units). Ocean↔mountain: large (15–25 units). Sharp cliffs: 0.

Store these in `BiomePairBlend` ScriptableObjects — authored, not computed.

### Domain warp the blend mask
Instead of `t = smoothstep(0, blendRadius, sdf)`:
```
warp = fbm(worldX * 0.05, worldZ * 0.05) * warpAmplitude
t = smoothstep(0, blendRadius, sdf + warp)
```
The boundary stops reading as a geometric line — biomes interdigitate.

### Slope-aware bias (optional, nice touch)
If one neighbor has much higher local slope near the edge, bias the blend toward it (steep terrain tends to "cut" into flatter terrain visually):
```
slopeBias = saturate((slope_neighbor - slope_own) * k)
t = t - slopeBias * 0.3
```

**Checkpoint:** Generate two adjacent regions with wildly different biomes (desert + mountain). The transition should feel natural, with no visible line and no crease.

---

## 8. Phase 7 — Mesh generation

**Build:** `RegionMesher` — converts a `RegionHeightmap` into one or more Unity `Mesh` objects.

### Step 7.1: Chunking
Split each region into fixed-size chunks (e.g., 64×64 vertices). Needed for:
- Culling and LOD
- Mesh vertex limits (65k vertices if using UInt16 indices)
- Streaming

### Step 7.2: Mesh generation
For each chunk:
- Vertices from the heightmap.
- Triangles as two per quad.
- UVs from local position.
- Normals via `RecalculateNormals()` or compute analytically from neighboring heights (smoother).
- Vertex colors or UV2 channel storing biome weights for splatmapping.

### Step 7.3: Handle chunk seams
Adjacent chunks share an edge of vertices. Ensure both chunks sample the same heights at the shared vertices (no T-junctions). Easiest: each chunk is responsible for `[x0..x1]` inclusive; neighbor starts at `x1`.

### Step 7.4: LOD (optional)
Skip for now. Add later via vertex decimation per chunk.

**Checkpoint:** A full world renders as a continuous mesh with no visible region boundaries.

---

## 9. Phase 8 — Materials and splatmapping

**Build:** a material that blends textures based on biome weights.

### Per-vertex biome weights
In the blend pass (Phase 6), also write biome weights into a secondary buffer:
```
weights[biomeA] = t
weights[biomeB] = 1 - t
```
(For multi-neighbor pixels, weights distribute across 3–4 biomes.)

Encode these into mesh vertex colors (`Color32` supports 4 channels — 4 biomes per pixel is usually enough).

### Shader
A splatmap shader that:
- Samples `biomeCount` albedo textures
- Weighs them by vertex color RGBA
- Supports normal maps per biome

URP's Terrain Lit shader handles this out of the box for Unity terrain, but with a custom mesh you'll want a small Shader Graph or hand-written shader.

### Why this is better than the original
You get material transitions for free — they use the exact same `t` that drives the height blend. Zero extra work, perfectly consistent.

---

## 10. Phase 9 — Performance

Order of operations once everything works:

1. **Move hot loops to Burst Jobs.** Voronoi assignment, noise generation, blend pass, SDF computation — all embarrassingly parallel.
2. **Use `NativeArray` end-to-end.** Managed arrays stay at the boundaries only.
3. **Async mesh construction.** `Mesh.MeshDataArray` + `Mesh.ApplyAndDisposeWritableMeshData` for background generation.
4. **Stream by chunk.** Generate chunks near the camera first.
5. **Cache heightmap as `half`** if memory is tight.

---

## 11. Phase 10 — Debug and authoring tools

Non-optional. Build these as you go, not at the end.

- **Region ID texture** → writes to disk / quad in scene
- **Biome color texture**
- **SDF texture** (grayscale, bright inside each region)
- **Blend weight texture** (RGB = top 3 biome weights per pixel)
- **Gizmos** for Voronoi seeds, edges, and region centroids
- **Inspector button** on your `WorldGenerator` MonoBehaviour: `Regenerate`, `Regenerate (new seed)`, `Export PNGs`

Also: a `WorldConfig` diff-tool in the inspector that shows which subsystem each field affects.

---

## 12. Common pitfalls (learned from the original)

- **Mixing pixel and world coordinates.** Pick one per subsystem. Convert at boundaries only. Name variables with suffix: `_px` vs `_u`.
- **Per-edge "strip" meshes overlaid on region meshes** — don't. They cause seams and Z-fighting. Blend into the region heightmap instead.
- **Global side-voting for edge ownership.** Always per-pixel. Voronoi edges can curve.
- **New `Material` per edge/region.** Cache shared materials. The original leaked one per edge.
- **Forgotten seed propagation.** Hash the world seed into every subsystem. Otherwise regenerating one system changes the others.
- **Float precision at world scale.** If your world is bigger than ~5k units, switch to double for seed positions or use an origin-shift scheme.
- **Mesh normals wrong at chunk seams.** Compute normals from the heightmap with neighbor lookups, not from the mesh — the mesh doesn't know about the next chunk's heights.

---

## 13. Feature roadmap after the core ships

Ordered by value-to-effort:

1. **Rivers.** Trace down-slope from high-elevation points across regions. The shared base layer makes this coherent.
2. **Erosion.** Hydraulic erosion pass over the combined heightmap. Works *across* region boundaries — another argument for writing into region heightmaps, not strips.
3. **Roads / paths.** A* on the heightmap with biome-type traversal costs.
4. **Vegetation scatter.** Poisson-disk per biome, filtered by slope and height curves from the `BiomeProfile`.
5. **Caves.** 3D noise carving under the surface, gated by biome.
6. **Infinite / streaming world.** Generate biomes on demand in world-space chunks instead of a fixed grid.

---

## 14. Suggested build order (tl;dr)

1. `WorldConfig` + deterministic seed — 1 session
2. Voronoi seeds + JFA pixel assignment — 1 session
3. Union-find region merging + adjacency — 1 session
4. Edge extraction + polyline ordering — 1 session
5. `BiomeProfile` + random assignment — 1 session
6. Per-region Perlin heightmaps — 1 session
7. Shared world base layer — short
8. Region SDF — 1 session
9. Smoothstep blend using SDF — 1 session
10. Chunked mesh generation — 1–2 sessions
11. Debug visualizers — incremental, throughout
12. Domain-warped blend + pair-specific radius — 1 session
13. Splatmap materials — 1–2 sessions
14. Burst/Jobs pass on hot paths — 1–2 sessions

After step 9 you already have a better-looking transition than the original had. Everything after is polish and performance.

---

## 15. Reference: what the original did well

Keep these ideas:

- Voronoi partitioning with organic merging
- Per-biome noise presets as data assets
- Queuing region generation to keep the main thread responsive
- Ordered spine construction (still useful for rivers along borders, decorations, etc.)

What to drop:

- Separate edge-strip mesh overlay → replaced by SDF blend into region heightmap
- Global "positive-side" voting → replaced by per-pixel neighbor sampling
- Linear wing blend → replaced by smoothstep
- Fixed wing width → replaced by biome-pair radius table
- 50/50 spine height average → replaced by actual heightmap crossfade
- Separate `EdgeManager`/`EdgePairGenerator`/`EdgeSpreader` chain → replaced by one SDF-driven blend pass

---

## 16. Minimal public API sketch

The shape of what you're building, for reference:

```csharp
public sealed class WorldGenerator : MonoBehaviour
{
    public WorldConfig config;
    public BiomeProfile[] biomes;
    public BiomePairBlend[] pairBlends;

    public World Generate(int seed);                 // synchronous, for tests
    public Task<World> GenerateAsync(int seed);      // production path
}

public sealed class World
{
    public RegionGraph Graph { get; }
    public IReadOnlyList<Region> Regions { get; }
    public float SampleHeight(float worldX, float worldZ);
    public BiomeType SampleBiome(float worldX, float worldZ);
}

public sealed class Region
{
    public int Id { get; }
    public RectInt BoundsPx { get; }
    public BiomeProfile Biome { get; }
    public float[,] HeightMap { get; }
    public float[,] Sdf { get; }
    public byte[,] BiomeWeightsPacked { get; }      // top-4 neighbors
    public IReadOnlyList<int> Neighbors { get; }
}
```

Start from this skeleton and fill it in phase by phase.

---

## 17. Final advice

- **Write throwaway scripts to visualize each step.** Textures on quads are faster than gizmos for debugging 2D data.
- **Test with a 4×4 world** while developing. Regenerate in under a second. Scale up only when correct.
- **Don't optimize before Phase 9.** Burst-ifying a wrong algorithm is wasted work.
- **Keep the original project around as a reference,** but don't copy code. Reimplement cleanly — you already know what you wish you'd done differently.

Good luck. The architecture you're heading toward is genuinely solid — Voronoi biomes + shared base + SDF blending is what commercial terrain tools (Gaia, MapMagic) converge on. You're on the right track.
