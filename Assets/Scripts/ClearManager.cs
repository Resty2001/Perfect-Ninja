using UnityEngine;
using UnityEngine.SceneManagement;

public class ClearManager : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // Enemy 스크립트가 붙은 모든 오브젝트를 찾습니다.
        Enemy[] allEnemies = Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None);

        if (allEnemies.Length == 0)
        {
            SceneManager.LoadScene("Scenes/Clear");

        }
    }
}
