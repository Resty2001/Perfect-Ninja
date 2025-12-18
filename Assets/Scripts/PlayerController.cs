using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

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

    [Header("Visuals")]
    public float motionDuration = 0.5f;
    private Vector3 originalScale = new Vector3(1f, 1.5f, 1f);
    private Vector3 hangScale = new Vector3(1.5f, 0.5f, 1f);
    
    private int facingDirection = 1; 

    private Rigidbody2D rb;
    private Coroutine scaleCoroutine;
    private Transform nearbyLadder;

    private int playerLayer;
    private int groundLayer;
    private int firstGroundLayer;

    // 착지 소음 방지용 플래그
    private bool skipLandingNoise = false; 
    private Animator anim; // [추가] 애니메이터 참조 변수
    private SpriteRenderer spriteRenderer; // [추가] 좌우 반전을 위해 필요

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>(); // [추가] 컴포넌트 가져오기
        spriteRenderer = GetComponent<SpriteRenderer>(); // [추가]

        currentStamina = maxStamina;
        transform.localScale = originalScale;

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

    // --- [핵심 수정] 사다리 위에서의 이동 로직 ---
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
        
        // 비트 연산자(|)를 사용하여 두 레이어를 합친 마스크를 만듭니다.
        // 이것은 "Ground 레이어 이거나 1st Floor 레이어인 것"을 의미합니다.
        int combinedLayerMask = (1 << groundLayer) | (1 << firstGroundLayer);

        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, 0.1f, combinedLayerMask);
        
        return hit.collider != null;
    }

    void UpdateAnimationState()
    {
        if (anim == null) return;

        // 1. 이동 (Running) 상태 전송
        // 좌우 입력이 있거나, 실제 속도가 있을 때
        bool isMoving = Mathf.Abs(rb.linearVelocity.x) > 0.1f;
        anim.SetBool("IsRunning", isMoving);

        // 2. 바닥/매달리기 상태 전송
        anim.SetBool("IsGrounded", isGrounded);
        anim.SetBool("IsHanging", isHanging);

        // 3. 캐릭터 좌우 반전 (Flip)
        // 사다리 타는 중에는 방향 전환 안 함 (선택 사항)
        if (!isLadderClimbing) 
    {
        // 오른쪽으로 이동 중일 때 (Velocity X > 0)
        if (rb.linearVelocity.x > 0.1f)
        {
            // [수정] 원본 이미지가 '왼쪽'을 보고 있다면 true여야 오른쪽을 봅니다.
            // (원본이 오른쪽을 보고 있다면 false가 맞습니다. 반대로 넣어보세요.)
            spriteRenderer.flipX = true; 
        }
        // 왼쪽으로 이동 중일 때 (Velocity X < 0)
        else if (rb.linearVelocity.x < -0.1f)
        {
            spriteRenderer.flipX = false;
        }
    }
    }

    void HandleInput()
    {
        // [조건 수정] 사다리 타기 시작 조건:
        // 1. 사다리 근처
        // 2. 바닥에 있어야 함 (isGrounded) -> 천장 매달리기 상태 불가능
        // 3. 오차 범위 내
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
        isGrounded = false; // 타는 순간 바닥 판정 끔 (로직상)
        rb.gravityScale = 0f; 
        rb.linearVelocity = Vector2.zero;

        // X축 위치 보정 (중앙 정렬)
        transform.position = new Vector3(nearbyLadder.position.x, transform.position.y, transform.position.z);
        
        // [핵심] 땅/천장과 충돌 무시 (뚫고 지나가기 위해)
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
        StartScaleCoroutine(hangScale);
    }

    void DropFromCeiling(bool forced)
    {
        isHanging = false;
        isClimbing = false;
        isForcedFall = forced;
        StartScaleCoroutine(originalScale);
        if (forced) { rb.gravityScale = heavyGravity; rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y); }
        else { rb.gravityScale = normalGravity; rb.linearVelocity = Vector2.down * 1.0f; }
    }

    // --- 충돌 감지 ---

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Enemy"))
        {
            SceneManager.LoadScene("Scenes/GameOver");
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
            // [주의] 사다리 타는 중에도 물리 충돌을 '잠깐' 켜거나 
            // StopLadderClimbing 직후에 여기로 들어올 수 있음
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
                { //규리: 여기 수치 조절 해보고 있는데 문제 있다면 원래대로 해두셔도 됩니다
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
            // 사다리 꼭대기를 벗어나면 자동 종료
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
            // 플레이어 머리 위쪽 위치 계산 (1.5는 플레이어 키, +0.5 여유)
            Vector3 gaugePos = transform.position + new Vector3(0, 1.2f, 0); 
            
            GameObject gaugeObj = Instantiate(stunGaugePrefab, gaugePos, Quaternion.identity);
            
            gaugeObj.transform.SetParent(transform); 

            // 스크립트 가져와서 시간 설정 (여기선 1초)
            StunGauge gaugeScript = gaugeObj.GetComponent<StunGauge>();
            if (gaugeScript != null)
            {
                gaugeScript.Setup(1.0f); // 기절 시간 1초 전달
            }
        }

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
        while (elapsed < motionDuration) { elapsed += Time.deltaTime; float t = elapsed / motionDuration; transform.localScale = Vector3.Lerp(startScale, targetScale, t); yield return null; }
        transform.localScale = targetScale;
    }
}

