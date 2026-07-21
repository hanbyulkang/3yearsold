using System.Collections;
using System;
using Random = UnityEngine.Random;
using UnityEngine;
using Extension;

/// <summary>
/// Autonomous wandering for the Dog Package NPC dogs.
///
/// The dog picks a random point inside a fenced area, turns toward it and walks or
/// runs there, then idles for a moment — sometimes sitting down, lying down or
/// barking — before choosing the next spot. Blocked paths, getting stuck and
/// leaving the area all make it pick a new destination.
///
/// Drives the DogAnimatorController through IsWalking / IsRunning plus the
/// Sit / LayDown / GetUp / Bark / Surprise triggers.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Animator))]
public class DogWanderAI : MonoBehaviour
{
    private enum State { Idle, Moving, Resting }

    [Header("Wander Area")]
    [SerializeField] private Vector2 _worldXRange = new Vector2(-5f, 5f);
    [SerializeField] private Vector2 _worldZRange = new Vector2(3f, 20f);
    [Tooltip("Box collider covering the inside of the fence. Leave empty to wander within Fallback Radius of the spawn position.")]
    [SerializeField] private BoxCollider _wanderArea;
    [Tooltip("Used when no Wander Area is assigned.")]
    [SerializeField] private float _fallbackRadius = 8f;
    [Tooltip("Keeps destinations this far inside the fence so the dog doesn't rub against the posts.")]
    [SerializeField] private float _areaPadding = 0.75f;

    [Header("Movement")]
    [SerializeField] private float _walkSpeed = 1f;
    [SerializeField] private float _runSpeed = 3f;
    [SerializeField] private float _turnSpeed = 180f;
    [Tooltip("How close counts as arrived.")]
    [SerializeField] private float _arriveDistance = 0.5f;
    [Tooltip("Destinations closer than this are rejected, so the dog doesn't shuffle in place.")]
    [SerializeField] private float _minTravelDistance = 2f;
    [Range(0f, 1f)]
    [Tooltip("Chance a trip is made at a run instead of a walk.")]
    [SerializeField] private float _runChance = 0.35f;

    [Header("Idle Behaviour")]
    [SerializeField] private Vector2 _idleDuration = new Vector2(1.5f, 4f);
    [Tooltip("How long the dog stays sitting or lying down.")]
    [SerializeField] private Vector2 _restDuration = new Vector2(4f, 9f);
    [Range(0f, 1f)] [SerializeField] private float _sitChance = 0.18f;
    [Range(0f, 1f)] [SerializeField] private float _layDownChance = 0.1f;
    [Range(0f, 1f)] [SerializeField] private float _barkChance = 0.12f;
    [Range(0f, 1f)] [SerializeField] private float _surpriseChance = 0.06f;

    [Header("Ground & Obstacles")]
    [Tooltip("Layers the dog can stand on. Destinations without ground under them are rejected.")]
    [SerializeField] private LayerMask _groundMask = ~0;
    [SerializeField] private float _groundProbeHeight = 20f;

    [Header("Debug")]
    [SerializeField] private bool _drawGizmos = true;

    private const int DestinationAttempts = 24;
    private const float StuckTime = 1.5f;
    private const float StuckDistance = 0.15f;

    private Animator _animator;
    private Rigidbody _rigidbody;
    private Collider _bodyCollider;
    private Transform _dogRoot;

    private State _state;
    private float _stateTimer;
    private Vector3 _destination;
    private Vector3 _homePosition;
    private bool _isWalking;
    private bool _isRunning;

    private Vector3 _stuckCheckPosition;
    private float _stuckTimer;

    private readonly RaycastHit[] _hits = new RaycastHit[8];
    private float _fenceAvoidCooldown;
    private Coroutine _happyRoutine;
    private Coroutine _poopRoutine;
    private bool _specialAction;
    public bool IsPerformingSpecialAction => _specialAction;

    /// <summary>Runtime/editor setup hook used by the automatic scene bootstrap.</summary>
    public void SetWanderArea(BoxCollider area)
    {
        _wanderArea = area;
    }

    public void PlayHappyReaction()
    {
        if (_specialAction)
            return;
        if (_happyRoutine != null)
            StopCoroutine(_happyRoutine);
        _happyRoutine = StartCoroutine(HappyReactionRoutine());
    }

    public bool PerformPoop(float sittingSeconds, Action<Vector3> onFinished)
    {
        if (_specialAction || _poopRoutine != null)
            return false;
        _poopRoutine = StartCoroutine(PoopRoutine(Mathf.Max(1f, sittingSeconds), onFinished));
        return true;
    }

    private IEnumerator PoopRoutine(float sittingSeconds, Action<Vector3> onFinished)
    {
        BeginSpecialAction();
        EnterIdle();
        Halt();
        _animator.SetBool(AnimationParameters.IsWalking, false);
        _animator.SetBool(AnimationParameters.IsRunning, false);
        _animator.Play(AnimationNames.SitDown, 0, 0f);
        yield return new WaitForSeconds(sittingSeconds);

        Vector3 poopPosition = transform.position - transform.forward * 0.45f;
        _animator.Play(AnimationNames.GetUpFromSit, 0, 0f);
        yield return new WaitForSeconds(0.9f);
        onFinished?.Invoke(poopPosition);

        _poopRoutine = null;
        EndSpecialAction();
    }

    public bool PerformFeed(Transform plate, float stopDistance, float eatingSeconds, Action onFinished)
    {
        if (plate == null || _specialAction)
            return false;
        StartCoroutine(FeedRoutine(plate, Mathf.Max(0.25f, stopDistance), Mathf.Max(0.5f, eatingSeconds), onFinished));
        return true;
    }

    private IEnumerator FeedRoutine(Transform plate, float stopDistance, float eatingSeconds, Action onFinished)
    {
        BeginSpecialAction();
        _animator.SetBool(AnimationParameters.IsWalking, false);
        _animator.SetBool(AnimationParameters.IsRunning, true);
        _animator.CrossFade(AnimationNames.Run, 0.15f, 0, 0f);
        float timeout = 12f;
        while (plate != null && timeout > 0f)
        {
            Vector3 target = plate.position;
            Vector3 delta = target - _rigidbody.position;
            delta.y = 0f;
            if (delta.magnitude <= stopDistance) break;

            Quaternion facing = Quaternion.LookRotation(delta.normalized, Vector3.up);
            _rigidbody.MoveRotation(Quaternion.RotateTowards(_rigidbody.rotation, facing, _turnSpeed * Time.fixedDeltaTime));
            _rigidbody.MovePosition(_rigidbody.position + delta.normalized * _runSpeed * Time.fixedDeltaTime);
            timeout -= Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        Halt();
        _animator.SetBool(AnimationParameters.IsRunning, false);
        if (plate != null)
        {
            Vector3 look = plate.position - transform.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.001f) _rigidbody.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
        }
        _animator.CrossFade(AnimationNames.Bite, 0.15f, 0, 0f);

        // Pause near the lowered-head part of Bite so eating reads as a held pose,
        // then resume the clip to lift the head naturally.
        float lowerHeadTime = Mathf.Min(0.45f, eatingSeconds * 0.25f);
        yield return new WaitForSeconds(lowerHeadTime);
        _animator.speed = 0f;
        yield return new WaitForSeconds(Mathf.Max(0.35f, eatingSeconds - lowerHeadTime - 0.45f));
        _animator.speed = 1f;
        yield return new WaitForSeconds(0.45f);
        onFinished?.Invoke();
        EndSpecialAction(true);
    }

    private void BeginSpecialAction()
    {
        _specialAction = true;
        if (_happyRoutine != null) { StopCoroutine(_happyRoutine); _happyRoutine = null; }
        EnterIdle();
        Halt();
        _animator.SetBool(AnimationParameters.IsWalking, false);
        _animator.SetBool(AnimationParameters.IsRunning, false);
        _animator.ResetTrigger(AnimationParameters.Bark);
        _animator.ResetTrigger(AnimationParameters.Sit);
        _animator.ResetTrigger(AnimationParameters.LayDown);
        _animator.ResetTrigger(AnimationParameters.Jump);
        _animator.ResetTrigger(AnimationParameters.GetUp);
    }

    private void EndSpecialAction(bool moveImmediately = false)
    {
        _animator.CrossFade(AnimationNames.Idle, 0.2f, 0, 0f);
        _specialAction = false;
        if (moveImmediately && TryPickDestination())
            EnterMoving();
        else
            EnterIdle();
    }

    private IEnumerator HappyReactionRoutine()
    {
        EnterIdle();
        _stateTimer = 2.5f;
        _animator.SetBool(AnimationParameters.IsWalking, false);
        _animator.SetBool(AnimationParameters.IsRunning, false);

        // Play the state directly so a current bark, sit, transition or other
        // animation can never swallow the heart-click reaction trigger.
        _animator.ResetTrigger(AnimationParameters.Jump);
        _animator.Play(AnimationNames.Jump, 0, 0f);
        yield return new WaitForSeconds(0.8f);
        if (Methods.AnimationBeingPlayed(_animator, AnimationNames.Idle))
            _animator.SetTrigger(AnimationParameters.Bark);
        _happyRoutine = null;
    }

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _rigidbody = GetComponent<Rigidbody>();
        _dogRoot = FindDogRoot(transform);
        _bodyCollider = (_dogRoot != null ? _dogRoot : transform).GetComponentInChildren<Collider>();
        _rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

        _homePosition = transform.position;
        _stuckCheckPosition = transform.position;
    }

    private void Start()
    {
        EnterIdle();
    }

    private void Update()
    {
        _fenceAvoidCooldown -= Time.deltaTime;

        if (_specialAction)
            return;

        switch (_state)
        {
            case State.Idle:
                TickIdle();
                break;
            case State.Moving:
                TickMoving();
                break;
            case State.Resting:
                TickResting();
                break;
        }

        UpdateAnimator();
    }

    private void FixedUpdate()
    {
        if (_specialAction || _state != State.Moving)
        {
            Halt();
            return;
        }

        Vector3 toTarget = _destination - _rigidbody.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f)
        {
            Halt();
            return;
        }

        // Turn toward the destination, then only drive forward once roughly facing it,
        // so the dog pivots on the spot instead of sliding sideways.
        Quaternion desired = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        Quaternion next = Quaternion.RotateTowards(_rigidbody.rotation, desired, _turnSpeed * Time.fixedDeltaTime);
        _rigidbody.MoveRotation(next);

        float speed = _isRunning ? _runSpeed : _walkSpeed;
        float facing = Quaternion.Angle(next, desired);
        if (facing > 60f)
            speed = 0f;

        Vector3 velocity = next * Vector3.forward * speed;
        velocity.y = _rigidbody.linearVelocity.y;   // leave gravity alone
        _rigidbody.linearVelocity = velocity;
    }

    private void Halt()
    {
        Vector3 velocity = _rigidbody.linearVelocity;
        velocity.x = 0f;
        velocity.z = 0f;
        _rigidbody.linearVelocity = velocity;
    }

    // ---------------------------------------------------------------- states

    private void EnterIdle()
    {
        _state = State.Idle;
        _stateTimer = Random.Range(_idleDuration.x, _idleDuration.y);
        _isWalking = false;
        _isRunning = false;
    }

    private void TickIdle()
    {
        _stateTimer -= Time.deltaTime;
        if (_stateTimer > 0f)
            return;

        // Only branch out of idle while the animator is actually idling, otherwise a
        // bark or get-up is still playing and the trigger would be swallowed.
        if (!Methods.AnimationBeingPlayed(_animator, AnimationNames.Idle))
            return;

        float roll = Random.value;
        float sit = _sitChance;
        float lie = sit + _layDownChance;
        float bark = lie + _barkChance;
        float surprise = bark + _surpriseChance;

        if (roll < sit)
            EnterResting(AnimationParameters.Sit);
        else if (roll < lie)
            EnterResting(AnimationParameters.LayDown);
        else if (roll < bark)
            PlayGesture(AnimationParameters.Bark);
        else if (roll < surprise)
            PlayGesture(AnimationParameters.Surprise);
        else if (TryPickDestination())
            EnterMoving();
        else
            _stateTimer = Random.Range(_idleDuration.x, _idleDuration.y);
    }

    private void PlayGesture(string trigger)
    {
        _animator.SetTrigger(trigger);
        _stateTimer = Random.Range(_idleDuration.x, _idleDuration.y);
    }

    private void EnterMoving()
    {
        _state = State.Moving;
        _isWalking = true;
        _isRunning = Random.value < _runChance;
        _stuckTimer = 0f;
        _stuckCheckPosition = transform.position;
    }

    private void TickMoving()
    {
        // Pushed out of the pen by physics? Head back to the middle.
        if (!IsInsideArea(transform.position))
            _destination = AreaCenter();

        Vector3 flat = _destination - transform.position;
        flat.y = 0f;

        if (flat.magnitude <= _arriveDistance)
        {
            EnterIdle();
            return;
        }

        if ((transform.position - _stuckCheckPosition).sqrMagnitude > StuckDistance * StuckDistance)
        {
            _stuckCheckPosition = transform.position;
            _stuckTimer = 0f;
        }
        else
        {
            _stuckTimer += Time.deltaTime;
            if (_stuckTimer >= StuckTime)
                RepickOrIdle();
        }
    }

    private void RepickOrIdle()
    {
        if (TryPickDestination())
            EnterMoving();
        else
            EnterIdle();
    }

    private void EnterResting(string trigger)
    {
        _animator.SetTrigger(trigger);
        _state = State.Resting;
        _stateTimer = Random.Range(_restDuration.x, _restDuration.y);
        _isWalking = false;
        _isRunning = false;
    }

    private void TickResting()
    {
        _stateTimer -= Time.deltaTime;
        if (_stateTimer > 0f)
            return;

        bool settled = Methods.AnimationBeingPlayed(_animator, AnimationNames.SitIdle)
                    || Methods.AnimationBeingPlayed(_animator, AnimationNames.LyingDownIdle);

        if (settled)
            _animator.SetTrigger(AnimationParameters.GetUp);
        else if (Methods.AnimationBeingPlayed(_animator, AnimationNames.Idle))
            EnterIdle();   // back on its feet
    }

    // ------------------------------------------------------------- animation

    private void UpdateAnimator()
    {
        // The controller only reads the locomotion bools from Idle / Walk / Run;
        // writing them during Sit, Bark or a get-up would fight those transitions.
        bool inLocomotion = Methods.AnimationBeingPlayed(_animator, AnimationNames.Idle)
                         || Methods.AnimationBeingPlayed(_animator, AnimationNames.Walk)
                         || Methods.AnimationBeingPlayed(_animator, AnimationNames.Run);
        if (!inLocomotion)
            return;

        _animator.SetBool(AnimationParameters.IsWalking, _isWalking);
        _animator.SetBool(AnimationParameters.IsRunning, _isRunning);
    }

    // ----------------------------------------------------------- destination

    private bool TryPickDestination()
    {
        for (int i = 0; i < DestinationAttempts; i++)
        {
            Vector3 candidate = RandomPointInArea();
            if (!TryProjectToGround(candidate, out Vector3 grounded))
                continue;

            Vector3 flat = grounded - transform.position;
            flat.y = 0f;
            if (flat.magnitude < _minTravelDistance)
                continue;

            _destination = grounded;
            return true;
        }

        return false;
    }

    private Vector3 RandomPointInArea()
    {
        float margin = _areaPadding;
        if (_bodyCollider != null)
            margin += Mathf.Max(_bodyCollider.bounds.extents.x, _bodyCollider.bounds.extents.z);

        float minX = Mathf.Min(_worldXRange.x, _worldXRange.y) + margin;
        float maxX = Mathf.Max(_worldXRange.x, _worldXRange.y) - margin;
        float minZ = Mathf.Min(_worldZRange.x, _worldZRange.y) + margin;
        float maxZ = Mathf.Max(_worldZRange.x, _worldZRange.y) - margin;
        return new Vector3(Random.Range(minX, maxX), transform.position.y, Random.Range(minZ, maxZ));
    }

    private Vector3 AreaCenter()
    {
        return new Vector3((_worldXRange.x + _worldXRange.y) * 0.5f, transform.position.y,
                           (_worldZRange.x + _worldZRange.y) * 0.5f);
    }

    private bool IsInsideArea(Vector3 worldPoint)
    {
        return worldPoint.x >= Mathf.Min(_worldXRange.x, _worldXRange.y)
            && worldPoint.x <= Mathf.Max(_worldXRange.x, _worldXRange.y)
            && worldPoint.z >= Mathf.Min(_worldZRange.x, _worldZRange.y)
            && worldPoint.z <= Mathf.Max(_worldZRange.x, _worldZRange.y);
    }

    private bool TryProjectToGround(Vector3 point, out Vector3 grounded)
    {
        grounded = point;

        Vector3 origin = point + Vector3.up * _groundProbeHeight;
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, _hits, _groundProbeHeight * 2f,
                                            _groundMask, QueryTriggerInteraction.Ignore);

        float nearest = float.MaxValue;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            if (IsOwnCollider(_hits[i].collider))
                continue;
            if (_hits[i].distance < nearest)
            {
                nearest = _hits[i].distance;
                grounded = _hits[i].point;
                found = true;
            }
        }

        return found;
    }

    private void OnCollisionEnter(Collision collision)
    {
        AvoidObstacle(collision);
    }

    private void AvoidObstacle(Collision collision)
    {
        // Compound colliders on the model can report contacts through sibling or
        // parent objects. Never treat anything in this Dog hierarchy as an obstacle.
        if (_fenceAvoidCooldown > 0f || IsOwnCollider(collision.collider))
            return;

        Vector3 inward = Vector3.zero;
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector3 normal = collision.GetContact(i).normal;
            // Floor/terrain contacts are support surfaces, not obstacles.
            if (Mathf.Abs(normal.y) < 0.7f)
                inward += normal;
        }
        inward.y = 0f;

        if (inward.sqrMagnitude < 0.001f)
            return;
        inward.Normalize();

        // Add a little sideways variation so repeated contacts do not make the
        // dog bounce between the same two fence segments.
        Vector3 tangent = Vector3.Cross(Vector3.up, inward);
        Vector3 escapeDirection = (inward + tangent * Random.Range(-0.55f, 0.55f)).normalized;
        Vector3 candidate = transform.position + escapeDirection * Random.Range(2.5f, 4.5f);

        if (IsInsideArea(candidate) && TryProjectToGround(candidate, out Vector3 grounded))
        {
            _destination = grounded;
            EnterMoving();
        }
        else if (TryPickDestination())
        {
            EnterMoving();
        }
        else
        {
            EnterIdle();
        }

        _fenceAvoidCooldown = 0.35f;
    }

    private void OnValidate()
    {
        _fallbackRadius = Mathf.Max(0.5f, _fallbackRadius);
        _areaPadding = Mathf.Max(0f, _areaPadding);
        _walkSpeed = Mathf.Max(0f, _walkSpeed);
        _runSpeed = Mathf.Max(_walkSpeed, _runSpeed);
        _turnSpeed = Mathf.Max(0f, _turnSpeed);
        _arriveDistance = Mathf.Max(0.05f, _arriveDistance);
        _minTravelDistance = Mathf.Max(_arriveDistance, _minTravelDistance);
    }

    private bool IsOwnCollider(Collider other)
    {
        if (other == null)
            return true;

        if (other.attachedRigidbody != null && other.attachedRigidbody == _rigidbody)
            return true;

        Transform owner = _dogRoot != null ? _dogRoot : transform;
        return other.transform == owner || other.transform.IsChildOf(owner);
    }

    private static Transform FindDogRoot(Transform start)
    {
        Transform result = start;
        for (Transform current = start; current != null; current = current.parent)
        {
            if (current.name.Equals("Dog", System.StringComparison.OrdinalIgnoreCase))
                result = current;
        }
        return result;
    }

    // ----------------------------------------------------------------- debug

    private void OnDrawGizmosSelected()
    {
        if (!_drawGizmos)
            return;

        Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.9f);
        Gizmos.DrawWireCube(AreaCenter(), new Vector3(
            Mathf.Abs(_worldXRange.y - _worldXRange.x), 0.05f,
            Mathf.Abs(_worldZRange.y - _worldZRange.x)));

        if (!Application.isPlaying || _state != State.Moving)
            return;

        Gizmos.color = _isRunning ? Color.red : Color.yellow;
        Gizmos.DrawLine(transform.position, _destination);
        Gizmos.DrawWireSphere(_destination, _arriveDistance);
    }
}
