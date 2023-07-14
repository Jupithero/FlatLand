﻿using OknaaEXTENSIONS.CustomWrappers;
using Player.Animation;
using Player.StateMachines.Base;
using Player.StateMachines.MoveStates;
using Player.StateMachines.WeaponStates;
using StarterAssetsStuff;
using UnityEngine;
using InputSystem = Systems.InputSystem;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/* Note: animations are called via the controller for both the character and capsule using animator null checks
 */

namespace Player {
    [RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class ThirdPersonController : Singleton<ThirdPersonController> {
        [Header("Player")]
        public float SneakSpeed = 1f;

        public float MoveSpeed = 4f;
        public float SprintSpeed = 6f;
        [Range(0.0f, 0.3f)] public float RotationSmoothTime = 0.12f;
        public float SpeedChangeRate = 10.0f;
        public AudioClip LandingAudioClip;
        public AudioClip[] FootstepAudioClips;
        [Range(0, 1)] public float FootstepAudioVolume = 0.5f;

        [Space(10)]
        [Tooltip("The height the player can jump")]
        public float JumpHeight = 1.2f;

        [Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
        public float Gravity = -15.0f;

        [Space(10)]
        [Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
        public float JumpTimeout = 0.50f;

        [Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
        public float FallTimeout = 0.15f;

        [Header("Player Grounded")]
        [Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
        public bool Grounded = true;

        [Tooltip("Useful for rough ground")]
        public float GroundedOffset = -0.14f;

        [Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
        public float GroundedRadius = 0.28f;

        [Tooltip("What layers the character uses as ground")]
        public LayerMask GroundLayers;

        [Header("Cinemachine")]
        [Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
        public GameObject CinemachineCameraTarget;

        [Tooltip("How far in degrees can you move the camera up")]
        public float TopClamp = 70.0f;

        [Tooltip("How far in degrees can you move the camera down")]
        public float BottomClamp = -30.0f;

        [Tooltip("Additional degress to override the camera. Useful for fine tuning camera position when locked")]
        public float CameraAngleOverride = 0.0f;

        [Tooltip("For locking the camera position on all axis")]
        public bool LockCameraPosition = false;


        public StarterAssetsInputs Input => _input;
        public CharacterController CharacterController => _characterController;
        public float VerticalVelocity => _verticalVelocity;
        public float Speed {
            get => _speed;
            set => _speed = value;
        }
        public float RotationVelocity {
            get => _rotationVelocity;
            set => _rotationVelocity = value;
        }
        public float TargetRotation {
            get => _targetRotation;
            set => _targetRotation = value;
        } 
        public float TargetSpeed {
            get => _targetSpeed;
            set => _targetSpeed = value;
        }    
        public float AnimationBlend {
            get => _animationBlend;
            set => _animationBlend = value;
        }
        


        // cinemachine
        private float _cinemachineTargetYaw;
        private float _cinemachineTargetPitch;

        // player
        private float _speed;
        private float _targetSpeed;
        private float _animationBlend;
        private float _targetRotation = 0.0f;
        private float _rotationVelocity;
        private float _verticalVelocity;
        private float _terminalVelocity = 53.0f;

        // timeout deltatimed
        private float _jumpTimeoutDelta;
        private float _fallTimeoutDelta;


        private PlayerInput _playerInput;
        private Animator _animator;
        private CharacterController _characterController;
        private StarterAssetsInputs _input;
        private GameObject _mainCamera;

        private const float _threshold = 0.01f;

        private bool _hasAnimator;

        private bool IsCurrentDeviceMouse => _playerInput.currentControlScheme == "KeyboardMouse";


        private void Awake() {
            if (_mainCamera == null) {
                _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
            }
        }

        private void Start() {
            _cinemachineTargetYaw = CinemachineCameraTarget.transform.rotation.eulerAngles.y;

            PlayerAnimation.Init(GetComponent<Animator>());
            _characterController = GetComponent<CharacterController>();
            _input = GetComponent<StarterAssetsInputs>();
            _playerInput = GetComponent<PlayerInput>();

            InputSystem.Init(this, _input);
            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;
        }

        private void Update() {
            // JumpAndGravity();
            // HandleMovement();
        }

        private void LateUpdate() {
            CameraRotation();

            void CameraRotation() {
                // make the player rotate with the _input.look when aiming


                if (_input.look.sqrMagnitude >= _threshold && !LockCameraPosition) {
                    //Don't multiply mouse input by Time.deltaTime;
                    float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

                    _cinemachineTargetYaw += _input.look.x * deltaTimeMultiplier;
                    _cinemachineTargetPitch += _input.look.y * deltaTimeMultiplier;
                }

                _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
                _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

                CinemachineCameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch + CameraAngleOverride, _cinemachineTargetYaw, 0.0f);
            }
        }


        public void JumpAndGravity() {
            if (Grounded) {
                _fallTimeoutDelta = FallTimeout;

                PlayerAnimation.SetJump((false));
                PlayerAnimation.SetFreeFall((false));

                if (_verticalVelocity < 0.0f) _verticalVelocity = -2f;

                if (_input.jump && _jumpTimeoutDelta <= 0.0f) {
                    var velocityNeededToReachHeight = Mathf.Sqrt(JumpHeight * -2f * Gravity);
                    _verticalVelocity = velocityNeededToReachHeight;

                    PlayerAnimation.SetJump((true));
                }

                if (_jumpTimeoutDelta >= 0.0f) _jumpTimeoutDelta -= Time.deltaTime;
            }
            else {
                _jumpTimeoutDelta = JumpTimeout;

                if (_fallTimeoutDelta >= 0.0f) _fallTimeoutDelta -= Time.deltaTime;
                else PlayerAnimation.SetFreeFall((true));

                _input.jump = false;
            }

            if (_verticalVelocity < _terminalVelocity) _verticalVelocity += Gravity * Time.deltaTime;
            
            GroundedCheck();
        }

        private void GroundedCheck() {
            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);
            Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);
            PlayerAnimation.SetGrounded(Grounded);
        }

        private void HandleMovement() {
            // CheckInput();
            // InputSystem.CheckInput();
            // BlendSpeedOverTime();


            // Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;
            // if (_input.move != Vector2.zero) {
            //     _targetRotation = _input.aim ? _input.look.x * RotationSmoothTime : Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + _mainCamera.transform.eulerAngles.y;
            //
            //     float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity, RotationSmoothTime);
            //
            //     transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
            // }
    //
            // Vector3 targetDirection = Quaternion.Euler(0.0f, _targetRotation, 0.0f) * Vector3.forward;
            //
            // _characterController.Move(targetDirection.normalized * (_speed * Time.deltaTime) + new Vector3(0.0f, Speed, 0.0f) * Time.deltaTime);

            void BlendSpeedOverTime() {
                float currentHorizontalSpeed = new Vector3(_characterController.velocity.x, 0.0f, _characterController.velocity.z).magnitude;
                float speedOffset = 0.1f;
                float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;
                PlayerAnimation.SetMotionSpeed(inputMagnitude);

                var targetSpeedReached = currentHorizontalSpeed < _targetSpeed - speedOffset || currentHorizontalSpeed > _targetSpeed + speedOffset;
                if (targetSpeedReached) _speed = _targetSpeed;
                else {
                    _speed = Mathf.Lerp(currentHorizontalSpeed, _targetSpeed * inputMagnitude, Time.deltaTime * SpeedChangeRate);
                    _speed = Mathf.Round(_speed * 1000f) / 1000f;
                }

                _animationBlend = Mathf.Lerp(_animationBlend, _targetSpeed, Time.deltaTime * SpeedChangeRate);
                if (_animationBlend < 0.01f) _animationBlend = 0f;
                PlayerAnimation.SetSpeed(_animationBlend);
            }
        }

        public void SetTargetSpeed(float speed) => _targetSpeed = speed;


        #region MyRegion

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax) {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }

        private void OnDrawGizmosSelected() {
            Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
            Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

            Gizmos.color = Grounded ? transparentGreen : transparentRed;

            // when selected, draw a gizmo in the position of, and matching radius of, the grounded collider
            Gizmos.DrawSphere(
                new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z),
                GroundedRadius);
        }

        private void OnFootstep(AnimationEvent animationEvent) {
            if (animationEvent.animatorClipInfo.weight > 0.5f) {
                if (FootstepAudioClips.Length > 0) {
                    var index = Random.Range(0, FootstepAudioClips.Length);
                    AudioSource.PlayClipAtPoint(FootstepAudioClips[index], transform.TransformPoint(_characterController.center), FootstepAudioVolume);
                }
            }
        }

        private void OnLand(AnimationEvent animationEvent) {
            if (animationEvent.animatorClipInfo.weight > 0.5f) {
                AudioSource.PlayClipAtPoint(LandingAudioClip, transform.TransformPoint(_characterController.center), FootstepAudioVolume);
            }
        }

        #endregion
    }
}