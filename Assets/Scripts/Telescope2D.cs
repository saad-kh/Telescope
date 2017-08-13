using System.Collections.Generic;
using UnityEngine;

namespace Telescope2D
{
	public class Telescope2D : MonoBehaviour
	{

        public bool remembering = true;
        public bool foreseeing = true;
		public float remember = 1f;
		public float foresee = 1f;
		public float foreseeChunk = 0.1f;

		public bool clearSight { get; private set; } = false;
        public bool clearKnowledge { get; private set; } = false;
        public float time { get; private set; } = 0f;
		public float remembered { get; private set; } = 0f;
		public float foreseen { get; private set; } = 0f;

		HashSet<MomentumTrail2D> trails;

        // Use this for initialization
        void Awake()
        {
            trails = new HashSet<MomentumTrail2D>();
        }

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
            uint simulatedTick = (uint)(simulatedTime / Time.fixedDeltaTime);

			remembered = Mathf.Max(
                time - (remembering ? remember : 0),
				remembered
			);

			foreseen = Mathf.Min(
                simulatedTime + (foreseeing ? foreseeChunk : Time.fixedDeltaTime),
				time + foresee
			);
            uint foreseenTick = (uint)(foreseen / Time.fixedDeltaTime);

            while (simulatedTick < foreseenTick)
			{
                simulatedTick++;
				foreach (MomentumTrail2D trail in trails)
                    trail.BeginSimulation(simulatedTick);

                Physics2D.Simulate(Time.fixedDeltaTime);

				foreach (MomentumTrail2D trail in trails)
                {
                    trail.EndSimulation(simulatedTick);
                    trail.SendContactEvents(simulatedTick);
                }
			}
            uint tick = (uint)(time / Time.fixedDeltaTime);
            uint keepTicks = tick - (uint)(remembered / Time.fixedDeltaTime);
            foreach (MomentumTrail2D trail in trails)
            {
                trail.GoToTime(tick, keepTicks);
                trail.SendContactEvents(tick);
            }
                
		}

        public void BlurSight()
		{
			clearSight = false;
		}

        public void BlurKnowledge()
        {
            clearSight = false;
            clearKnowledge = false;
        }

        void CleanGlass(float currentTime)
		{
			foreseen = currentTime;
            
            if(!clearKnowledge)
            {
                trails.Clear();
                foreach (Rigidbody2D body in FindObjectsOfType(typeof(Rigidbody2D)))
                {
                    MomentumTrail2D trail = body.GetComponent<MomentumTrail2D>();
                    if (trail == null)
                        trail = body.gameObject.AddComponent<MomentumTrail2D>();

                    trails.Add(trail);
                }
                clearKnowledge = true;
            }
			clearSight = true;
		}

	}

}