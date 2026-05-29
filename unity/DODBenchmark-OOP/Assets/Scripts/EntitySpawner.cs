using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// OOP entity'lerini (GameObject + EntityMover) olusturur ve yok eder.
/// Tek sorumluluk: entity yasam dongusu yonetimi.
/// </summary>
public class EntitySpawner
{
    private readonly List<GameObject> active = new List<GameObject>();
    private readonly Sprite sprite;
    private readonly float moveSpeed;
    private readonly Vector2 screenMin;
    private readonly Vector2 screenMax;

    public int ActiveCount => active.Count;

    public EntitySpawner(Sprite sprite, float moveSpeed, Vector2 screenMin, Vector2 screenMax)
    {
        this.sprite = sprite;
        this.moveSpeed = moveSpeed;
        this.screenMin = screenMin;
        this.screenMax = screenMax;
    }

    public void Spawn(int count)
    {
        Clear();
        for (int i = 0; i < count; i++)
        {
            GameObject go = new GameObject($"Entity_{i}");
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = new Color(
                Random.Range(0.3f, 1f),
                Random.Range(0.3f, 1f),
                Random.Range(0.3f, 1f));
            go.transform.localScale = Vector3.one * 0.15f;

            EntityMover mover = go.AddComponent<EntityMover>();
            mover.Initialize(moveSpeed, screenMin, screenMax);

            active.Add(go);
        }
    }

    public void Clear()
    {
        foreach (var go in active) Object.Destroy(go);
        active.Clear();
    }
}
