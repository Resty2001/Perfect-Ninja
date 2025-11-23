using UnityEngine;

public class Arrow : MonoBehaviour
{
    public Vector2 velocity;
    public float ArrowLifeTime;  
    
    private bool _hasHitGround = false;
    
    void Update()
    {
        if (!_hasHitGround)
        {
            transform.position += (Vector3)velocity * Time.deltaTime;
        }

        ArrowLifeTime -= Time.deltaTime;
        if (ArrowLifeTime <= 0f)
        {
            Destroy(gameObject);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Ground") || other.CompareTag("Ceiling"))
        {
            _hasHitGround = true;
            velocity = Vector2.zero;
        }
    }
}
