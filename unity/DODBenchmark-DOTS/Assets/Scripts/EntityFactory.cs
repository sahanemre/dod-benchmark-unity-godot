using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// ECS entity'lerini olusturur ve yok eder.
/// Tek sorumluluk: entity yasam dongusu (data component'lar + render).
///
/// DOD: entity'lere yalnizca veri component'lari (LocalTransform, Velocity,
/// ScreenBounds) eklenir. Davranis burada degil, MovementSystem'dedir.
/// </summary>
public class EntityFactory
{
    private readonly EntityManager entityManager;
    private readonly Mesh mesh;
    private readonly Material material;
    private readonly float moveSpeed;
    private readonly float2 screenMin;
    private readonly float2 screenMax;

    public int SpawnedCount { get; private set; }

    public EntityFactory(EntityManager entityManager, Mesh mesh, Material material,
        float moveSpeed, float2 screenMin, float2 screenMax)
    {
        this.entityManager = entityManager;
        this.mesh = mesh;
        this.material = material;
        this.moveSpeed = moveSpeed;
        this.screenMin = screenMin;
        this.screenMax = screenMax;
    }

    public void Spawn(int count)
    {
        DestroyAll();

        var renderMeshArray = new RenderMeshArray(
            new Material[] { material },
            new Mesh[] { mesh });

        var renderMeshDescription = new RenderMeshDescription(
            shadowCastingMode: ShadowCastingMode.Off,
            receiveShadows: false);

        var random = new Unity.Mathematics.Random((uint)System.DateTime.Now.Ticks);

        for (int i = 0; i < count; i++)
        {
            Entity entity = entityManager.CreateEntity();

            float px = random.NextFloat(screenMin.x, screenMax.x);
            float py = random.NextFloat(screenMin.y, screenMax.y);
            entityManager.AddComponentData(entity, new LocalTransform
            {
                Position = new float3(px, py, 0),
                Rotation = quaternion.identity,
                Scale = 1f
            });

            float angle = random.NextFloat(0f, math.PI * 2f);
            entityManager.AddComponentData(entity, new Velocity
            {
                Value = new float2(math.cos(angle), math.sin(angle)) * moveSpeed
            });

            entityManager.AddComponentData(entity, new ScreenBounds
            {
                Min = screenMin,
                Max = screenMax
            });

            RenderMeshUtility.AddComponents(entity, entityManager, renderMeshDescription, renderMeshArray,
                MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
        }

        SpawnedCount = count;
    }

    public void DestroyAll()
    {
        var query = entityManager.CreateEntityQuery(typeof(Velocity));
        entityManager.DestroyEntity(query);
        SpawnedCount = 0;
    }

    /// <summary>Varsayilan kucuk quad mesh olusturur.</summary>
    public static Mesh CreateQuadMesh()
    {
        Mesh mesh = new Mesh();
        mesh.vertices = new Vector3[]
        {
            new Vector3(-0.075f, -0.075f, 0),
            new Vector3( 0.075f, -0.075f, 0),
            new Vector3( 0.075f,  0.075f, 0),
            new Vector3(-0.075f,  0.075f, 0),
        };
        mesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
        mesh.normals = new Vector3[] { -Vector3.forward, -Vector3.forward, -Vector3.forward, -Vector3.forward };
        mesh.uv = new Vector2[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
        mesh.RecalculateBounds();
        return mesh;
    }
}
