using UnityEngine;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    [Header("Settings")]
    public float groundSpeed = 5f;
    public float hangSpeed = 3f;
    public float climbSpeed = 5f;
    
    [Header("Stamina Settings")]
    public float maxStamina = 4f;
    public float staminaRecoveryRate = 0.5f;
    public float staminaDrainRate = 1f;

    [Header("Gravity Settings")]
    public float normalGravity = 1f;
    public float heavyGravity = 3f;

    [Header("State Flags")]
    public bool isGrounded;    
    public bool isHanging;     
    public bool isActing;      
    public bool isForcedFall;  
    public bool isStunned;     

    // UI 스크립트에서 읽어가야 하므로 public 유지
    public float currentStamina;

    [Header("Visuals")]
    public float motionDuration = 0.5f;
    private Vector3 originalScale = new Vector3(1f, 1.5f, 1f);
    private Vector3 hangScale = new Vector3(1.5f, 0.5f, 1f);

    private Rigidbody2D rb;
    private Coroutine scaleCoroutine;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        
        currentStamina = maxStamina;
        transform.localScale = originalScale;

        isGrounded = true;
        isHanging = false;
        isActing = false;
        isForcedFall = false;
    }

    void Update()
    {
        if (isStunned) return;

        HandleInput();
        HandleStamina();
    }

    void FixedUpdate()
    {
        if (isStunned) return;
        Move();
    }

    void Move()
    {
        if (isActing) return;

        float xInput = Input.GetAxisRaw("Horizontal");
        float currentSpeed = isHanging ? hangSpeed : groundSpeed;

        if (isHanging && xInput != 0)
        {
            currentStamina -= staminaDrainRate * Time.deltaTime;
        }

        // Unity 6 최신 버전이 아니라면 linearVelocity 대신 velocity 사용 (여기선 velocity로 통일하겠습니다)
        rb.linearVelocity = new Vector2(xInput * currentSpeed, rb.linearVelocity.y);
    }

    void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (isHanging)
            {
                DropFromCeiling(false);
            }
            else if (isGrounded)
            {
                StartClimbing();
            }
        }
    }

    void StartClimbing()
    {
        isGrounded = false;
        isActing = true;
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.up * climbSpeed;
        StartScaleCoroutine(hangScale);
    }

    void DropFromCeiling(bool forced)
    {
        isHanging = false;
        isActing = true;
        isForcedFall = forced;

        StartScaleCoroutine(originalScale);

        if (forced) rb.gravityScale = heavyGravity;
        else rb.gravityScale = normalGravity;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ceiling"))
        {
            if (isActing)
            {
                isActing = false;
                isHanging = true;
                rb.linearVelocity = Vector2.zero;
                rb.gravityScale = 0f;
            }
        }

        if (collision.gameObject.CompareTag("Ground"))
        {
            if (!isGrounded)
            {
                isGrounded = true;
                isActing = false;
                rb.gravityScale = normalGravity;

                if (isForcedFall) StartCoroutine(StunRoutine());
                // else 소음 발생 로직
                
                isForcedFall = false;
            }
        }
    }

    void HandleStamina()
    {
        // 스태미나 수치 계산만 담당
        if (isGrounded && currentStamina < maxStamina)
        {
            currentStamina += staminaRecoveryRate * Time.deltaTime;
        }
        
        currentStamina = Mathf.Clamp(currentStamina, 0, maxStamina);

        if (isHanging && currentStamina <= 0)
        {
            DropFromCeiling(true);
        }
    }

    IEnumerator StunRoutine()
    {
        isStunned = true;
        rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(1.0f);
        isStunned = false;
    }

    void StartScaleCoroutine(Vector3 target)
    {
        if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
        scaleCoroutine = StartCoroutine(ChangeScaleProcess(target));
    }

    IEnumerator ChangeScaleProcess(Vector3 targetScale)
    {
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;
        while (elapsed < motionDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / motionDuration;
            transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }
        transform.localScale = targetScale;
    }
}