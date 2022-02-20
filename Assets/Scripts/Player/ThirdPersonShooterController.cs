using System;
using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using Custom.FinalIK;
using UnityEngine;
using Unity.Netcode;

public class ThirdPersonShooterController : NetworkBehaviour
{
    [Tooltip("瞄准状态下使用的VirtualCamera")]
    private CinemachineVirtualCamera aimVirtualCamera;
    [SerializeField] [Tooltip("未瞄准状态下，摄像机的旋转灵敏度")]
    private float normalSensitivity = 1f;
    [SerializeField] [Tooltip("瞄准状态下，摄像机的旋转灵敏度")]
    private float aimSensitivity = 0.5f;
    [SerializeField] [Tooltip("可以被击中的层")]
    private LayerMask aimColliderLayerMask;
    [SerializeField] [Tooltip("子弹预制体")]
    private Transform pfBulletProjectile;
    [SerializeField] [Tooltip("子弹生成位置，即枪口位置")]
    private Transform spawnBulletPosition;

    [Header("Aim IK")]
    [SerializeField][Tooltip("瞄准位置")]
    private Transform aimTarget;
    [SerializeField][Tooltip("瞄准控制器组件")]
    private CustomAimController aimController;
    
    private InputHandler _input;
    private ThirdPersonController thirdPersonController;
    private Animator _animator;
    
    /// <summary>
    /// 鼠标指向位置对应的世界空间位置。
    /// </summary>
    private Vector3 mouseWorldPosition = Vector3.zero;
    
    
    private void Start()
    {
        _input = GetComponent<InputHandler>();
        thirdPersonController = GetComponent<ThirdPersonController>();
        TryGetComponent(out _animator);
        if (IsClient && IsOwner)
        {
            aimVirtualCamera = GameObject.Find("PlayerAimCamera").GetComponent<CinemachineVirtualCamera>();
            aimVirtualCamera.Follow = thirdPersonController.CinemachineCameraTarget.transform;
        }
    }

    private void Update()
    {
        // 为兼容非键鼠设备，使用屏幕中心点作为瞄准位置。
        Vector2 screenCenterPoint = new Vector2(Screen.width / 2f, Screen.height / 2f);
        Ray ray = Camera.main.ScreenPointToRay(screenCenterPoint);
        if (Physics.Raycast(ray, out RaycastHit raycastHit, 999f, aimColliderLayerMask)) {
            mouseWorldPosition = raycastHit.point;
            aimTarget.position = raycastHit.point;
        }
        if (_input.aim)
        {
            aimVirtualCamera.enabled = true;
            thirdPersonController.SetSensibility(aimSensitivity);
            thirdPersonController.SetRotateOnMove(false);
            
            //Todo: 配合IK制作更加自然的动作。
            aimController.target = aimTarget;
            // // 确定瞄准方向，在瞄准状态下，Player始终朝向此方向
            // Vector3 worldAimTarget = mouseWorldPosition;
            // worldAimTarget.y = transform.position.y;
            // Vector3 aimDirection = (worldAimTarget - transform.position).normalized;
            //
            // // 使Player（平滑地）朝向瞄准方向
            // transform.forward = Vector3.Lerp(transform.forward, aimDirection, Time.deltaTime * 20f);
        }
        else
        {
            aimVirtualCamera.enabled = false;
            thirdPersonController.SetSensibility(normalSensitivity);
            thirdPersonController.SetRotateOnMove(true);
            aimController.target = null;
        }
        
        if (_input.shoot) {
            // 发射子弹
            Vector3 aimDir = (mouseWorldPosition - spawnBulletPosition.position).normalized;
            Instantiate(pfBulletProjectile, spawnBulletPosition.position, Quaternion.LookRotation(aimDir, Vector3.up));
            //
            _input.shoot = false;
        }
    }
}
