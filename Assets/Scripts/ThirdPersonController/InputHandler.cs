using UnityEngine;
using UnityEngine.InputSystem;

public class InputHandler : MonoBehaviour
{
    [Header("Character Input Values")]
    public Vector2 move;
    public Vector2 look;
    public bool jump;
    [Tooltip("冲刺")]
    public bool sprint;

    [Header("Movement Settings")]
    [Tooltip("是否使用输入模拟真实运动（调整跑步动画播放速度和角色移动速度）")]
    public bool analogMovement;

#if !UNITY_IOS || !UNITY_ANDROID
    [Header("Mouse Cursor Settings")]
    [Tooltip("是否在获得焦点时，将鼠标指针固定在窗口中央")]
    public bool cursorLocked = true;
    [Tooltip("摄像机是否跟随鼠标")]
    public bool cursorInputForLook = true;
#endif

#if ENABLE_INPUT_SYSTEM
    public void OnMove(InputValue value)
    {
        MoveInput(value.Get<Vector2>());
    }

    public void OnLook(InputValue value)
    {
        if(cursorInputForLook)
        {
            LookInput(value.Get<Vector2>());
        }
    }

    public void OnJump(InputValue value)
    {
        JumpInput(value.isPressed);
    }

    public void OnSprint(InputValue value)
    {
        SprintInput(value.isPressed);
    }
#endif


    public void MoveInput(Vector2 newMoveDirection)
    {
        move = newMoveDirection;
    } 

    public void LookInput(Vector2 newLookDirection)
    {
        look = newLookDirection;
    }

    public void JumpInput(bool newJumpState)
    {
        jump = newJumpState;
    }

    public void SprintInput(bool newSprintState)
    {
        sprint = newSprintState;
    }
    
#if !UNITY_IOS || !UNITY_ANDROID
    
    // 如果是移动端，判断焦点还需要用到OnApplicationPause()。但此次暂时只考虑捕获鼠标功能，移动端不使用鼠标，暂不考虑。

    private void OnApplicationFocus(bool hasFocus)
    {
        // 根据是否获得焦点，觉得是否捕获鼠标
        if(hasFocus)
            SetCursorState(cursorLocked);
        else
            SetCursorState(false);
    }

    /// <summary>
    /// 设置是否捕获鼠标。传入true，则将鼠标指针锁定到窗口中央；传入false，则不锁定鼠标。
    /// </summary>
    /// <param name="newState">是否捕获鼠标</param>
    private void SetCursorState(bool newState)
    {
        Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
    }

#endif

}
