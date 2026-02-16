using System.Collections;
using UnityEngine;
using Game.Data;

/// <summary>
/// 쿠키런 스타일의 플레이어 컨트롤러
/// - 커스텀 중력 시스템
/// - 상태 패턴 기반 제어
/// - Rigidbody2D velocity 직접 제어
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class PlayerController : MonoBehaviour
{
    #region State Definition
    public enum PlayerState
    {
        Run,
        Jump,
        DoubleJump,
        Slide,
        Hit,
        Die
    }
    #endregion

    #region Components
    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;
    private Animator animator;
    #endregion

    #region State Variables
    [Header("=== State ===")]
    [SerializeField] private PlayerState currentState = PlayerState.Run;
    private int jumpCount = 0; // 현재 점프 횟수
    private int maxJumpCount = 2; // CSV에서 로드할 최대 점프 횟수
    
    [Header("=== Data ===")]
    public int currentLevel = 1; // 인스펙터에서 레벨 테스트 가능
    private float magnetRange = 0f; // CSV에서 로드할 자력 범위
    private bool isInitialized = false; // 초기화 완료 여부
    #endregion

    #region HP Variables
    [Header("=== HP System ===")]
    public float maxHp = 100f; // 최대 체력
    private float currentHp; // 현재 체력
    [SerializeField] private float hpDrainRate = 5f; // 초당 체력 감소량
    #endregion

    #region Movement Variables
    [Header("=== Movement ===")]
    [SerializeField] private float runSpeed = 8f;
    [SerializeField] private float jumpForce1 = 15f;  // 첫 번째 점프력
    [SerializeField] private float jumpForce2 = 13f;  // 더블 점프력
    [SerializeField] private float gravity = 30f;     // 커스텀 중력 강도
    [SerializeField] private float maxFallSpeed = 20f; // 최대 낙하 속도

    private Vector2 velocity;
    #endregion

    #region Slide Variables
    [Header("=== Slide ===")]
    [SerializeField] private float slideDuration = 0.5f;    // 슬라이드 지속시간
    [SerializeField] private float slideSizeRatio = 0.5f;   // 슬라이드 시 높이 비율
    private float slideTimer = 0f;
    private Vector2 originalColliderSize;
    private Vector2 originalColliderOffset;
    #endregion

    #region Ground Check Variables
    [Header("=== Ground Check ===")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckDistance = 0.2f;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.8f, 0.1f);
    [SerializeField] private bool enableGroundSnap = true; // 땅 위치 보정 활성화
    private bool isGrounded = false;
    private RaycastHit2D groundHit; // BoxCast 결과 저장
    #endregion

    #region Hit & Die Variables
    [Header("=== Hit & Die ===")]
    [SerializeField] private float hitKnockbackForce = 5f;
    [SerializeField] private float hitDuration = 0.5f;
    [SerializeField] private float dieFallSpeed = 10f;
    private float hitTimer = 0f;
    #endregion

    #region Magnet Variables
    [Header("=== Magnet ===")]
    [SerializeField] private float magnetSpeed = 12f; // 젤리가 끌려오는 속도
    [SerializeField] private LayerMask itemLayer;      // 아이템 레이어
    
    // 성능 최적화: NonAlloc 배열 캐싱 및 스캔 주기 제한
    private readonly Collider2D[] magnetHits = new Collider2D[64];
    private float magnetScanTimer = 0f;
    private const float magnetScanInterval = 0.05f; // 0.05초(50ms)마다 스캔
    
    #if DEVELOPMENT_BUILD || UNITY_EDITOR
    private bool hasWarnedMagnetBufferFull = false;
    #endif
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();
        animator = GetComponentInChildren<Animator>();

        // Rigidbody2D 설정: 중력 비활성화 (커스텀 중력 사용)
        rb.gravityScale = 0f;
        
        // [수정] X축 위치와 Z축 회전을 모두 고정 (플레이어는 절대 좌우로 밀리지 않음)
        rb.constraints = RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePositionX;

        // 원본 콜라이더 크기 저장
        originalColliderSize = boxCollider.size;
        originalColliderOffset = boxCollider.offset;
    }

    private void Start()
    {
        // [중요] 체력은 기다리지 말고 즉시 초기화해야 Update에서 죽지 않음
        currentHp = maxHp;
        
        StartCoroutine(InitializePlayer());
    }

    /// <summary>
    /// 플레이어 초기화 (매니저가 준비된 후 실행)
    /// Fail Fast 정책: 필수 의존성 누락 시 컴포넌트를 완전히 무력화합니다.
    /// </summary>
    private IEnumerator InitializePlayer()
    {
        // 한 프레임 대기 (모든 Start()가 완료될 때까지)
        yield return null;

        // Fail Fast: DataManager 필수 의존성 체크
        var dm = DataManager.Instance;
        if (dm == null || !dm.IsLoaded)
        {
            DisablePlayerHard($"[PlayerController] DataManager 로드 실패: {dm?.LastErrorContext ?? "DataManager가 씬에 존재하지 않습니다."}");
            yield break;
        }

        // 현재 레벨의 스탯 데이터 가져오기
        CharacterStat stat = dm.GetStat(currentLevel);

        // Fail Fast: 데이터 유효성 체크 (Level이 0이면 기본값 = 데이터 없음)
        if (stat.Level == 0)
        {
            DisablePlayerHard($"[PlayerController] 레벨 {currentLevel}에 해당하는 데이터가 없습니다!");
            yield break;
        }

        // 스탯 데이터 적용
        maxHp = stat.Hp;
        currentHp = maxHp;
        magnetRange = stat.MagnetRange;
        maxJumpCount = stat.MaxJumpCount;

        Debug.Log($"[PlayerController] 레벨 {stat.Level} 데이터 적용 완료: 체력 {stat.Hp}, 자력 범위 {stat.MagnetRange}, 최대 점프 횟수 {stat.MaxJumpCount}");

        // UI 강제 초기화 (체력바를 최신 maxHp 기준으로 동기화)
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateHp((int)currentHp, (int)maxHp);
            UIManager.Instance.UpdateHpRatio(1f); // 100% 상태로 강제 설정
            Debug.Log($"[PlayerController] 체력 UI 초기화 완료: {currentHp}/{maxHp} (100%)");
        }
        else
        {
            Debug.LogWarning("[PlayerController] UIManager를 찾을 수 없습니다. (UI는 업데이트되지 않지만 게임은 계속 진행됩니다)");
        }

        // 초기화 완료
        isInitialized = true;
    }

    /// <summary>
    /// Fail Fast: 플레이어를 완전히 무력화합니다.
    /// </summary>
    private void DisablePlayerHard(string reason)
    {
        Debug.LogError(reason);
        enabled = false;
        rb.simulated = false;
        boxCollider.enabled = false;
    }

    private void Update()
    {
        // 초기화 게이트: 초기화 완료 전에는 모든 Update 로직 무시
        if (!isInitialized)
            return;

        // 죽은 상태면 입력 무시
        if (currentState == PlayerState.Die)
        {
            ApplyCustomGravity();
            ApplyVelocity();
            return;
        }

        // HP 감소 시스템 (게임이 진행 중일 때만)
        // [정책] 거리 누적 책임은 MapManager로 이동 (거리 SSOT = MapManager.scrollSpeed)
        if (GameManager.Instance != null && GameManager.Instance.isGameActive)
        {
            // 시간에 따른 체력 감소
            currentHp -= hpDrainRate * Time.deltaTime;
            
            // 체력이 0 이하가 되면 사망
            if (currentHp <= 0f)
            {
                currentHp = 0f;
                OnDie();
            }
            
            // 매 프레임 UI 갱신 (부드러운 슬라이더 애니메이션)
            if (UIManager.Instance != null)
            {
                UIManager.Instance.UpdateHpRatio(currentHp / maxHp);
            }
        }

        // 지면 체크
        CheckGround();

        // 입력 처리
        HandleInput();

        // 상태별 업데이트
        UpdateState();

        // 자력 적용 (젤리 끌어당기기)
        ApplyMagnetForce();

        // 중력 적용
        ApplyCustomGravity();

        // 속도 적용
        ApplyVelocity();
    }
    #endregion

    #region Input Handling
    private void HandleInput()
    {
        // Hit 상태에서는 입력 무시
        if (currentState == PlayerState.Hit)
            return;

        // 점프 입력 (스페이스바 또는 마우스 왼쪽 클릭)
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
        {
            TryJump();
        }

        // 슬라이드 입력 (S키 또는 아래 방향키)
        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
        {
            TrySlide();
        }
    }
    #endregion

    #region State Management
    private void UpdateState()
    {
        switch (currentState)
        {
            case PlayerState.Run:
                UpdateRunState();
                break;
            case PlayerState.Jump:
                UpdateJumpState();
                break;
            case PlayerState.DoubleJump:
                UpdateDoubleJumpState();
                break;
            case PlayerState.Slide:
                UpdateSlideState();
                break;
            case PlayerState.Hit:
                UpdateHitState();
                break;
            case PlayerState.Die:
                UpdateDieState();
                break;
        }
    }

    private void UpdateRunState()
    {
        velocity.x = runSpeed;

        // 땅에서 떨어지면 점프 상태로 전환
        if (!isGrounded && velocity.y < 0)
        {
            SetState(PlayerState.Jump);
        }
    }

    private void UpdateJumpState()
    {
        velocity.x = runSpeed;

        // 착지 시 Run 상태로 전환
        if (isGrounded && velocity.y <= 0)
        {
            jumpCount = 0;
            SetState(PlayerState.Run);
        }
    }

    private void UpdateDoubleJumpState()
    {
        velocity.x = runSpeed;

        // 착지 시 Run 상태로 전환
        if (isGrounded && velocity.y <= 0)
        {
            jumpCount = 0;
            SetState(PlayerState.Run);
        }
    }

    private void UpdateSlideState()
    {
        velocity.x = runSpeed;
        slideTimer += Time.deltaTime;

        // 슬라이드 종료
        if (slideTimer >= slideDuration)
        {
            EndSlide();
        }
    }

    private void UpdateHitState()
    {
        hitTimer += Time.deltaTime;

        // Hit 상태 종료
        if (hitTimer >= hitDuration)
        {
            if (isGrounded)
            {
                SetState(PlayerState.Run);
            }
            else
            {
                SetState(PlayerState.Jump);
            }
        }
    }

    private void UpdateDieState()
    {
        // 죽은 상태에서는 낙하만 함
        velocity.x = 0;
    }

    private void SetState(PlayerState newState)
    {
        if (currentState == newState)
            return;

        // 상태 종료 처리
        OnStateExit(currentState);

        currentState = newState;

        // 상태 진입 처리
        OnStateEnter(newState);

        // 애니메이터 연동
        // if (animator != null)
        // {
        //     animator.SetInteger("State", (int)currentState);
        // }

        Debug.Log($"State Changed: {newState}");
    }

    private void OnStateEnter(PlayerState state)
    {
        switch (state)
        {
            case PlayerState.Run:
                ResetColliderSize();
                break;
            case PlayerState.Jump:
                break;
            case PlayerState.DoubleJump:
                break;
            case PlayerState.Slide:
                SetSlideColliderSize();
                slideTimer = 0f;
                break;
            case PlayerState.Hit:
                hitTimer = 0f;
                ResetColliderSize();
                break;
            case PlayerState.Die:
                ResetColliderSize();
                break;
        }
    }

    private void OnStateExit(PlayerState state)
    {
        switch (state)
        {
            case PlayerState.Slide:
                ResetColliderSize();
                break;
        }
    }
    #endregion

    #region Action Methods
    private void TryJump()
    {
        // 슬라이드, 피격, 사망 상태에서는 점프 불가
        if (currentState == PlayerState.Slide || currentState == PlayerState.Hit || currentState == PlayerState.Die)
            return;

        // 1. 땅에 있을 때 (1단 점프)
        if (isGrounded)
        {
            velocity.y = jumpForce1; // 힘을 더하는게 아니라 수직 속도를 덮어씌움 (쫀득함의 핵심)
            jumpCount = 1;
            SetState(PlayerState.Jump);
            
            // 점프 애니메이션 시작
            if (animator != null)
                animator.SetBool("IsJumping", true);
        }
        // 2. 공중이고 점프 횟수가 남았을 때 (2단 점프)
        else if (jumpCount < maxJumpCount)
        {
            velocity.y = jumpForce2; // 수직 속도 리셋 (낙하 중이어도 튀어오르게)
            jumpCount++;
            SetState(PlayerState.DoubleJump);
            
            // 점프 애니메이션 시작
            if (animator != null)
                animator.SetBool("IsJumping", true);
        }
    }

    private void Jump(float force)
    {
        velocity.y = force;
    }

    private void TrySlide()
    {
        // 땅에 있을 때만 슬라이드 가능
        if (isGrounded && currentState != PlayerState.Slide)
        {
            SetState(PlayerState.Slide);
        }
    }

    private void EndSlide()
    {
        if (isGrounded)
        {
            SetState(PlayerState.Run);
        }
        else
        {
            SetState(PlayerState.Jump);
        }
    }
    #endregion

    #region Physics
    private void ApplyCustomGravity()
    {
        // 죽은 상태는 별도 처리
        if (currentState == PlayerState.Die)
        {
            velocity.y = -dieFallSpeed;
            return;
        }

        // [수정된 부분] 땅에 있고, 아래로 떨어지려는 힘이 있다면? -> 속도 리셋 (낙하 방지)
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -1f; // 0이 아니라 -1 정도로 해서 바닥에 딱 붙게 유지
        }
        else
        {
            // 공중에 있을 때만 중력 적용
            velocity.y -= gravity * Time.deltaTime;
        }

        // 최대 낙하 속도 제한
        if (velocity.y < -maxFallSpeed)
        {
            velocity.y = -maxFallSpeed;
        }
    }

    private void ApplyVelocity()
    {
        rb.velocity = velocity;
    }

    private void CheckGround()
    {
        // BoxCast 시작 위치 (콜라이더 하단)
        Vector2 boxCenter = (Vector2)transform.position + boxCollider.offset;
        float halfHeight = boxCollider.size.y * 0.5f;
        Vector2 castOrigin = boxCenter;
        castOrigin.y -= halfHeight - groundCheckSize.y * 0.5f; // 살짝 위에서 시작

        // BoxCast로 아래 방향으로 땅 감지
        groundHit = Physics2D.BoxCast(
            castOrigin,                  // 시작 위치
            groundCheckSize,             // 박스 크기
            0f,                          // 회전 각도
            Vector2.down,                // 방향 (아래)
            groundCheckDistance,         // 거리
            groundLayer                  // 레이어
        );

        isGrounded = groundHit.collider != null;

        // [위치 보정] 땅을 감지했고, 캐릭터가 땅속에 파묻혀 있다면 표면으로 스냅
        if (isGrounded && enableGroundSnap && velocity.y <= 0)
        {
            // 땅의 표면 위치 계산
            float groundSurfaceY = groundHit.point.y;
            
            // 캐릭터의 바닥 위치
            float characterBottomY = transform.position.y + boxCollider.offset.y - halfHeight;
            
            // 캐릭터가 땅보다 아래에 있다면 위로 이동 (스냅)
            if (characterBottomY < groundSurfaceY)
            {
                float snapOffset = groundSurfaceY - characterBottomY;
                Vector3 newPosition = transform.position;
                newPosition.y += snapOffset;
                transform.position = newPosition;
                
                // 수직 속도도 리셋 (더 이상 떨어지지 않도록)
                velocity.y = Mathf.Max(velocity.y, 0f);
            }
        }
    }
    #endregion

    #region Magnet System
    /// <summary>
    /// 자력 범위 내의 젤리를 플레이어 쪽으로 끌어당깁니다.
    /// 성능 최적화: OverlapCircleNonAlloc 사용 + 0.05초 주기 스캔 + itemLayer 필터링
    /// </summary>
    private void ApplyMagnetForce()
    {
        // 자력이 0이면 작동하지 않음
        if (magnetRange <= 0f)
            return;

        // 타이머 갱신 (0.05초마다만 스캔)
        magnetScanTimer -= Time.deltaTime;
        if (magnetScanTimer > 0f)
            return;

        magnetScanTimer = magnetScanInterval;

        // NonAlloc으로 GC 압력 최소화 (itemLayer 적용)
        int hitCount = Physics2D.OverlapCircleNonAlloc(
            transform.position,
            magnetRange,
            magnetHits,
            itemLayer
        );

        #if DEVELOPMENT_BUILD || UNITY_EDITOR
        // 버퍼 부족 가능성 경고 (최초 1회만)
        if (hitCount == magnetHits.Length && !hasWarnedMagnetBufferFull)
        {
            Debug.LogWarning($"[PlayerController] 자석 버퍼가 가득 찼습니다. (hitCount: {hitCount}) 배열 크기를 늘려야 할 수 있습니다.");
            hasWarnedMagnetBufferFull = true;
        }
        #endif

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D col = magnetHits[i];
            
            // "Jelly" 태그를 가진 오브젝트만 끌어당김
            if (col != null && col.CompareTag("Jelly") && col.gameObject.activeSelf)
            {
                // 젤리 위치를 플레이어 쪽으로 이동
                Vector2 jellyPos = col.transform.position;
                Vector2 playerPos = transform.position;
                
                // MoveTowards로 부드럽게 끌어당김
                Vector2 newPos = Vector2.MoveTowards(jellyPos, playerPos, magnetSpeed * Time.deltaTime);
                col.transform.position = newPos;
            }
        }
    }
    #endregion

    #region Collider Management
    private void SetSlideColliderSize()
    {
        boxCollider.size = new Vector2(
            originalColliderSize.x,
            originalColliderSize.y * slideSizeRatio
        );

        // 오프셋 조정 (바닥에 붙도록)
        boxCollider.offset = new Vector2(
            originalColliderOffset.x,
            originalColliderOffset.y - (originalColliderSize.y * (1f - slideSizeRatio) * 0.5f)
        );
    }

    private void ResetColliderSize()
    {
        boxCollider.size = originalColliderSize;
        boxCollider.offset = originalColliderOffset;
    }
    #endregion

    #region Public Methods (외부 호출용)
    /// <summary>
    /// 플레이어가 피격당했을 때 호출
    /// </summary>
    public void OnHit()
    {
        if (currentState == PlayerState.Die)
            return;

        SetState(PlayerState.Hit);

        // [수정] 넉백 효과 (Y축만 튕겨오름, X축은 정상 속도 유지)
        velocity.y = hitKnockbackForce;
        velocity.x = runSpeed; // 0.5f 배속이 아닌 정상 속도 유지
    }

    /// <summary>
    /// 플레이어가 죽었을 때 호출
    /// </summary>
    public void OnDie()
    {
        SetState(PlayerState.Die);
        velocity.x = 0;
        
        // GameManager에 게임 오버 통보
        if (GameManager.Instance != null)
        {
            GameManager.Instance.GameOver();
        }
    }

    /// <summary>
    /// 현재 상태 가져오기
    /// </summary>
    public PlayerState GetCurrentState()
    {
        return currentState;
    }

    /// <summary>
    /// 지면에 있는지 여부
    /// </summary>
    public bool IsGrounded()
    {
        return isGrounded;
    }
    #endregion

    #region Collision & Trigger Handling
    /// <summary>
    /// 트리거 충돌 처리 (젤리 획득)
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        // 젤리 획득
        if (other.CompareTag("Jelly"))
        {
            Debug.Log("젤리 먹음! 점수 +100");
            
            // GameManager에 점수 추가
            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddScore(100);
            }
            
            // [수정] Destroy 대신 SetActive(false)로 숨김 (맵 재활용 시 다시 나타남)
            other.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 물리 충돌 처리 (장애물, 바닥)
    /// </summary>
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // 바닥 착지 (점프 애니메이션 종료)
        if (((1 << collision.gameObject.layer) & groundLayer) != 0)
        {
            if (animator != null)
                animator.SetBool("IsJumping", false);
        }
        
        // 장애물 충돌
        if (collision.gameObject.CompareTag("Obstacle"))
        {
            // Hit 또는 Die 상태가 아닐 때만 피격 처리
            if (currentState != PlayerState.Hit && currentState != PlayerState.Die)
            {
                // 체력 감소
                currentHp -= 40f;
                Debug.Log($"장애물 충돌! 체력 -40 (현재 체력: {currentHp})");
                
                // 즉시 UI 갱신
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.UpdateHpRatio(currentHp / maxHp);
                }
                
                // 체력이 0 이하면 사망
                if (currentHp <= 0f)
                {
                    currentHp = 0f;
                    OnDie();
                }
                else
                {
                    // 살아있으면 피격 상태로 전환
                    OnHit();
                }
                
                // [수정] 충돌 직후 X축 속도 유지 (물리 반발력으로 밀리는 것 방지)
                // Rigidbody2D.constraints에서 X축이 고정되어 있지만, 추가 안전장치
                velocity.x = runSpeed;
            }
        }
    }
    #endregion

    #region Gizmos (디버깅용)
    private void OnDrawGizmos()
    {
        if (boxCollider == null)
            boxCollider = GetComponent<BoxCollider2D>();

        // BoxCast 시작 위치 계산
        Vector2 boxCenter = (Vector2)transform.position + boxCollider.offset;
        float halfHeight = boxCollider.size.y * 0.5f;
        Vector2 castOrigin = boxCenter;
        castOrigin.y -= halfHeight - groundCheckSize.y * 0.5f;

        // BoxCast 시작 박스 (반투명)
        Gizmos.color = isGrounded ? new Color(0f, 1f, 0f, 0.3f) : new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawCube(castOrigin, groundCheckSize);
        
        // BoxCast 시작 박스 외곽선 (진하게)
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireCube(castOrigin, groundCheckSize);

        // BoxCast 종료 위치 (거리만큼 아래)
        Vector2 castEnd = castOrigin + Vector2.down * groundCheckDistance;
        Gizmos.color = isGrounded ? new Color(0f, 1f, 0f, 0.2f) : new Color(1f, 0f, 0f, 0.2f);
        Gizmos.DrawCube(castEnd, groundCheckSize);
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireCube(castEnd, groundCheckSize);

        // BoxCast 레이 시각화 (4개 모서리)
        Vector2 halfSize = groundCheckSize * 0.5f;
        Gizmos.color = isGrounded ? Color.green : Color.red;
        
        // 좌하단
        Gizmos.DrawLine(
            castOrigin + new Vector2(-halfSize.x, -halfSize.y),
            castEnd + new Vector2(-halfSize.x, -halfSize.y)
        );
        // 우하단
        Gizmos.DrawLine(
            castOrigin + new Vector2(halfSize.x, -halfSize.y),
            castEnd + new Vector2(halfSize.x, -halfSize.y)
        );
        // 좌상단
        Gizmos.DrawLine(
            castOrigin + new Vector2(-halfSize.x, halfSize.y),
            castEnd + new Vector2(-halfSize.x, halfSize.y)
        );
        // 우상단
        Gizmos.DrawLine(
            castOrigin + new Vector2(halfSize.x, halfSize.y),
            castEnd + new Vector2(halfSize.x, halfSize.y)
        );

        // 땅 충돌 지점 표시 (노란색 십자)
        if (isGrounded && groundHit.collider != null)
        {
            Gizmos.color = Color.yellow;
            Vector2 hitPoint = groundHit.point;
            float crossSize = 0.2f;
            Gizmos.DrawLine(hitPoint + Vector2.left * crossSize, hitPoint + Vector2.right * crossSize);
            Gizmos.DrawLine(hitPoint + Vector2.up * crossSize, hitPoint + Vector2.down * crossSize);
        }
    }

    /// <summary>
    /// 오브젝트가 선택되었을 때만 자석 범위를 시각화합니다.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // 자력 범위가 0보다 클 때만 표시
        if (magnetRange > 0f)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, magnetRange);
        }
    }
    #endregion
}