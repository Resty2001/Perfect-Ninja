using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement; // 씬 전환을 위해 필요

public class PlayerController : MonoBehaviour
{
    [Header("Settings")]
    public float groundSpeed = 5f;
    public float hangSpeed = 3f;
    public float climbSpeed = 8f;

    [Header("Stamina Settings")]
    public float maxStamina = 4f;
    public float staminaRecoveryRate = 0.5f;
    public float staminaDrainRate = 1f;

    [Header("Gravity Settings")]
    public float normalGravity = 2f;
    public float heavyGravity = 3f;

    [Header("Prefabs")]
    public GameObject noisePrefab; 
    public GameObject daggerPrefab;
    public GameObject stunGaugePrefab;

    [Header("State Flags")]
    public bool isGrounded;
    public bool isHanging;
    public bool isClimbing; 
    public bool isLadderClimbing; 
    public bool isForcedFall;
    public bool isStunned;
    public bool isAttacking;
    public bool isDead;
    public bool isAirAttacking;

    public float currentStamina;

    // [삭제] Visuals 관련 변수들 (scale 관련) 삭제함
    // 픽셀 아트 애니메이션을 위해 강제 크기 조절 로직을 제거했습니다.
    // public float motionDuration = 0.5f;
    // private Vector3 originalScale = new Vector3(1f, 1.5f, 1f);
    // private Vector3 hangScale = new Vector3(1.5f, 0.5f, 1f);
    
    private int facingDirection = 1; 

    private Rigidbody2D rb;
    // [삭제] Scale 관련 코루틴 변수 삭제
    // private Coroutine scaleCoroutine;
    private Transform nearbyLadder;

    private int playerLayer;
    private int groundLayer;
    private int firstGroundLayer;

    // 착지 소음 방지용 플래그
    private bool skipLandingNoise = false; 
    private Animator anim; // 애니메이터 참조 변수
    private SpriteRenderer spriteRenderer; // 좌우 반전을 위해 필요

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>(); 
        spriteRenderer = GetComponent<SpriteRenderer>(); 

        currentStamina = maxStamina;
        
        // [수정] 스케일을 강제로 (1, 1, 1)로 고정합니다.
        // 원본 픽셀 아트의 비율을 유지하기 위함입니다.
        transform.localScale = Vector3.one; 

        isGrounded = true;
        isHanging = false;
        isClimbing = false;
        isLadderClimbing = false;
        isForcedFall = false;
        isAttacking = false;
        isDead = false;

        playerLayer = LayerMask.NameToLayer("Player");
        groundLayer = LayerMask.NameToLayer("Ground");
        firstGroundLayer = LayerMask.NameToLayer("1st Floor");
    }

    void Update()
    {
        if (isStunned || isDead) return;

        HandleInput();
        HandleStamina();
        UpdateAnimationState();
    }

    void FixedUpdate()
    {
        if (isStunned || isDead) return;
        Move();
    }

    void Move()
    {
        // 1. 사다리 이동 로직 (가장 우선)
        if (isLadderClimbing)
        {
            HandleLadderMovement();
            return;
        }

        // 2. 다른 상태 체크
        if (isClimbing || isAttacking) return;

        if (!isGrounded && !isHanging)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        // 3. 일반 이동
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

    // --- 사다리 위에서의 이동 로직 ---
    void HandleLadderMovement()
    {
        float yInput = Input.GetAxisRaw("Vertical"); // W, S
        float xInput = Input.GetAxisRaw("Horizontal"); // A, D

        // 1. 바닥에 닿아있는지 확인 (사다리 타는 중엔 충돌을 무시하므로 Raycast 사용)
        bool isTouchingGround = CheckGroundRaycast();

        // 2. 바닥에 닿아있고, 좌우 입력이 있다면 -> 사다리 탈출 (걷기 시작)
        if (isTouchingGround && xInput != 0)
        {
            StopLadderClimbing();
            isGrounded = true;
            // 즉시 걷기 속도 적용 (부드러운 전환)
            rb.linearVelocity = new Vector2(xInput * groundSpeed, rb.linearVelocity.y);
            return;
        }

        // 3. 바닥이 아니거나 좌우 입력이 없으면 -> Y축 이동만 허용, X축 고정
        rb.linearVelocity = new Vector2(0f, yInput * climbSpeed);
    }

    // 바닥 감지용 레이캐스트 (충돌 무시 상태에서도 바닥 감지)
    bool CheckGroundRaycast()
    {
        Vector2 rayOrigin = new Vector2(transform.position.x, transform.position.y - 0.75f);
        
        // Ground 레이어 이거나 1st Floor 레이어인 것
        int combinedLayerMask = (1 << groundLayer) | (1 << firstGroundLayer);

        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, 0.1f, combinedLayerMask);
        
        return hit.collider != null;
    }

    void UpdateAnimationState()
    {
        if (anim == null) return;

        // 1. 이동 (Running) 상태 전송
        bool isMoving = Mathf.Abs(rb.linearVelocity.x) > 0.1f;
        anim.SetBool("IsRunning", isMoving);

        // 2. 바닥/매달리기 상태 전송
        anim.SetBool("IsGrounded", isGrounded);
        anim.SetBool("IsHanging", isHanging);

        // [추가] 3. 사다리 타기 상태 연결
        // 스크립트의 isLadderClimbing 변수 값을 애니메이터의 "IsClimbing" 파라미터에 넣습니다.
        anim.SetBool("IsClimbing", isLadderClimbing); 

        // 4. 캐릭터 좌우 반전 (Flip)
        // 사다리 타는 중에는 방향 전환 안 함
        if (!isLadderClimbing) 
        {
            if (rb.linearVelocity.x > 0.1f)
            {
                spriteRenderer.flipX = true; // (이미지 원본 방향에 따라 수정 필요)
            }
            else if (rb.linearVelocity.x < -0.1f)
            {
                spriteRenderer.flipX = false;
            }
        }
    }

    void HandleInput()
    {
        // 사다리 타기 시작 조건
        if (nearbyLadder != null && !isLadderClimbing && isGrounded && !isAttacking && !isStunned)
        {
            if (Mathf.Abs(transform.position.x - nearbyLadder.position.x) <= 0.2f)
            {
                float yInput = Input.GetAxisRaw("Vertical");
                if (yInput != 0) 
                {
                    StartLadderClimbing();
                }
            }
        }

        // 공격
        if (Input.GetKeyDown(KeyCode.Space) && !isAttacking && !isLadderClimbing) 
        {
            if (isGrounded)
            {
                StartCoroutine(ProcessGroundAttack());
            }
            else if (!isGrounded && !isHanging && !isClimbing && !isAirAttacking)
            {
                PerformAirAttack();
            }
        }

        // E키 상호작용
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (isLadderClimbing) return; // 사다리 중엔 E키 무시

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

    // --- 사다리 상태 관리 ---

    void StartLadderClimbing()
    {
        isLadderClimbing = true;
        isGrounded = false; // 타는 순간 바닥 판정 끔
        rb.gravityScale = 0f; 
        rb.linearVelocity = Vector2.zero;

        // X축 위치 보정 (중앙 정렬)
        transform.position = new Vector3(nearbyLadder.position.x, transform.position.y, transform.position.z);
        
        // 땅/천장과 충돌 무시 (뚫고 지나가기 위해)
        Physics2D.IgnoreLayerCollision(playerLayer, groundLayer, true);
    }

    void StopLadderClimbing()
    {
        isLadderClimbing = false;
        rb.gravityScale = normalGravity;
        rb.linearVelocity = Vector2.zero;

        // 사다리에서 내릴 때 (걷기로 전환 시) 착지 소음 방지
        skipLandingNoise = true;
        isForcedFall = false;

        // 충돌 다시 활성화
        Physics2D.IgnoreLayerCollision(playerLayer, groundLayer, false);
    }

    // --- 기타 기능 (공격, 소음 등) ---

    IEnumerator ProcessGroundAttack()
    {
        isAttacking = true;
        rb.linearVelocity = Vector2.zero;
        anim.SetTrigger("Attack");
        GameObject dagger = Instantiate(daggerPrefab, transform.position, Quaternion.identity);
        dagger.transform.SetParent(transform); 
        dagger.transform.localScale = new Vector3(1f, 0.2f, 1f); 
        Vector3 startPos = Vector3.zero;
        Vector3 endPos = new Vector3(facingDirection * 1.0f, 0, 0); 
        float attackHalfDuration = 0.25f;
        float elapsed = 0f;
        while (elapsed < attackHalfDuration) { elapsed += Time.deltaTime; float t = elapsed / attackHalfDuration; dagger.transform.localPosition = Vector3.Lerp(startPos, endPos, t); yield return null; }
        dagger.transform.localPosition = endPos;
        elapsed = 0f;
        while (elapsed < attackHalfDuration) { elapsed += Time.deltaTime; float t = elapsed / attackHalfDuration; dagger.transform.localPosition = Vector3.Lerp(endPos, startPos, t); yield return null; }
        Destroy(dagger);
        isAttacking = false;
    }

    void PerformAirAttack()
    {
        isAirAttacking = true;
        anim.SetTrigger("Attack");
        Vector3 spawnPos = transform.position + new Vector3(0, -0.5f, 0); 
        GameObject dagger = Instantiate(daggerPrefab, spawnPos, Quaternion.identity);
        dagger.transform.SetParent(transform); 
        dagger.transform.localScale = new Vector3(0.1f, 1.25f, 1f);
        Destroy(dagger, 0.25f);
    }

    void StartClimbing()
    {
        isGrounded = false;
        isClimbing = true;
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.up * climbSpeed;
        
        // [삭제] Scale 코루틴 삭제
        // StartScaleCoroutine(hangScale);
    }

    void DropFromCeiling(bool forced)
    {
        isHanging = false;
        isClimbing = false;
        isForcedFall = forced;
        
        // [삭제] Scale 코루틴 삭제
        // StartScaleCoroutine(originalScale);
        
        if (forced) { rb.gravityScale = heavyGravity; rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y); }
        else { rb.gravityScale = normalGravity; rb.linearVelocity = Vector2.down * 1.0f; }
    }

    // --- 충돌 감지 ---

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Enemy"))
        {
            Enemy enemy = collision.gameObject.GetComponent<Enemy>();

            if (enemy != null && !enemy._isDead)
            {
                SceneManager.LoadScene("Scenes/GameOver");
            }
        }

        if (collision.gameObject.CompareTag("Ceiling"))
        {
            if (isLadderClimbing) return; // 사다리 중엔 천장 무시

            if (isClimbing)
            {
                isClimbing = false;
                isHanging = true;
                rb.linearVelocity = Vector2.zero;
                rb.gravityScale = 0f;
            }
        }

        if (collision.gameObject.CompareTag("Ground"))
        {
            if (isLadderClimbing) return;

            if (!isGrounded)
            {
                isGrounded = true;
                isClimbing = false;
                rb.gravityScale = normalGravity;

                if (skipLandingNoise)
                {
                    skipLandingNoise = false; // 소음 없이 착지 처리만 함
                }
                else
                { 
                    // 규리: 수치 조절 부분 (그대로 유지)
                    if (isForcedFall) { CreateLandingNoise(7f); StartCoroutine(StunRoutine()); }
                    else { CreateLandingNoise(3f); }
                }
                isForcedFall = false;
                isAirAttacking = false;
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Retry"))
        {
            SceneManager.LoadScene("Scenes/GameOver");
        }
        if (collision.CompareTag("Ladder")) { nearbyLadder = collision.transform; }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Ladder"))
        {
            nearbyLadder = null;
            if (isLadderClimbing) StopLadderClimbing();
        }
    }

    void GameOver() { if (isDead) return; isDead = true; rb.linearVelocity = Vector2.zero; rb.gravityScale = normalGravity; Debug.Log("Game Over!"); }
    
    void CreateLandingNoise(float widthSize)
    {
        if (noisePrefab == null) return;
        float bottomY = transform.position.y - 0.75f;
        Vector3 rightPos = new Vector3(transform.position.x + (widthSize * 0.5f), bottomY + 0.25f, 0);
        Instantiate(noisePrefab, rightPos, Quaternion.identity).transform.localScale = new Vector3(widthSize, 0.5f, 1f);
        Vector3 leftPos = new Vector3(transform.position.x - (widthSize * 0.5f), bottomY + 0.25f, 0);
        Instantiate(noisePrefab, leftPos, Quaternion.identity).transform.localScale = new Vector3(widthSize, 0.5f, 1f);
    }

    void HandleStamina()
    {
        if (isGrounded && currentStamina < maxStamina) currentStamina += staminaRecoveryRate * Time.deltaTime;
        currentStamina = Mathf.Clamp(currentStamina, 0, maxStamina);
        if (isHanging && currentStamina <= 0) DropFromCeiling(true);
    }

    IEnumerator StunRoutine()
    {
        isStunned = true; rb.linearVelocity = Vector2.zero;
        if (stunGaugePrefab != null)
        {
            Vector3 gaugePos = transform.position + new Vector3(0, 1.2f, 0); 
            
            GameObject gaugeObj = Instantiate(stunGaugePrefab, gaugePos, Quaternion.identity);
            
            gaugeObj.transform.SetParent(transform); 

            StunGauge gaugeScript = gaugeObj.GetComponent<StunGauge>();
            if (gaugeScript != null)
            {
                gaugeScript.Setup(1.0f); 
            }
        }

        yield return new WaitForSeconds(1.0f);
        
        isStunned = false;
    }

    // [삭제] 크기 변형 관련 함수들 (StartScaleCoroutine, ChangeScaleProcess) 모두 삭제함
}