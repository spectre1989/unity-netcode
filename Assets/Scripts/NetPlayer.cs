using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NetPlayer : NetObject
{
    public float MovementSpeed;

    public struct Input
    {
        public bool forward, back, left, right;
    }

    [NetSerialise]
    public Vector3 Pos
    {
        get { return this.transform.localPosition; }
        set { this.transform.localPosition = value; }
    }

    public void Move(Input input, float dt)
    {
        Vector3 direction = Vector3.zero;
        if (input.forward)
        {
            direction += Vector3.forward;
        }
        if (input.back)
        {
            direction -= Vector3.forward;
        }
        if (input.left)
        {
            direction -= Vector3.right;
        }
        if (input.right)
        {
            direction += Vector3.right;
        }

        Vector3 movement = direction.normalized * MovementSpeed;

        Vector3 newPosition = this.transform.localPosition + (movement * dt);
        newPosition.y = 1.0f;
        this.transform.localPosition = newPosition;
    }

    private void Start()
    {
        if (IsClient)
        {
            Transform camera = GetComponentInParent<Client>().Camera.transform;
            camera.parent = this.transform;
            camera.localPosition = new Vector3(0.0f, 1.0f, 0.0f);
        }
    }
}
