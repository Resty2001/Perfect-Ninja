using UnityEngine;

public class Dagger : MonoBehaviour
{
    // 공격력이나 소유자 정보 등을 담을 수 있음
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        // 나중에 적(Enemy) 태그를 가진 오브젝트와 닿으면 로직 처리
        if (other.CompareTag("Enemy"))
        {
            // Debug.Log("적을 찔렀습니다!");
            // Destroy(other.gameObject); 
        }
    }
}