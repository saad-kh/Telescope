using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class MomentumTrail2D : MonoBehaviour {

    public bool derailed { get; private set; } = true;
	public float time { get; private set; } = 0f;
	public float rememberTime { get; private set; } = 0f;
	public float foreseeTime { get; private set; } = 0f;


    Rigidbody2D body;

    void OnEnable()
    {
        body = GetComponent<Rigidbody2D>();
    }

    void FixedUpdate () {
        Debug.Log("HELLO");
	}

    public void SnapToTrack(float time, float rememberTime)
    {
        
    }

    public void Step(float time, float remmeberTime)
    {
        
    }

	public void Foresee(float time)
	{

	}
}
