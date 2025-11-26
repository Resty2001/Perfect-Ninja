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

        // 규리: 뭔가 화살이 Player를 그대로 패스해 버리는 게 이상해서 추가해뒀는데 일단 지금 확인한 바로는 문제 없었어요(2025.11.26 4:07)
        if (other.CompareTag("Player"))
        {
            Destroy(gameObject);
        }
    }
}
