using UnityEngine;
using System.Collections;

public enum StartDir { Left, Right } //맛도리장도리 시작방향설정 드롭다운 

[RequireComponent(typeof(Rigidbody2D))]
public class Enemy : MonoBehaviour
{
    #region Public Variables (Inspector)
    [Header("Patrol")]
    public float speed = 2f;
    public float patrolDistance = 7f;
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

    #endregion

    #region Private Variables (Internal)
    // Components
    private Rigidbody2D _rigidbody;
    private Transform _playerTransform;
    private PlayerController _playerController;
    private GameObject _playerGameObject;

    // 와리가리
    private Vector3 _currentDirection;
    private Vector3 _patrolOrigin;
    private Vector3 _currentTarget;

    // 감지/조준/발사
    private bool _isAlerted = false;
    private bool _isAiming = false;
    private bool _isShooting = false;
    private float _aimTimer = 0f;
    private float _shootTimer = 0f;  
    private GameObject _alertInstance;

    // 그뭐냐노이즈감지
    private bool _isInvestigatingNoise = false;
    private Vector3 _savedPatrolOrigin;
    private Vector3 _savedDirection;
    #endregion

    void Start()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
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
        if (_playerTransform == null) return; // 플레이어 없으면 철수철수~

        float dist = Vector2.Distance(new Vector2(_playerTransform.position.x, _playerTransform.position.y), // 플레이어위치벡터
                                      new Vector2(transform.position.x, transform.position.y)); // 적위치벡터

        bool playerHanging = (_playerController != null) && _playerController.isHanging;


        if (_isShooting)
        {
            _shootTimer += Time.deltaTime;
            if (_shootTimer >= duration)
            {
                _isShooting = false;
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
                    _isShooting = true;
                    _shootTimer = 0f;
                    
                    FireArrow();
                }
            }
        }
    }

    void FixedUpdate() 
    {
        // 조준 또는 발사 상태에서는 이동 멈춤 (수직속도는 유지)
        if (_isAiming || _isShooting || _isAlerted)
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
        FaceDirection(new Vector3(dirX, 0f, 0f)); // 스프라이트 방향 조정
    }

    void SetNextTarget() // 다음 이동점
    {
        Vector3 target = _patrolOrigin + _currentDirection * Mathf.Max(0.01f, patrolDistance); // 목표 = 출발점 + (방향 * 거리)
        target.y = _patrolOrigin.y; // y좌표 고정
        _currentTarget = target;
    }

    void FaceDirection(Vector3 dir) // 스프라이트 생겼을 때의 얘기지만요~
    {
        if (dir.sqrMagnitude < 0.0001f) return;
        Vector3 s = transform.localScale;
        if (dir.x > 0) transform.localScale = new Vector3(Mathf.Abs(s.x), s.y, s.z);
        else if (dir.x < 0) transform.localScale = new Vector3(-Mathf.Abs(s.x), s.y, s.z);
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
    void FireArrow()
    {
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
        if (_isAiming || _isShooting) return;

        if (other.GetComponent<Noise>() == null) return;

        GoToNoise(other.transform.position);
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
            FaceDirection(_currentDirection);
        }

        _currentTarget = new Vector3(noisePos.x, transform.position.y, transform.position.z);

        _patrolOrigin = transform.position;
    }
}
