using UnityEngine;
using System.Collections;

public enum StartDir { Left, Right } //맛도리장도리 시작방향설정 드롭다운 

[RequireComponent(typeof(Rigidbody2D))]
public class Enemy : MonoBehaviour
{
    #region Public Variables (Inspector)
    [Header("Patrol")]
    public float speed = 4f;
    public float patrolDistance = 5f;
    public StartDir startDirection = StartDir.Left; // Inspector용 드롭다운

    [Header("Detection")]
    public float detectRadius = 5f;            // 감지 반경
    public float detectHeight = 0.3f;          // 같은층 판정 범위
    public GameObject alertIndicatorPrefab;    // 발견 시 띄울 느낌표 프리팹.. 을~ 만들어야겠죠? ㅎㅎ
    public bool playerInSight = false;

    [Header("Aim")]
    public float aimDelay = 0.5f; //조준시간
    public float aimStopDistance = 5f;

    [Header("Arrow Shooting")]
    public GameObject arrowPrefab;
    public Vector2 arrowVelocity;
    public float duration;
    public float ArrowLifeTime;

    // 스프라이트 원본 방향이 닌자랑 반대라서 기본값을 true로 고정
    [HideInInspector]
    public bool invertFlipX = true;

    #endregion

    #region Private Variables (Internal)
    // Components
    private Rigidbody2D _rigidbody;
    private Transform _playerTransform;
    private PlayerController _playerController;
    private GameObject _playerGameObject;

    // 스프라이트와 애니메이션
    private Animator _anim;
    private SpriteRenderer _spriteRenderer;

    // 와리가리
    public Vector3 _currentDirection;
    private Vector3 _patrolOrigin;
    private Vector3 _currentTarget;

    // 감지/조준/발사
    private bool _isAlerted = false;
    private bool _isAiming = false;
    public bool _isDead = false;
    private float _aimTimer = 0f;
    private float _shootTimer = 0f;  
    private GameObject _alertInstance;

    // Animation-event helpers
    private bool _pendingShoot = false; // 활쏘기 애니메이션 재생 중인지(입력 잠금용)
    private bool _hasFiredThisShot = false; // 한 번의 Shoot 애니에서 화살 1회만 발사 보장

    // 얼마나 오래 죽을지(마지막 컷)
    public float deathLingerTime = 0.35f;
    private bool _deathDestroyQueued = false;

    // 그뭐냐노이즈감지
    private bool _isInvestigatingNoise = false;
    private Vector3 _savedPatrolOrigin;
    private Vector3 _savedDirection;
    #endregion

    void Start()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        _anim = GetComponent<Animator>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _patrolOrigin = transform.position;
        _currentDirection = (startDirection == StartDir.Left) ? Vector3.left : Vector3.right;
        _playerGameObject = GameObject.FindGameObjectWithTag("Player");
        if (_playerGameObject != null)
        {
            _playerTransform = _playerGameObject.transform;
            _playerController = _playerGameObject.GetComponent<PlayerController>();
        }
        SetNextTarget();
    }

    void Update()
    {
        if (_isDead) return;
        if (_playerTransform == null) return; // 플레이어 없으면 철수철수~

        float dist = Vector2.Distance(new Vector2(_playerTransform.position.x, _playerTransform.position.y), // 플레이어위치벡터
                                      new Vector2(transform.position.x, transform.position.y)); // 적위치벡터

        bool playerHanging = (_playerController != null) && _playerController.isHanging;


        if (_pendingShoot)
        {
            _shootTimer += Time.deltaTime;
            if (_shootTimer >= duration)
            {
                _pendingShoot = false;
                _shootTimer = 0f;
            }
            return; // 쿨다운 중에는 다른 행동 안함
        }

        if (!_isAiming)
        {
            CheckPlayerInSight();

            if (dist <= detectRadius && !playerHanging && playerInSight)
            {   
                // 노이즈 따라가고 있어도 플레이어발견시 취소
                if (_isInvestigatingNoise)
                {
                    _isInvestigatingNoise = false;
                    _patrolOrigin = _savedPatrolOrigin;
                    _currentDirection = _savedDirection;
                    SetNextTarget();
                }

                if (!_isAlerted)
                {
                    _isAlerted = true;
                    ShowAlert();
                }

                if (!_isAiming)
                {
                    _isAiming = true;
                    _aimTimer = 0f;
                    StartShootAnimation();
                    _shootTimer = 0f;
                }
            }
            else
            {
                if (_isAlerted)
                {
                    _isAlerted = false;
                    HideAlert();
                }
            }
        }

        if (_isAiming)
        {
            if (dist > aimStopDistance)
            {
                _isAiming = false;
                _isAlerted = false;
                HideAlert();
            }
            else
            {
                _aimTimer += Time.deltaTime;
                if (_aimTimer >= aimDelay)
                {
                    _isAiming = false;
                }
            }
        }

        UpdateAnimationState();
    }

    void UpdateAnimationState()
    {
        if (_anim == null) return;

        // 1) 이동 여부
        bool isMoving = Mathf.Abs(_rigidbody.linearVelocity.x) > 0.1f;
        _anim.SetBool("IsMoving", isMoving);

        // 2) 죽음
        _anim.SetBool("IsDead", _isDead);

        // 3) 좌우 반전(Flip) 
        if (_spriteRenderer != null)
        {
            float vx = _rigidbody.linearVelocity.x;
            if (vx > 0.1f) SetFlipFacingRight(true);
            else if (vx < -0.1f) SetFlipFacingRight(false);
            else
            {
                // 멈춰있을 땐 마지막 바라보던 방향을 유지
                if (_currentDirection.x > 0.001f) SetFlipFacingRight(true);
                else if (_currentDirection.x < -0.001f) SetFlipFacingRight(false);
            }
        }
    }

    void SetFlipFacingRight(bool facingRight)
    {
        if (_spriteRenderer == null) return;
        // 우리 프로젝트 기준: 기본값은 "오른쪽 바라봄 = flipX true" (PlayerController와 동일)
        // 스프라이트 원본이 반대면 invertFlipX로 뒤집어서 대응
        _spriteRenderer.flipX = invertFlipX ? !facingRight : facingRight;
    }

    void FixedUpdate() 
    {
        if (_isDead)
        {
            _rigidbody.linearVelocity = Vector2.zero;
            return;
        }
        // 조준 또는 발사 상태에서는 이동 멈춤 (수직속도는 유지)
        if (_isAiming || _pendingShoot || _isAlerted)
        {
            _rigidbody.linearVelocity = new Vector2(0f, _rigidbody.linearVelocity.y); // 스답스답
            return; // 이후 패트롤무브로 넘어가지 못하도록
        }

        PatrolMove();
    }

    //---와리가리---
    void PatrolMove() // 움직움직
    {
        float toTargetX = _currentTarget.x - transform.position.x; // x값만 와리가리 왜 x값만이냐면.. 많은 이야기가 있는데요..

        if (Mathf.Abs(toTargetX) < 0.05f) //절댓값기준 목표거리가 0.05보다 작으면~ = 거의 도달했으면~
        {
            _rigidbody.position = new Vector2(_currentTarget.x, _rigidbody.position.y); // 위치보정
            _rigidbody.linearVelocity = new Vector2(0f, _rigidbody.linearVelocity.y); // 스답!

            // 소음 지점 도착후 복귀
            if (_isInvestigatingNoise)
            {
                _isInvestigatingNoise = false;

                _patrolOrigin = _savedPatrolOrigin;
                _currentDirection = _savedDirection;
                SetNextTarget();
                return;
            }

            // 출발점 갱신 및 방향 반전
            _patrolOrigin = _currentTarget;
            _currentDirection = -_currentDirection;
            SetNextTarget();
            return;
        }

        float dirX = Mathf.Sign(toTargetX); // 투타겟엑스 부호, -1 또는 +1
        _rigidbody.linearVelocity = new Vector2(dirX * speed, _rigidbody.linearVelocity.y);
        SetFacingFromDirX(dirX); // 스프라이트 방향 조정(+화살 방향에도 사용)
    }

    void SetNextTarget() // 다음 이동점
    {
        Vector3 target = _patrolOrigin + _currentDirection * Mathf.Max(0.01f, patrolDistance); // 목표 = 출발점 + (방향 * 거리)
        target.y = _patrolOrigin.y; // y좌표 고정
        _currentTarget = target;
    }

    void SetFacingFromDirX(float dirX)
    {
        if (Mathf.Abs(dirX) < 0.0001f) return;
        _currentDirection = (dirX > 0f) ? Vector3.right : Vector3.left;

        // localScale 반전은 콜라이더/자식 오브젝트 스케일에 영향을 줄 수 있어서,
        // PlayerController처럼 SpriteRenderer.flipX만 사용하는 쪽이 보통 더 안전합니다.
        if (_spriteRenderer != null)
        {
            SetFlipFacingRight(dirX > 0f);
        }
    }

    //---감지---

    void ShowAlert()
    {
        if (_alertInstance != null) return;
        GameObject go = new GameObject("AlertText");
        go.transform.SetParent(transform);
        go.transform.localPosition = Vector3.up * 1.5f;
        var tm = go.AddComponent<TextMesh>();
        tm.text = "!";
        tm.characterSize = 0.2f;
        tm.fontSize = 64;
        tm.color = Color.yellow;
        tm.anchor = TextAnchor.MiddleCenter;
        _alertInstance = go;
    }

    void HideAlert()
    {
        if (_alertInstance != null)
        {
            Destroy(_alertInstance);
            _alertInstance = null;
        }
    }

        void CheckPlayerInSight()
    {
        float heightDifference = Mathf.Abs(_playerTransform.position.y - transform.position.y);
        if (heightDifference > detectHeight)
        {
            playerInSight = false;
            return;
        }
        if (_playerTransform.position.x < transform.position.x && _currentDirection == Vector3.left)
        {
            playerInSight = true;
        }
        else if (_playerTransform.position.x > transform.position.x && _currentDirection == Vector3.right)
        {
            playerInSight = true;
        }
        else
        {
            playerInSight = false;
        }
    }
    
    //---화살발사---
    public void FireArrow()
    {
        // Animation Event로 정확히 맞출 경우, 이 가드가 없으면 여러 번 이벤트가 찍혔을 때 다발 발사 가능
        if (_hasFiredThisShot) return;
        _hasFiredThisShot = true;

        if (arrowPrefab == null)
        {
            Debug.LogWarning("arrowPrefab이 설정되지 않았습니다!");
            return;
        }
        
        Debug.Log("화살 발사!");
        
        var arrow = Instantiate(arrowPrefab, transform.position, Quaternion.identity);
        var arrowComponent = arrow.GetComponent<Arrow>();
        
        Vector2 adjustedVelocity = new Vector2(arrowVelocity.x * _currentDirection.x, arrowVelocity.y);
        arrowComponent.velocity = adjustedVelocity;
        arrowComponent.ArrowLifeTime = ArrowLifeTime;
    }

    //---노이즈감지---
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_isDead) return;
        if (_isAiming || _pendingShoot) return;

        if (other.GetComponent<Noise>() == null) return;

        GoToNoise(other.transform.position);
    }

    //---죽음---

    public void Die()
    {
        if (_isDead) return;
        _isDead = true;

        // 진행 중이던 행동 정리
        _isAlerted = false;
        _isAiming = false;
        _pendingShoot = false;
        _hasFiredThisShot = false;
        _isInvestigatingNoise = false;
        HideAlert();

        // 물리 정지
        if (_rigidbody != null)
        {
            _rigidbody.linearVelocity = Vector2.zero;
        }

        // 애니메이션에 죽음 상태 전달
        UpdateAnimationState();

        // 파괴 타이밍은 Die 애니메이션 마지막 프레임에 Animation Event로 맞추는 게 가장 정확합니다.
        // Die 클립 마지막 프레임에 아래 함수(OnDieLastFrameEvent)를 이벤트로 걸어주세요.
        // (원하면 deathDestroyDelay 코루틴 방식도 남겨두었지만, 정밀도가 떨어져요.)
    }

    void StartShootAnimation()
    {
        if (_anim == null) return;
        if (_pendingShoot) return;

        // 공격 시작 시점에 항상 플레이어 방향을 다시 계산
        if (_playerTransform != null)
        {
            float dirX = _playerTransform.position.x - transform.position.x;
            if (Mathf.Abs(dirX) >= 0.001f)
            {
                SetFacingFromDirX(Mathf.Sign(dirX));
            }
        }

        _pendingShoot = true;
        _hasFiredThisShot = false;
        _anim.SetTrigger("Shoot");
    }

    // Animation Event용: Shoot 클립의 "화살 놓는 프레임"에 이 함수를 호출
    public void OnShootFireEvent()
    {
        if (_isDead) return;
        if (!_pendingShoot) return;

        // Fire 프레임에서도 한번 더 방향 보정
        if (_playerTransform != null)
        {
            float dirX = _playerTransform.position.x - transform.position.x;
            if (Mathf.Abs(dirX) >= 0.001f)
            {
                SetFacingFromDirX(Mathf.Sign(dirX));
            }
        }
        FireArrow();
    }

    // Animation Event용: Die 클립의 "마지막 프레임"에 이 함수를 호출하세요.
    // 마지막 프레임(쓰러진 모습)에서 deathLingerTime만큼 멈춘 뒤 파괴합니다.
    public void OnDieLastFrameEvent()
    {
        if (_deathDestroyQueued) return;
        _deathDestroyQueued = true;
        StartCoroutine(DeathLingerAndDestroyRoutine());
    }

    IEnumerator DeathLingerAndDestroyRoutine()
    {
        if (deathLingerTime > 0f)
        {
            yield return new WaitForSeconds(deathLingerTime);
        }
        Destroy(gameObject);
    }

    void GoToNoise(Vector3 noisePos)
    {
        // 현재 패트롤값 저장
        if (!_isInvestigatingNoise)
        {
            _savedPatrolOrigin = _patrolOrigin;
            _savedDirection = _currentDirection;
        }

        _isInvestigatingNoise = true;

        // 방향 휙휙
        float dirX = noisePos.x - transform.position.x;
        if (Mathf.Abs(dirX) > 0.001f)
        {
            _currentDirection = (dirX > 0f) ? Vector3.right : Vector3.left;
            // 방향은 SpriteRenderer.flipX로 처리하고, 화살 방향은 _currentDirection을 사용
            SetFlipFacingRight(dirX > 0f);
        }

        _currentTarget = new Vector3(noisePos.x, transform.position.y, transform.position.z);

        _patrolOrigin = transform.position;
    }
}
