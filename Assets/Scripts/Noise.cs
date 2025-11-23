using UnityEngine;

public class Noise : MonoBehaviour
{
    // 생성되자마자 0.1초 뒤에 사라짐
    void Start()
    {
        Destroy(gameObject, 0.1f);
    }
}