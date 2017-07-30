using System.Collections.Generic;
using UnityEngine;

namespace Telescope2D
{
	public class Telescope2D : MonoBehaviour
	{

		public float remember = 1f;
		public float foresee = 1f;
		public float foreseeChunk = 0.1f;

		public bool clearSight { get; private set; } = false;
		public float time { get; private set; } = 0f;
		public float remembered { get; private set; } = 0f;
		public float foreseen { get; private set; } = 0f;

		HashSet<MomentumTrail2D> trails;

		// Use this for initialization
		void OnEnable()
		{
			if (Physics2D.autoSimulation)
			{
				Debug.Log($"Automatically deactivating {nameof(Physics2D.autoSimulation)}");
				Physics2D.autoSimulation = false;
			}
			CleanGlass(Time.fixedTime);
		}

		// Update is called once per frame
		void FixedUpdate()
		{
			Debug.Assert(
				!Physics2D.autoSimulation,
				$"{nameof(Physics2D.autoSimulation)} should be deactivated"
			);

			Debug.Assert(
				foresee >= 0 && foreseeChunk >= 0 && remember >= 0,
				$"Timeline manipulation properties ${nameof(foresee)}, ${nameof(foreseeChunk)} and ${nameof(remember)} should both be greater than 0"
			);

			time = Time.fixedTime;
			if (!clearSight)
				CleanGlass(time);

			float simulatedTime = foreseen;

			remembered = Mathf.Max(
                time - remember,
				remembered
			);

			foreseen = Mathf.Min(
				simulatedTime + foreseeChunk,
				time + foresee
			);

            foreach (MomentumTrail2D trail in trails)
            {
				trail.PurgeMomentumsOlderThan(remembered);
				trail.PurgeMomentumsNewerThan(simulatedTime);
            }

			bool done = simulatedTime >= foreseen
					|| Mathf.Approximately(simulatedTime, foreseen);
            
            while (!done)
			{
				foreach (MomentumTrail2D trail in trails)
					trail.GoToTime(simulatedTime);
				
                Physics2D.Simulate(Time.fixedDeltaTime);
				simulatedTime += Time.fixedDeltaTime;

				foreach (MomentumTrail2D trail in trails)
                    trail.DigestMomentum(simulatedTime);

				done = simulatedTime >= foreseen
	                || Mathf.Approximately(simulatedTime, foreseen);
			}
            foreseen = simulatedTime;

            foreach (MomentumTrail2D trail in trails)
                trail.GoToTime(time);
		}

		public void BlurSight()
		{
			clearSight = false;
		}

		void CleanGlass(float currentTime)
		{
			foreseen = currentTime;

			trails = new HashSet<MomentumTrail2D>();
			foreach (Rigidbody2D body in FindObjectsOfType(typeof(Rigidbody2D)))
			{
				MomentumTrail2D trail = body.GetComponent<MomentumTrail2D>();
				if (trail == null)
					trail = body.gameObject.AddComponent<MomentumTrail2D>();
				trails.Add(trail);
			}

			clearSight = true;
		}

	}

}