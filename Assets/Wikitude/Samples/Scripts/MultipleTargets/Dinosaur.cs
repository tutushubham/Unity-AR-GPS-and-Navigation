using UnityEngine;
using System.Collections;

/* Controls a dinosaur in the Multiple Targets sample. */
public class Dinosaur : MonoBehaviour
{
    /* Parameter defined in the Assets/Wikitude/Samples/Animations/Dinosaur.controller. */
    private const string AttackParameterName = "Attack";
    private const string CelebrateParameterName = "Celebrate";
    private const string HitParameterName = "Hit";
    private const string WalkingSpeedParameterName = "Walking Speed";

    /* When the angle between the dinosaur and the desired target is less that this threshold, we stop rotating it. */
    private const float AngleThreshold = 1.0f;
    /* When the distance between the dinosaur and the desired target is less that this threshold, we stop moving it. */
    private const float DistanceThreshold = 0.6f;
    private const float WalkingSpeed = 1.0f;
    /* Time it takes to transition to full walking speed. */
    private const float ToWalkingTransitionTime = 0.5f;

    /* All the states in which the dinosaur can be in. */
    public enum State {
        Idle,
        RotateToTarget,
        MoveToTarget,
        WaitingForAttacker,
        Fight,
        Defeated,
        Celebrate,
        MoveToOrigin
    }

    /* The walking speed coroutine is started when the walking speed needs to gradually change towards a target speed. */
    private Coroutine _walkingSpeedCoroutine = null;
    /* The sequence coroutine is started when when two dinosaurs are tracked and one of them moves next to the other and initiates an attack. */
    private Coroutine _sequenceCoroutine = null;

    /* The target dinosaur that this dinosaur is supposed to attack. */
    public Dinosaur TargetDinosaur {
        get;
        private set;
    }

    /* The attacking dinosaur from which this dinosaur is supposed to defend from. */
    public Dinosaur AttackingDinosaur {
        get;
        private set;
    }

    private Animator _animator = null;

    public float RotationSpeed = 140.0f;
    public float MovementSpeed = 0.5f;

    public bool InBattle {
        get;
        private set;
    }

    private State _currentState;
    public State CurrentState {
        get {
            return _currentState;
        }
        private set {
            _currentState = value;
        }
    }

    /* When two dinosaurs are tracked, the attack sequence is started. */
    public void Attack(Dinosaur targetDinosaur) {
        TargetDinosaur = targetDinosaur;
        /* The target dinosaur will start its defense sequence. */
        TargetDinosaur.DefendFrom(this);
        InBattle = true;
        _sequenceCoroutine = StartCoroutine(StartAttackSequence());
    }

    private IEnumerator StartAttackSequence() {
        /* Rotate towards the target. */
        CurrentState = State.RotateToTarget;
        yield return RotateTowards(TargetDinosaur.transform);
        /* Move towards the target. */
        CurrentState = State.MoveToTarget;
        yield return MoveTowards(TargetDinosaur.transform);

        /* Wait for the defending dinosaur to receive the attack. */
        while (TargetDinosaur.CurrentState != State.WaitingForAttacker) {
            yield return null;
        }

        Attack();
        /* Keep attacking until the target dinosaur is defeated. */
        while (TargetDinosaur.CurrentState != State.Defeated) {
            yield return null;
        }

        Celebrate();

        /* Wait until the celebration is done and the dinosaur can move back to its original location */
        while (CurrentState != State.MoveToOrigin) {
            yield return null;
        }

        /* Walk back to the original location. */
        yield return StartCoroutine(WalkBackSequence());
    }

    private IEnumerator WalkBackSequence() {
        yield return RotateTowards(transform.parent);
        yield return MoveTowards(transform.parent, 0.1f);
        SetWalkingSpeed(0.0f, 0.2f);
        CurrentState = State.Idle;
        InBattle = false;
    }

    public void DefendFrom(Dinosaur attackingDinosaur) {
        AttackingDinosaur = attackingDinosaur;
        InBattle = true;
        _sequenceCoroutine = StartCoroutine(StartDefendSequence());
    }

    private void StopCoroutines() {
        if (_sequenceCoroutine != null) {
            StopCoroutine(_sequenceCoroutine);
        }
        if (_walkingSpeedCoroutine != null) {
            StopCoroutine(_walkingSpeedCoroutine);
        }
    }

    public void OnAttackerDisappeared() {
        /* If the attacking dinosaur dissappears, because its target was lost, revert to idle, if we weren't already defeated. */
        StopCoroutines();
        if (CurrentState != State.Defeated) {
            CurrentState = State.Idle;
            InBattle = false;
        }
    }

    public void OnTargetDisappeared() {
        /* If the target dinosaur dissapears, because its target was lost, move back to the original position. */
        StopCoroutines();

        StartCoroutine(WalkBackSequence());
    }

    private IEnumerator StartDefendSequence() {
        /* Rotate towards the attacker. */
        CurrentState = State.RotateToTarget;
        yield return RotateTowards(AttackingDinosaur.transform);
        /* Stop walking. */
        SetWalkingSpeed(0.0f, 0.5f);
        /* Wait for the fight to start. */
        CurrentState = State.WaitingForAttacker;
        while (CurrentState != State.Fight) {
            yield return null;
        }
        /* As soon as we are hit, play the hit animation. */
        _animator.SetTrigger(HitParameterName);
    }

    private void Attack() {
        _animator.SetTrigger(AttackParameterName);
        CurrentState = State.Fight;
    }

    private void Hit() {
        CurrentState = State.Fight;
    }

    private void Celebrate() {
        _animator.SetTrigger(CelebrateParameterName);
    }

    private void OnAttackAnimationEvent() {
        TargetDinosaur.Hit();
    }

    private void OnDefeatedAnimationEvent() {
        CurrentState = State.Defeated;
    }

    private void OnCelebrateEndAnimationEvent() {
        CurrentState = State.MoveToOrigin;
    }

    private float GetFacingAngleToTarget(Transform target) {
        var direction = Quaternion.FromToRotation(transform.forward, (target.position - transform.position).normalized);
        return direction.eulerAngles.y;
    }

    private IEnumerator RotateTowards(Transform rotationTarget) {
        /* Gradually rotate towards a target, until the AngleThreshold is hit. */
        var targetRotation = Quaternion.LookRotation((rotationTarget.position - transform.position).normalized, transform.up);
        var angleToTarget = Quaternion.Angle(targetRotation, transform.rotation);

        if (angleToTarget > AngleThreshold) {
            SetWalkingSpeed(WalkingSpeed, ToWalkingTransitionTime);

            while (angleToTarget > AngleThreshold && rotationTarget != null) {
                float maxAngle = RotationSpeed * Time.deltaTime;
                float maxT = Mathf.Min(1.0f, maxAngle / angleToTarget);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, maxT);

                targetRotation = Quaternion.LookRotation((rotationTarget.position - transform.position).normalized, transform.up);
                angleToTarget = Quaternion.Angle(targetRotation, transform.rotation);

                yield return null;
            }
        }
    }

    private float GetDistanceToTarget(Transform target) {
        return (transform.position - target.position).magnitude;
    }

    private IEnumerator MoveTowards(Transform moveTarget, float distanceThreshold = DistanceThreshold) {
        /* Gradually move towards a target, until the DistanceThreshold is hit. */
        float distanceToTarget = GetDistanceToTarget(moveTarget);
        if (distanceToTarget > DistanceThreshold) {
            SetWalkingSpeed(WalkingSpeed, ToWalkingTransitionTime);
            while (distanceToTarget > distanceThreshold && moveTarget != null) {
                transform.LookAt(moveTarget);

                Vector3 direction = (moveTarget.position - transform.position).normalized;
                transform.position += direction * MovementSpeed * Time.deltaTime;
                distanceToTarget = GetDistanceToTarget(moveTarget);

                yield return null;
            }
        }
    }

    private void SetWalkingSpeed(float walkingSpeed, float transitionTime) {
        _walkingSpeedCoroutine = StartCoroutine(SetWalkingSpeedCoroutine(walkingSpeed, transitionTime));
    }

    private IEnumerator SetWalkingSpeedCoroutine(float walkingSpeed, float transitionTime) {
        /* Gradually change the walking speed. */
        float startingSpeed = _animator.GetFloat(WalkingSpeedParameterName);
        float currentTime = 0.0f;
        while (currentTime < transitionTime) {
            _animator.SetFloat(WalkingSpeedParameterName, Mathf.Lerp(startingSpeed, walkingSpeed, currentTime / transitionTime));
            currentTime += Time.deltaTime;
            yield return null;
        }
        _animator.SetFloat(WalkingSpeedParameterName, walkingSpeed);
    }

    private void Awake() {
        _animator = GetComponent<Animator>();
        CurrentState = State.Idle;
    }
}
