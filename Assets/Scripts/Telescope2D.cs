using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Telescope2D : MonoBehaviour {

	public float remember = 1f;
	public float foresee = 1f;
    public float foreseeChunk = 0.1f;

    public bool clearSight { get; private set; } = false;
    public float time { get; private set; } = 0f;
    public float rememberTime { get; private set; } = 0f;
    public float foreseeTime { get; private set; } = 0f;

    HashSet<MomentumTrail2D> trails;

	// Use this for initialization
	void OnEnable () {
        if(Physics2D.autoSimulation)
        {
            Debug.Log($"Automatically deactivating {nameof(Physics2D.autoSimulation)}");
            Physics2D.autoSimulation = false;
        }
        CleanGlass(Time.fixedTime);
	}

    // Update is called once per frame
    void FixedUpdate () {
        Debug.Assert(
            !Physics2D.autoSimulation,
            $"{nameof(Physics2D.autoSimulation)} should be deactivated" 
        );

        Debug.Assert(
            foresee >= 0 && foreseeChunk >= 0 && remember >= 0,
            $"Timeline manipulation properties ${nameof(foresee)}, ${nameof(foreseeChunk)} and ${nameof(remember)} should both greater than 0"
        );

        time = Time.fixedTime;
        if (!clearSight)
            CleanGlass(time);

		float simulatedTime = foreseeTime;

		rememberTime = Mathf.Max(
            time - remember,
            rememberTime
        );

		foreseeTime = Mathf.Min(
			time + foreseeChunk,
			time + foresee
		);

        while (simulatedTime < foreseeTime)
        {
            if (Mathf.Approximately(simulatedTime, foreseeTime))
                simulatedTime = foreseeTime;
            else
            {
				Physics2D.Simulate(Time.fixedDeltaTime);
				simulatedTime += Time.fixedDeltaTime;
                foreach(MomentumTrail2D trail in trails)
                {
                    if (    simulatedTime <= time
                        ||  Mathf.Approximately(simulatedTime, time))
                        trail.Step(simulatedTime, rememberTime);
                    else
                        trail.Foresee(simulatedTime);
                        
                }
            }
        }



        Debug.Log(
                $"{nameof(Time.time)} {Time.time:N4} "
            +   $"{nameof(Time.deltaTime)} {Time.deltaTime:N4} "
            +   $"{nameof(Time.fixedTime)} {Time.fixedTime:N4} "
            +   $"{nameof(Time.fixedDeltaTime)} {Time.fixedDeltaTime:N4} "
            +   $"{nameof(time)} {time:N4} "
            +   $"{nameof(rememberTime)} {rememberTime:N4} "
            +   $"{nameof(foreseeTime)} {foreseeTime:N4} "
        );
	}

	public void BlurSight()
	{
		clearSight = false;
	}

    void CleanGlass(float currentTime)
    {
        foreseeTime = currentTime;

        trails = new HashSet<MomentumTrail2D>();
        foreach(Rigidbody2D body in FindObjectsOfType(typeof(Rigidbody2D)))
        {
			MomentumTrail2D trail = body.GetComponent<MomentumTrail2D>();
			if (trail == null)
				trail = body.gameObject.AddComponent<MomentumTrail2D>();
			trails.Add(trail);

            trail.SnapToTrack(currentTime, rememberTime);
        }

        clearSight = true;
    }

}
