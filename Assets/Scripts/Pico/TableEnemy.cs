using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class TableEnemy : MonoBehaviour
{
    public enum State
    {
        Wandering,
        Fleeing
    }

    private const int FleeCandidateCount = 12;
    private static readonly int MoveHash = Animator.StringToHash("Move");
    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    public MotionTrackerDriver tracker;
    public float wanderSpeed = 0.08f;
    public float fleeSpeed = 0.14f;
    public float rotationSpeed = 360f;
    public float detectionRadius = 0.3f;
    public float exitDetectionRadius = 0.4f;
    public float waypointMinDist = 0.02f;
    public float waypointTimeout = 3f;
    public float edgeMargin = 0.1f;
    public float fleeTargetDistance = 0.25f;
    public float fleeRepathInterval = 0.25f;

    public State CurrentState { get; private set; }
    public PicoFloorArea Area { get; private set; }

    private Rigidbody _body;
    private Animator _animator;
    private bool _hasMoveParameter;
    private bool _hasSpeedParameter;
    private Vector3 _target;
    private bool _hasTarget;
    private float _targetSetTime;
    private float _height;
    private float _nextFleeTargetTime;

    private void Awake()
    {
        _body = GetComponent<Rigidbody>();
        _body.isKinematic = true;
        _body.useGravity = false;
        _animator = GetComponent<Animator>();
        CacheAnimatorParameters();
        if (tracker == null)
        {
            tracker = FindAnyObjectByType<MotionTrackerDriver>();
        }
    }

    public void Configure(PicoFloorArea area, MotionTrackerDriver tracker)
    {
        Area = area;
        this.tracker = tracker;
        _height = transform.position.y;
        CurrentState = State.Wandering;
        ClearTarget();
    }

    private void FixedUpdate()
    {
        if (Area.Vertices == null || Area.Vertices.Length < 3)
        {
            UpdateAnimator(false);
            return;
        }

        UpdateState();
        if (CurrentState == State.Fleeing)
        {
            UpdateFleeTarget();
        }
        else
        {
            UpdateWanderTarget();
        }

        if (_hasTarget)
        {
            MoveToTarget(_target);
        }
        else
        {
            UpdateAnimator(false);
        }
    }

    private void UpdateState()
    {
        var nextState = CurrentState;
        if (tracker == null || !tracker.DebugPoseValid)
        {
            nextState = State.Wandering;
        }
        else
        {
            var distance = HorizontalDistance(transform.position, tracker.transform.position);
            if (CurrentState == State.Fleeing)
            {
                nextState = distance <= Mathf.Max(detectionRadius, exitDetectionRadius)
                    ? State.Fleeing
                    : State.Wandering;
            }
            else if (distance <= detectionRadius)
            {
                nextState = State.Fleeing;
            }
        }

        if (nextState == CurrentState)
        {
            return;
        }

        CurrentState = nextState;
        ClearTarget();
    }

    private void UpdateWanderTarget()
    {
        if (_hasTarget && (HorizontalDistance(transform.position, _target) <= waypointMinDist ||
                           Time.time - _targetSetTime >= waypointTimeout))
        {
            ClearTarget();
        }

        if (_hasTarget || !Area.TryGetRandomPoint(edgeMargin, out var point))
        {
            return;
        }

        SetTarget(point);
    }

    private void UpdateFleeTarget()
    {
        if (_hasTarget && Time.time < _nextFleeTargetTime)
        {
            return;
        }

        ClearTarget();
        _nextFleeTargetTime = Time.time + fleeRepathInterval;
        if (tracker == null || fleeTargetDistance <= 0f)
        {
            return;
        }

        var away = transform.position - tracker.transform.position;
        away.y = 0f;
        if (away.sqrMagnitude <= 0.0001f)
        {
            away = -tracker.transform.forward;
            away.y = 0f;
        }
        if (away.sqrMagnitude <= 0.0001f)
        {
            away = transform.forward;
            away.y = 0f;
        }
        if (away.sqrMagnitude <= 0.0001f)
        {
            away = Vector3.back;
        }
        away.Normalize();

        var currentDistance = HorizontalDistance(transform.position, tracker.transform.position);
        var bestScore = 0f;
        var foundTarget = false;
        var bestTarget = Vector3.zero;
        for (var i = 0; i < FleeCandidateCount; i++)
        {
            var direction = Quaternion.AngleAxis(i * 360f / FleeCandidateCount, Vector3.up) * away;
            var candidate = transform.position + direction * fleeTargetDistance;
            candidate.y = _height;
            if (!Area.ContainsXZ(candidate, edgeMargin))
            {
                continue;
            }

            var candidateDistance = HorizontalDistance(candidate, tracker.transform.position);
            var score = 2f * Vector3.Dot(direction, away) +
                (candidateDistance - currentDistance) / fleeTargetDistance;
            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestTarget = candidate;
            foundTarget = true;
        }

        if (foundTarget)
        {
            SetTarget(bestTarget);
        }
    }

    private void SetTarget(Vector3 target)
    {
        target.y = _height;
        _target = target;
        _hasTarget = true;
        _targetSetTime = Time.time;
    }

    private void ClearTarget()
    {
        _hasTarget = false;
        _nextFleeTargetTime = 0f;
    }

    private void MoveToTarget(Vector3 target)
    {
        target.y = _height;
        var current = _body.position;
        current.y = _height;
        var speed = CurrentState == State.Fleeing ? fleeSpeed : wanderSpeed;
        var next = Vector3.MoveTowards(current, target, speed * Time.fixedDeltaTime);
        var direction = next - current;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0000001f)
        {
            UpdateAnimator(false);
            return;
        }

        if (!Area.ContainsXZ(next, edgeMargin))
        {
            ClearTarget();
            UpdateAnimator(false);
            return;
        }

        _body.MovePosition(next);
        var targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        _body.MoveRotation(Quaternion.RotateTowards(_body.rotation, targetRotation,
            rotationSpeed * Time.fixedDeltaTime));
        UpdateAnimator(true);
    }

    private void CacheAnimatorParameters()
    {
        if (_animator == null)
        {
            return;
        }

        foreach (var parameter in _animator.parameters)
        {
            _hasMoveParameter |= parameter.nameHash == MoveHash &&
                parameter.type == AnimatorControllerParameterType.Bool;
            _hasSpeedParameter |= parameter.nameHash == SpeedHash &&
                parameter.type == AnimatorControllerParameterType.Int;
        }
    }

    private void UpdateAnimator(bool moving)
    {
        if (_animator == null)
        {
            return;
        }

        if (_hasMoveParameter)
        {
            _animator.SetBool(MoveHash, moving);
        }
        if (_hasSpeedParameter)
        {
            _animator.SetInteger(SpeedHash, CurrentState == State.Fleeing ? 2 : 1);
        }
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
