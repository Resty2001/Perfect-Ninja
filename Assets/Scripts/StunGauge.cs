using UnityEngine;
using UnityEngine.UI;

public class StunGauge : MonoBehaviour
{
    public Image fillImage; // 노란색 게이지 이미지 연결
    private float stunDuration;
    private float timer;

    // 외부에서 초기화할 때 호출
    public void Setup(float duration)
    {
        stunDuration = duration;
        timer = duration;
    }

    void Update()
    {
        if (timer > 0)
        {
            timer -= Time.deltaTime;
            
            // 남은 시간 비율 계산 (0 ~ 1)
            float ratio = timer / stunDuration;
            
            if (fillImage != null)
            {
                fillImage.fillAmount = ratio;
            }
        }
        else
        {
            // 시간이 다 되면 삭제
            Destroy(gameObject);
        }
    }
}