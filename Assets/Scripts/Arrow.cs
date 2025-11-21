using UnityEngine;

public class Arrow : MonoBehaviour
{
    public Vector2 velocity;
    
    void Update()
    {
        transform.position += (Vector3)velocity * Time.deltaTime;
    }
}
