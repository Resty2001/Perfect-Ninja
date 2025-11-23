using UnityEngine;
using System.Collections;
// [추가] Scene 관리를 위해 필요하지만 지금은 주석 처리된 코드에만 사용됨
using UnityEngine.SceneManagement; 

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

    [Header("Prefabs")]
    public GameObject noisePrefab; 
    public GameObject daggerPrefab;

    [Header("State Flags")]
    public bool isGrounded;
    public bool isHanging;
    public bool isClimbing; 
    public bool isForcedFall;
    public bool isStunned;
    public bool isAttacking;
    public bool isDead; // [추가] 죽은 상태 확인용

    public float currentStamina;

    [Header("Visuals")]
    public float motionDuration = 0.5f;
    private Vector3 originalScale = new Vector3(1f, 1.5f, 1f);
    private Vector3 hangScale = new Vector3(1.5f, 0.5f, 1f);
    
    private int facingDirection = 1; 

    private Rigidbody2D rb;
    private Coroutine scaleCoroutine;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        currentStamina = maxStamina;
        transform.localScale = originalScale;

        isGrounded = true;
        isHanging = false;
        isClimbing = false;
        isForcedFall = false;
        isAttacking = false;
        isDead = false;
    }

    void Update()
    {
        // [수정] 죽었거나 기절했으면 입력 불가
        if (isStunned || isDead) return;

        HandleInput();
        HandleStamina();
    }

    void FixedUpdate()
    {
        // [수정] 죽었거나 기절했으면 이동 불가
        if (isStunned || isDead) return;
        Move();
    }

    void Move()
    {
        if (isClimbing || isAttacking) return;

        if (!isGrounded && !isHanging)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        float xInput = Input.GetAxisRaw("Horizontal");
        
        if (xInput != 0)
        {
            facingDirection = (int)Mathf.Sign(xInput);
        }

        float currentSpeed = isHanging ? hangSpeed : groundSpeed;

        if (isHanging && xInput != 0)
        {
            currentStamina -= staminaDrainRate * Time.deltaTime;
        }

        rb.linearVelocity = new Vector2(xInput * currentSpeed, rb.linearVelocity.y);
    }

    void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.Space) && !isAttacking)
        {
            if (isGrounded)
            {
                StartCoroutine(ProcessGroundAttack());
            }
            else if (!isGrounded && !isHanging && !isClimbing)
            {
                PerformAirAttack();
            }
        }

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

    // --- 공격 로직 ---
    IEnumerator ProcessGroundAttack()
    {
        isAttacking = true;
        rb.linearVelocity = Vector2.zero;

        GameObject dagger = Instantiate(daggerPrefab, transform.position, Quaternion.identity);
        dagger.transform.SetParent(transform); 
        dagger.transform.localScale = new Vector3(1f, 0.2f, 1f); 

        Vector3 startPos = Vector3.zero;
        Vector3 endPos = new Vector3(facingDirection * 1.0f, 0, 0); 

        float attackHalfDuration = 0.25f;
        float elapsed = 0f;

        while (elapsed < attackHalfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / attackHalfDuration;
            dagger.transform.localPosition = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }
        dagger.transform.localPosition = endPos;

        elapsed = 0f;
        while (elapsed < attackHalfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / attackHalfDuration;
            dagger.transform.localPosition = Vector3.Lerp(endPos, startPos, t);
            yield return null;
        }
        
        Destroy(dagger);
        isAttacking = false;
    }

    void PerformAirAttack()
    {
        Vector3 spawnPos = transform.position + new Vector3(0, -0.5f, 0); 
        GameObject dagger = Instantiate(daggerPrefab, spawnPos, Quaternion.identity);
        dagger.transform.SetParent(transform); 
        dagger.transform.localScale = new Vector3(0.1f, 1.25f, 1f);
        Destroy(dagger, 0.25f);
    }

    // --- 이동 및 상태 로직 ---
    void StartClimbing()
    {
        isGrounded = false;
        isClimbing = true;
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.up * climbSpeed;
        StartScaleCoroutine(hangScale);
    }

    void DropFromCeiling(bool forced)
    {
        isHanging = false;
        isClimbing = false;
        isForcedFall = forced;
        StartScaleCoroutine(originalScale);

        if (forced) 
        {
            rb.gravityScale = heavyGravity;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }
        else 
        {
            rb.gravityScale = normalGravity;
            rb.linearVelocity = Vector2.down * 1.0f; 
        }
    }

    // --- [중요] 충돌 감지 로직 ---

    // 1. 물리적 충돌 (몸통 박치기 등)
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // [우선순위 1] Game Over 체크 (가장 먼저 연산)
        if (collision.gameObject.CompareTag("Enemy"))
        {
            GameOver();
            return; // 죽었으므로 아래 로직(착지, 매달리기 등) 실행 안 함
        }

        // [우선순위 2] 천장 매달리기
        if (collision.gameObject.CompareTag("Ceiling"))
        {
            if (isClimbing)
            {
                isClimbing = false;
                isHanging = true;
                rb.linearVelocity = Vector2.zero;
                rb.gravityScale = 0f;
            }
        }

        // [우선순위 3] 바닥 착지
        if (collision.gameObject.CompareTag("Ground"))
        {
            if (!isGrounded)
            {
                isGrounded = true;
                isClimbing = false;
                rb.gravityScale = normalGravity;

                if (isForcedFall)
                {
                    CreateLandingNoise(5f);
                    StartCoroutine(StunRoutine());
                }
                else
                {
                    CreateLandingNoise(3f);
                }
                isForcedFall = false;
            }
        }
    }

    // 2. 트리거 충돌 (화살, 가시, 투사체 등)
    // [추가] 물리적 충돌 없이 겹침만 감지하는 경우도 Game Over 처리를 위해 추가
    private void OnTriggerEnter2D(Collider2D collision)
    {
        // [우선순위 1] Game Over 체크
        if (collision.CompareTag("Retry"))
        {
            GameOver();
            return;
        }
    }

    // --- Game Over 처리 함수 ---
    void GameOver()
    {
        if (isDead) return; // 이미 죽었으면 중복 실행 방지

        isDead = true;
        
        // 움직임 정지
        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = normalGravity; // 공중에서 죽으면 떨어지도록
        
        Debug.Log("Game Over!"); // 콘솔 확인용

        // TODO: 나중에 Game Over Scene이 준비되면 주석 해제하여 사용
        // SceneManager.LoadScene("GameOverScene"); 
    }

    // ----------------------------

    void CreateLandingNoise(float widthSize)
    {
        if (noisePrefab == null) return;
        float bottomY = transform.position.y - 0.75f;
        
        Vector3 rightPos = new Vector3(transform.position.x + (widthSize * 0.5f), bottomY + 0.25f, 0);
        GameObject rightNoise = Instantiate(noisePrefab, rightPos, Quaternion.identity);
        rightNoise.transform.localScale = new Vector3(widthSize, 0.5f, 1f);

        Vector3 leftPos = new Vector3(transform.position.x - (widthSize * 0.5f), bottomY + 0.25f, 0);
        GameObject leftNoise = Instantiate(noisePrefab, leftPos, Quaternion.identity);
        leftNoise.transform.localScale = new Vector3(widthSize, 0.5f, 1f);
    }

    void HandleStamina()
    {
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