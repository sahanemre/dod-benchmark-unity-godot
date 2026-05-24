using UnityEngine;

/// <summary>
/// Her entity'ye (GameObject) eklenen script.
/// OOP yaklasimi: her entity kendi verisini ve davranisini tasir.
/// </summary>
public class EntityMover : MonoBehaviour
{
    private Vector2 velocity;
    private float speed;
    private Vector2 screenMin;
    private Vector2 screenMax;

    public void Initialize(float moveSpeed, Vector2 min, Vector2 max)
    {
        speed = moveSpeed;
        screenMin = min;
        screenMax = max;

        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;

        transform.position = new Vector3(
            Random.Range(screenMin.x, screenMax.x),
            Random.Range(screenMin.y, screenMax.y),
            0f
        );
    }

    void Update()
    {
        Vector3 pos = transform.position;
        pos.x += velocity.x * Time.deltaTime;
        pos.y += velocity.y * Time.deltaTime;

        if (pos.x < screenMin.x || pos.x > screenMax.x)
        {
            velocity.x = -velocity.x;
            pos.x = Mathf.Clamp(pos.x, screenMin.x, screenMax.x);
        }
        if (pos.y < screenMin.y || pos.y > screenMax.y)
        {
            velocity.y = -velocity.y;
            pos.y = Mathf.Clamp(pos.y, screenMin.y, screenMax.y);
        }

        transform.position = pos;
    }
}