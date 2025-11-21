using UnityEngine;

public enum StartDir { Left, Right } //맛도리장도리 시작방향설정 드롭다운 

[RequireComponent(typeof(Rigidbody2D))]
public class Enemy : MonoBehaviour
{
    [Header("Patrol")]
    public float speed = 4f;
    public float patrolDistance = 5f;
    public StartDir startDirection = StartDir.Left; // Inspector용 드롭다운

    [Header("Detection")]
    public float detectRadius = 5f;            
    public float detectHeight = 0.5f;          // 같은 층 판정을 위한 높이 허용 오차
    public GameObject alertIndicatorPrefab;    
    public bool playerInSight = false;

    [Header("Aim")]
    public float aimDelay = 0.5f; //조준시간
    public float aimStopDistance = 5f;

    private Rigidbody2D _rigidbody;
    private Vector3 currentDirection;
    private Vector3 patrolOrigin;
    private Vector3 currentTarget;
    private GameObject playerGameObject;
    private Transform playerTransform;         
    private PlayerController playerController;  
    private bool isAlerted = false;
    private bool isAiming = false;
    private float aimTimer = 0f;
    private bool isShooting = false;
    private GameObject alertInstance;

    void Start()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        patrolOrigin = transform.position;
        currentDirection = (startDirection == StartDir.Left) ? Vector3.left : Vector3.right;

        // 한 줄로 합침: playerGameObject에 바로 찾은 결과를 넣음
        playerGameObject = GameObject.FindGameObjectWithTag("Player");
        if (playerGameObject != null)
        {
            playerTransform = playerGameObject.transform;
            playerController = playerGameObject.GetComponent<PlayerController>();
        }

        SetNextTarget();
    }

    void Update()
    {
        if (playerTransform == null) return; // 플레이어 없으면 철수철수~

        float dist = Vector2.Distance(new Vector2(playerTransform.position.x, playerTransform.position.y), // 플레이어위치벡터
                                      new Vector2(transform.position.x, transform.position.y)); // 적위치벡터

        bool playerHanging = (playerController != null) && playerController.isHanging; //대롱대롱?

        if (!isAiming && !isShooting)
        {
            CheckPlayerInSight();

            if (dist <= detectRadius && !playerHanging && playerInSight)
            {   
                if (!isAlerted)
                {
                    isAlerted = true;
                    ShowAlert();
                }

                if (!isAiming)
                {
                    isAiming = true;
                    aimTimer = 0f;
                }
            }
            else
            {
                if (isAlerted)
                {
                    isAlerted = false;
                    HideAlert();
                }
            }
        }

        if (isAiming)
        {
            if (dist > aimStopDistance)
            {
                isAiming = false;
                isAlerted = false;
                HideAlert();
            }
            else
            {
                aimTimer += Time.deltaTime;
                if (aimTimer >= aimDelay)
                {
                    isAiming = false;
                    isShooting = true;
                    StartShooting();
                }
            }
        }
    }

    void FixedUpdate() //물리엔진 업데이트는 이렇게 생겼대요
    {
        // 조준 또는 발사 상태에서는 이동 멈춤 (수직속도는 유지)
        if (isAiming || isShooting || isAlerted)
        {
            _rigidbody.linearVelocity = new Vector2(0f, _rigidbody.linearVelocity.y); // 스답스답
            return; // 이후 패트롤무브로 넘어가지 못하도록
        }

        PatrolMove();
    }

    void PatrolMove() // 움직움직
    {
        float toTargetX = currentTarget.x - transform.position.x; // x값만 와리가리 왜 x값만이냐면.. 많은 이야기가 있는데요..

        if (Mathf.Abs(toTargetX) < 0.05f) //절댓값기준 목표거리가 0.05보다 작으면~ = 거의 도달했으면~
        {
            _rigidbody.position = new Vector2(currentTarget.x, _rigidbody.position.y); // 위치보정
            _rigidbody.linearVelocity = new Vector2(0f, _rigidbody.linearVelocity.y); // 스답!

            // 출발점 갱신 및 방향 반전
            patrolOrigin = currentTarget;
            currentDirection = -currentDirection;
            SetNextTarget();
            return;
        }

        float dirX = Mathf.Sign(toTargetX); // 투타겟엑스 부호, -1 또는 +1
        _rigidbody.linearVelocity = new Vector2(dirX * speed, _rigidbody.linearVelocity.y);
        FaceDirection(new Vector3(dirX, 0f, 0f)); // 스프라이트 방향 조정
    }

    void SetNextTarget() // 다음 이동점
    {
        Vector3 target = patrolOrigin + currentDirection * Mathf.Max(0.01f, patrolDistance); // 목표 = 출발점 + (방향 * 거리)
        target.y = patrolOrigin.y; // y좌표 고정
        currentTarget = target;
    }

    void FaceDirection(Vector3 dir) // 스프라이트 생겼을 때의 얘기지만요~
    {
        if (dir.sqrMagnitude < 0.0001f) return;
        Vector3 s = transform.localScale;
        if (dir.x > 0) transform.localScale = new Vector3(Mathf.Abs(s.x), s.y, s.z);
        else if (dir.x < 0) transform.localScale = new Vector3(-Mathf.Abs(s.x), s.y, s.z);
    }

    void ShowAlert()
    {
        if (alertInstance != null) return;
        // TextMesh로 '!' 생성
        GameObject go = new GameObject("AlertText");
        go.transform.SetParent(transform);
        go.transform.localPosition = Vector3.up * 1.5f;
        var tm = go.AddComponent<TextMesh>();
        tm.text = "!";
        tm.characterSize = 0.2f;
        tm.fontSize = 64;
        tm.color = Color.yellow;
        tm.anchor = TextAnchor.MiddleCenter;
        alertInstance = go;
    }

    void HideAlert()
    {
        if (alertInstance != null)
        {
            Destroy(alertInstance);
            alertInstance = null;
        }
    }

    // 발사 시작(여기서는 스텁: 실제 발사 구현은 이후에 연결)
    void StartShooting()
    {
        // isShooting 플래그를 사용해 FixedUpdate/다른 로직에서 발사 동작 구현
        // 예: Instantiate 총알, 쿨다운 등. 현재는 로그용으로만 둠.
        Debug.Log($"{name}: StartShooting() called");
        // TODO: 실제 발사 구현 추가
    }

    void CheckPlayerInSight()
    {
        // 높이 차이 확인 - 같은 층에 있는지 체크
        float heightDifference = Mathf.Abs(playerTransform.position.y - transform.position.y);
        if (heightDifference > detectHeight)
        {
            playerInSight = false;
            return;
        }

        // 기존 방향 체크
        if (playerTransform.position.x < transform.position.x && currentDirection == Vector3.left)
        {
            playerInSight = true;
        }
        else if (playerTransform.position.x > transform.position.x && currentDirection == Vector3.right)
        {
            playerInSight = true;
        }
        else
        {
            playerInSight = false;
        }
    }
}
