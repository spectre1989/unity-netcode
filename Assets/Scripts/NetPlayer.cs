using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NetPlayer : NetObject
{
    public float MovementSpeed;
    public float MouseSensitivity;
    public float MinPitch;
    public float MaxPitch;
    public Transform Head;

    public struct Input
    {
        public bool forward, back, left, right;
        public float pitch;
        public float yaw;
    }

    [NetSerialise]
    public Vector3 Pos
    {
        get { return this.transform.localPosition; }
        set { this.transform.localPosition = value; }
    }

    public void Move(Input input, float dt)
    {
        this.transform.localEulerAngles = new Vector3(this.transform.localEulerAngles.x, input.yaw, this.transform.localEulerAngles.z);
        input.pitch = Mathf.Clamp(input.pitch, MinPitch, MaxPitch);
        Head.localEulerAngles = new Vector3(input.pitch, Head.localEulerAngles.y, Head.localEulerAngles.z);

        Vector3 localForward = new Vector3(Head.forward.x, 0.0f, Head.forward.z).normalized;
        Vector3 localRight = new Vector3(Head.right.x, 0.0f, Head.right.z).normalized;

        Vector3 direction = Vector3.zero;
        if (input.forward)
        {
            direction += localForward;
        }
        if (input.back)
        {
            direction -= localForward;
        }
        if (input.left)
        {
            direction -= localRight;
        }
        if (input.right)
        {
            direction += localRight;
        }

        Vector3 movement = direction.normalized * MovementSpeed;

        Vector3 newPosition = this.transform.localPosition + (movement * dt);
        newPosition.y = 0.0f;
        this.transform.localPosition = newPosition;
    }
}
