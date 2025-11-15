using UnityEngine;
using UnityEngine.UI; // UI 기능을 위해 필수

public class StaminaHUD : MonoBehaviour
{
    [Header("References")]
    public PlayerController player; // 플레이어의 스태미나 정보를 가져오기 위해 필요
    public Image fillImage; // 실제 줄어들고 늘어날 게이지 이미지

    void Update()
    {
        if (player == null || fillImage == null) return;

        // 1. 스태미나 비율 계산 (0 ~ 1)
        float ratio = player.currentStamina / player.maxStamina;

        // 2. UI 이미지 업데이트 (Fill Amount 조절)
        fillImage.fillAmount = ratio;

        // 3. 스태미나가 꽉 찼을 때 숨기기 기능 (요청사항 반영)
        if (ratio >= 1f)
        {
            // 깜빡임 방지를 위해 이미 꺼져있으면 굳이 또 끄지 않음
            if (fillImage.gameObject.activeSelf) 
                fillImage.gameObject.SetActive(false);
        }
        else
        {
            if (!fillImage.gameObject.activeSelf) 
                fillImage.gameObject.SetActive(true);
        }
    }
}