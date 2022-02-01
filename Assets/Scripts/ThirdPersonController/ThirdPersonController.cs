using Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/* Note: 角色和胶囊的动画都是通过控制器调用的，使用动画师的空检查。*/

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class ThirdPersonController : NetworkBehaviour
{
	[Header("Player")]
	[Tooltip("角色的移动速度，单位为 m/s")]
	public float MoveSpeed = 2.0f;
	[Tooltip("角色的冲刺速度为 m/s")]
	public float SprintSpeed = 5.335f;
	[Tooltip("角色转身面对运动方向的速度")]
	[Range(0.0f, 0.3f)]
	public float RotationSmoothTime = 0.12f;
	[Tooltip("加速和减速的速率")]
	public float SpeedChangeRate = 10.0f;

	[Space(10)]
	[Tooltip("玩家可以跳的高度")]
	public float JumpHeight = 1.2f;
	[Tooltip("角色使用自己的重力值。引擎的默认值是-9.81f")]
	public float Gravity = -15.0f;

	[Space(10)]
	[Tooltip("在能够再次跳跃之前所需的时间。设置为0f可立即再次跳起")]
	public float JumpTimeout = 0.50f;
	[Tooltip("进入下落状态前需要经过的时间。避免下楼梯时出错。")]
	public float FallTimeout = 0.15f;

	[Space(10)] 
	[Tooltip("最高的降落速度（用于限制下落速度过快）")]
	[SerializeField]
	private float _terminalVelocity = 53.0f;
	
	[Header("Player Grounded")]
	[Tooltip("角色是否已经接触地面。不是CharacterController内置的接地检查的一部分。")]
	public bool Grounded = true;
	[Tooltip("着地检测球的球心上下偏移量。0时，球心在脚底，值增大向下偏移。")]
	public float GroundedOffset = -0.14f;
	[Tooltip("接地检查的半径。应该与CharacterController的半径相匹配")]
	public float GroundedRadius = 0.28f;
	[Tooltip("被角色视为地面的 layers")]
	public LayerMask GroundLayers;

	[Header("Cinemachine")]
	[Tooltip("在Cinemachine虚拟摄像机中设置的跟随目标，摄像机将跟随该目标。")]
	public GameObject CinemachineCameraTarget;
	[Tooltip("你可以将摄像机向上移动多少度")]
	public float TopClamp = 70.0f;
	[Tooltip("你可以将摄像机向下移动多少度")]
	public float BottomClamp = -30.0f;
	[Tooltip("此值用于微调摄像机旋转的X轴分量。对锁定时微调相机位置很有用")]
	public float CameraAngleOverride = 0.0f;
	[Tooltip("用于锁定所有轴上的摄像机位置")]
	public bool LockCameraPosition = false;
	[Tooltip("指针运动可以使摄像机旋转的阈值。指针移动距离的平方小于此值时，摄像机不跟随指针")]
	[SerializeField]
	private float _threshold = 0.01f;

	// cinemachine
	/// <summary>
	/// 摄像机当前transform.rotation的Y轴分量
	/// </summary>
	private float _cinemachineTargetYaw;
	/// <summary>
	/// 摄像机当前transform.rotation的X轴分量
	/// </summary>
	private float _cinemachineTargetPitch;
	
	// player
	/// <summary>
	/// Player当前速度。与不同，此值最大速度使用最高设定速度 * 输入量大小，是真实速度
	/// </summary>
	private float _speed;
	/// <summary>
	/// Player动画器使用的速度变量。与_speed不同，此值最大值为移动或冲刺的设定速度。
	/// </summary>
	private float _animationBlend;
	/// <summary>
	/// Player目标旋转角度
	/// </summary>
	private float _targetRotation = 0.0f;
	/// <summary>
	/// 当前Player旋转到目标方向的速度。
	/// </summary>
	private float _rotationVelocity;
	/// <summary>
	///	Player当前纵向速度
	/// </summary>
	private float _verticalVelocity;

	// timeout deltatime
	/// <summary>
	/// 在能够再次跳跃之前的剩余等待时间。初始值来自序列化字段 JumpTimeout。
	/// </summary>
	private float _jumpTimeoutDelta;
	/// <summary>
	/// 进入下落状态前的剩余等待时间。初始值来自序列化字段 FallTimeout。
	/// </summary>
	private float _fallTimeoutDelta;

	// animation IDs
	/// <summary>
	/// 动画变量ID。角色当前速度。
	/// </summary>
	private int _animIDSpeed;
	/// <summary>
	/// 动画变量ID。角色是否着地。
	/// </summary>
	private int _animIDGrounded;
	/// <summary>
	/// 动画变量ID。角色是否在跳跃。
	/// </summary>
	private int _animIDJump;
	/// <summary>
	/// 动画变量ID。角色是否在自由降落。
	/// </summary>
	private int _animIDFreeFall;
	/// <summary>
	/// 动画变量ID。动画播放速度的乘数，用于调节动画播放速度。
	/// </summary>
	private int _animIDMotionSpeed;

	private Animator _animator;
	private CharacterController _controller;
	private InputHandler _input;
	private GameObject _mainCamera;
	private CinemachineVirtualCamera _cinemachineVirtualCamera;

	/// <summary>
	/// 是否使用Animator
	/// </summary>
	private bool _hasAnimator;

	private void Awake()
	{
		// 获取对 main camera 的引用
		if (_mainCamera == null)
		{
			_mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
		}
	}

	private void Start()
	{
		_hasAnimator = TryGetComponent(out _animator);
		_controller = GetComponent<CharacterController>();
		_input = GetComponent<InputHandler>();
		_cinemachineVirtualCamera = GameObject.Find("PlayerFollowCamera").GetComponent<CinemachineVirtualCamera>();
		_cinemachineVirtualCamera.Follow = CinemachineCameraTarget.transform;

		AssignAnimationIDs();

		// 在开始时，重置剩余等待时间
		_jumpTimeoutDelta = JumpTimeout;
		_fallTimeoutDelta = FallTimeout;
	}

	private void Update()
	{
		JumpAndGravity();
		GroundedCheck();
		Move();
	}

	private void LateUpdate()
	{
		CameraRotation();
	}

	/// <summary>
	/// 分配 Animator 变量的 ID（Hash值）。
	/// </summary>
	private void AssignAnimationIDs()
	{
		_animIDSpeed = Animator.StringToHash("Speed");
		_animIDGrounded = Animator.StringToHash("Grounded");
		_animIDJump = Animator.StringToHash("Jump");
		_animIDFreeFall = Animator.StringToHash("FreeFall");
		_animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
	}

	/// <summary>
	/// 着地检测。
	/// </summary>
	private void GroundedCheck()
	{
		Vector3 rootPosition = transform.position;
		// 设置着地检测球的球心位置，使用偏移值 GroundedOffset
		Vector3 spherePosition = new Vector3(rootPosition.x, rootPosition.y - GroundedOffset, rootPosition.z);
		// 进行碰撞检测，忽略触发器。
		Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);
		// 如果使用 Animator，则更新_animIDGrounded变量。
		if (_hasAnimator)
		{
			_animator.SetBool(_animIDGrounded, Grounded);
		}
	}

	/// <summary>
	/// 摄像机的鼠标跟随旋转。
	/// </summary>
	private void CameraRotation()
	{
		// 如果有输入，并且摄像机位置不固定的话
		if (_input.look.sqrMagnitude >= _threshold && !LockCameraPosition)
		{
			_cinemachineTargetYaw += _input.look.x * Time.deltaTime;
			_cinemachineTargetPitch += _input.look.y * Time.deltaTime;
		}

		// 钳制我们的旋转，使我们的值被限制在360度以内
		_cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
		_cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

		// 更改Cinemachine的目标物体旋转，以此改变摄像机的位置和旋转
		CinemachineCameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch + CameraAngleOverride, _cinemachineTargetYaw, 0.0f);
	}

	private void Move()
	{
		// 一个简单的加速和减速的设计，易于移除、替换或迭代
		
		// 根据移动速度、冲刺速度和是否按了冲刺键来设置目标速度
		float targetSpeed;

		// 注意：Vector2的==操作符使用了近似值，所以不容易出现浮点错误，而且比幅度更便宜。
		// 如果没有输入，将目标速度设为0
		if (_input.move == Vector2.zero) 
			targetSpeed = 0.0f;
		else
			targetSpeed = _input.sprint ? SprintSpeed : MoveSpeed;

		Vector3 controllerVelocity = _controller.velocity;
		// 实时水平面上速度的大小
		float currentHorizontalSpeed = new Vector3(controllerVelocity.x, 0.0f, controllerVelocity.z).magnitude;

		// 由于使用 Lerp ，将当前速度向目标速度逼近，此值用于避免速度进入没有意义的无限细分。
		float speedOffset = 0.1f;
		// 如果模拟真实运动，则根据输入的Vector2的大小，调整角色跑步、走路等动作的速度和移动速度；否则同一使用原速度播放动画并使用原速度移动。
		float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

		// 加速或减速至目标速度
		if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset)
		{
			// 使用 Lerp 给人一种更有机的速度变化
			_speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, Time.deltaTime * SpeedChangeRate);

			// 将速度四舍五入到小数点后3位
			_speed = Mathf.Round(_speed * 1000f) / 1000f;
		}
		else
		{
			// 使用Lerp将速度调整到目标速度附近后，直接转到目标速度
			_speed = targetSpeed;
		}
		// 获取Animator中速度变量的值
		_animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, Time.deltaTime * SpeedChangeRate);

		// 归一化输入方向
		Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

		// 注意：Vector2的 !=操作符使用了近似值，所以不容易出现浮点错误，而且比幅度更便宜。
		// 移动时，调整身体转身到目标角度
		if (_input.move != Vector2.zero)
		{
			// 目标旋转角度 = 输入的Vector2的弧度 * 弧度转角度常数 + mainCamera的全局角度的水平面分量
			_targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + _mainCamera.transform.eulerAngles.y;
			// 平滑地转身。
			float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity, RotationSmoothTime);
			// 旋转到相对于相机位置的输入方向
			transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
		}


		Vector3 targetDirection = Quaternion.Euler(0.0f, _targetRotation, 0.0f) * Vector3.forward;

		// 移动Player（水平面移动量+垂直移动量）
		_controller.Move(targetDirection.normalized * (_speed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);

		// 更新 animator 
		if (_hasAnimator)
		{
			_animator.SetFloat(_animIDSpeed, _animationBlend);
			_animator.SetFloat(_animIDMotionSpeed, inputMagnitude);
		}
	}

	private void JumpAndGravity()
	{
		if (Grounded)
		{
			// 回到地面，重置下落CD
			_fallTimeoutDelta = FallTimeout;

			// 更新 animator 
			if (_hasAnimator)
			{
				_animator.SetBool(_animIDJump, false);
				_animator.SetBool(_animIDFreeFall, false);
			}

			// 阻止我们落地后速度无限下降
			if (_verticalVelocity < 0.0f)
			{
				_verticalVelocity = -2f;
			}

			// 跳跃
			if (_input.jump && _jumpTimeoutDelta <= 0.0f)
			{
				// H*-2*G的平方根=达到理想高度所需的速度
				_verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);

				// update animator
				if (_hasAnimator)
				{
					_animator.SetBool(_animIDJump, true);
				}
			}

			// jump timeout
			if (_jumpTimeoutDelta >= 0.0f)
			{
				_jumpTimeoutDelta -= Time.deltaTime;
			}
		}
		else
		{
			// 减少确认进入下落确认时间
			if (_fallTimeoutDelta >= 0.0f)
			{
				_fallTimeoutDelta -= Time.deltaTime;
			}
			else
			{
				// 确认进入下落状态
				// 离开地面则重置跳跃CD
				_jumpTimeoutDelta = JumpTimeout;
				// 如果到达下落CD，这播放自由下落动画
				if (_hasAnimator)
				{
					_animator.SetBool(_animIDFreeFall, true);
				}
			}

			// 不在地面时不允许跳跃
			_input.jump = false;
		}

		// 如果没有达到最高下落速度，则使用重力加速度增加下落速度
		if (_verticalVelocity < _terminalVelocity)
		{
			_verticalVelocity += Gravity * Time.deltaTime;
		}
	}

	/// <summary>
	/// 限制角度范围。原值可以超过360度，目标不可以。
	/// </summary>
	/// <param name="lfAngle">需要限制范围的角度</param>
	/// <param name="lfMin">最小允许角度</param>
	/// <param name="lfMax">最大允许角度</param>
	/// <returns></returns>
	private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
	{
		if (lfAngle < -360f) lfAngle += 360f;
		if (lfAngle > 360f) lfAngle -= 360f;
		return Mathf.Clamp(lfAngle, lfMin, lfMax);
	}

	/// <summary>
	/// 展示着地检测球。着地为绿色，没有着地为红色。
	/// </summary>
	private void OnDrawGizmosSelected()
	{
		Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
		Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

		if (Grounded) 
			Gizmos.color = transparentGreen;
		else 
			Gizmos.color = transparentRed;
		
		// 当被选中时，在接地的碰撞器的位置和匹配的半径上画一个Gizmos。
		Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z), GroundedRadius);
	}
}
