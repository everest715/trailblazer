using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InputManager : MonoBehaviour
{
    [SerializeField]
    private List<KeyCode> jumpKeys = new List<KeyCode>();

    [SerializeField]
    private List<KeyCode> runKeys = new List<KeyCode>();

    [SerializeField]
    private List<KeyCode> attackKeys = new List<KeyCode>();

    [SerializeField]
    private List<KeyCode> rightKeys = new List<KeyCode>();

    [SerializeField]
    private List<KeyCode> leftKeys = new List<KeyCode>();

    [SerializeField]
    private List<KeyCode> upKeys = new List<KeyCode>();

    [SerializeField]
    private List<KeyCode> downKeys = new List<KeyCode>();

    [SerializeField]
    private string XInputName = "Horizontal";

    [SerializeField]
    private string YInputName = "Vertical";

    private static InputManager instance;
    private void Awake()
    {
        instance = this;
    }
    public static bool JumpDown
    {
        get
        {
            bool pressed = false;

            foreach (var k in instance.jumpKeys)
            {
                if (MarioInput.GetKeyDown(k))
                {
                    pressed = true;
                }
            }
            return pressed;
        }
    }

    public static bool Jump
    {
        get
        {
            bool pressed = false;

            foreach (var k in instance.jumpKeys)
            {
                if (MarioInput.GetKey(k))
                {
                    pressed = true;
                }
            }
            return pressed;
        }
    }

    public static bool Run
    {
        get
        {
            bool pressed = false;

            foreach (var k in instance.runKeys)
            {
                if (MarioInput.GetKey(k))
                {
                    pressed = true;
                }
            }
            return pressed;
        }
    }

    public static bool AttackDown
    {
        get
        {
            bool pressed = false;

            foreach (var k in instance.attackKeys)
            {
                if (MarioInput.GetKeyDown(k))
                {
                    pressed = true;
                }
            }
            return pressed;
        }
    }

    public static float AxisX
    {
        get
        {
            float axisValue = 0f;

            bool pressed = false;

            foreach (var k in instance.rightKeys)
            {
                if (MarioInput.GetKey(k))
                {
                    pressed = true;
                }
            }

            if (pressed)
            {
                axisValue += 1f;
            }

            pressed = false;
            foreach (var k in instance.leftKeys)
            {
                if (MarioInput.GetKey(k))
                {
                    pressed = true;
                }
            }

            if (pressed)
            {
                axisValue -= 1f;
            }

            if (instance.XInputName.Trim() != "")
            {
                return Mathf.Clamp(axisValue + MarioInput.GetAxis(instance.XInputName), -1, 1);
            }
            else
            {
                return axisValue;
            }
        }

    }

    public static float AxisY
    {
        get
        {
            float axisValue = 0f;

            bool pressed = false;

            foreach (var k in instance.upKeys)
            {
                if (MarioInput.GetKey(k))
                {
                    pressed = true;
                }
            }

            if (pressed)
            {
                axisValue += 1f;
            }

            pressed = false;
            foreach (var k in instance.downKeys)
            {
                if (MarioInput.GetKey(k))
                {
                    pressed = true;
                }
            }

            if (pressed)
            {
                axisValue -= 1f;
            }

            if (instance.YInputName.Trim() != "")
            {
                return Mathf.Clamp(axisValue + MarioInput.GetAxis(instance.YInputName), -1, 1);
            }
            else
            {
                return axisValue;
            }
     
        }

    }
}
