using System;
using System.Collections.Generic;
using UnityEngine;

namespace Telescope2D
{
	[RequireComponent(typeof(Rigidbody2D))]
	public class MomentumTrail2D : MonoBehaviour
	{

		public float time { get; private set; } = 0f;

		Rigidbody2D body;
		int currentIndex;
		List<Momentum2D> momentums;

		void OnEnable()
		{
			body = GetComponent<Rigidbody2D>();
			if (momentums == null)
			{
				momentums = new List<Momentum2D>();
				momentums.Add(ExtractMomentum(Time.fixedTime));
			}
		}

        public void Step(float toTime)
		{
			float currentMomentumTime = momentums[currentIndex].time;
			bool synced = false;
			if (Mathf.Approximately(currentMomentumTime, toTime))
			{
				momentums[currentIndex] = ExtractMomentum(toTime);
				time = toTime;
				synced = true;
			}
			else if (currentMomentumTime > toTime)
			{
				momentums[currentIndex] = ExtractMomentum(toTime);
				time = toTime;
				synced = true;
				if (currentIndex < momentums.Count - 1)
					momentums.RemoveRange(currentIndex + 1, momentums.Count - currentIndex);
			}
			else
			{
				if (currentIndex == momentums.Count - 1)
				{
					momentums.Add(ExtractMomentum(toTime));
					currentIndex++;
					time = toTime;
                    synced = true;
				}
				else
				{
					bool done = false;
					bool found = false;
					int nextIndex = currentIndex + 1;
					while (!done && nextIndex < momentums.Count)
					{
						float nextMomentumTime = momentums[nextIndex].time;
						if (Mathf.Approximately(nextMomentumTime, toTime))
						{
							currentIndex = nextIndex;
							found = done = true;
						}
						else if (nextMomentumTime > toTime)
							done = true;
						else
							nextIndex++;
					}

					if (!found)
					{
						if (currentIndex < momentums.Count - 1)
						{
							momentums.Insert(
								currentIndex + 1,
								Momentum2D.InterpolateMomentum(
									toTime,
									momentums[currentIndex],
									momentums[currentIndex + 1]
								)
							);
							currentIndex++;
						}
						else if (currentIndex > 0)
						{
							momentums.Insert(
								currentIndex + 1,
								Momentum2D.InterpolateMomentum(
									toTime,
									momentums[currentIndex - 1],
									momentums[currentIndex]
								)
							);
							currentIndex++;
						}
						else
                        {
                            momentums.Add(ExtractMomentum(toTime));
                            currentIndex++;
                            time = toTime;
                            synced = true;
                        }
							
					}
				}
			}

			if (!synced)
			{
				ApplyMomentum(momentums[currentIndex]);
				time = toTime;
			}

		}

		public void BeginSimulation(float atTime)
		{
			bool found = false;
			bool done = false;
			bool synced = false;
			int futurIndex = momentums.Count - 1;
			while (!done && futurIndex >= 0)
			{
				float futurMomentumTime = momentums[futurIndex].time;
				if (Mathf.Approximately(futurMomentumTime, atTime))
				{
					synced = currentIndex == futurIndex;
					done = found = true;
				}
				else if (futurMomentumTime < atTime)
					done = true;
				else
					futurIndex--;
			}

			if (!found)
			{
				if (futurIndex == 0)
				{
					momentums.Clear();
					momentums.Add(ExtractMomentum(atTime));
					currentIndex = 0;
					time = atTime;
					synced = true;
				}
				else
				{
					if (futurIndex < momentums.Count - 1)
						momentums.Insert(
							futurIndex + 1,
                            Momentum2D.InterpolateMomentum(
								atTime,
								momentums[futurIndex],
								momentums[futurIndex + 1]
							)
						);
					else
						momentums.Insert(
							futurIndex + 1,
							Momentum2D.InterpolateMomentum(
								atTime,
								momentums[futurIndex - 1],
								momentums[futurIndex]
							)
						);
					futurIndex++;
				}
			}

            if (futurIndex < momentums.Count - 1) 
                momentums.RemoveRange(futurIndex + 1, momentums.Count - (futurIndex + 1));

            if (futurIndex < currentIndex)
                currentIndex = futurIndex;

			if (!synced)
			{
				ApplyMomentum(momentums[momentums.Count - 1]);
				time = atTime;
			}

		}

		public void EndSimulation(float atTime)
		{
			bool done = false;
			int pastIndex = momentums.Count - 1;
			while (!done && pastIndex >= 0)
			{
				float futurMomentumTime = momentums[pastIndex].time;
				if (futurMomentumTime < atTime
				   && !Mathf.Approximately(futurMomentumTime, atTime))
					done = true;
				else
					pastIndex--;
			}

			if (pastIndex < momentums.Count - 2)
			{
                if (pastIndex < 0) pastIndex = 0;
                
                momentums.RemoveRange(pastIndex + 1, momentums.Count - (pastIndex + 1));

                if (currentIndex > pastIndex)
					currentIndex = pastIndex;
			}

			momentums.Add(ExtractMomentum(atTime));

			ApplyMomentum(momentums[currentIndex]);
			time = momentums[currentIndex].time;
		}

		Momentum2D ExtractMomentum(float forTime)
		{
			return new Momentum2D(
				forTime,
				transform.position,
				transform.rotation,
				body.velocity,
				body.angularVelocity
			);
		}

		void ApplyMomentum(Momentum2D momentum)
		{
			transform.position = momentum.position;
			transform.rotation = momentum.rotation;
			body.velocity = momentum.velocity;
			body.angularVelocity = momentum.angularVelocity;
		}
	}

    public struct Momentum2D : IEquatable<Momentum2D>
	{
		public float time { get; }
		public Vector2 position { get; }
		public Quaternion rotation { get; }
		public Vector2 velocity { get; }
		public float angularVelocity { get; }

		public Momentum2D(
			float time,
			Vector2 position,
			Quaternion rotation,
			Vector2 velocity,
			float angularVelocity
		)
		{
			this.time = time;
			this.position = position;
			this.rotation = rotation;
			this.velocity = velocity;
			this.angularVelocity = angularVelocity;
		}

        public bool Equals(Momentum2D momentum2D)
        {
            return this == momentum2D;
        }

		public override bool Equals(System.Object obj)
		{
            return obj is Momentum2D && this == (Momentum2D)obj;
		}

		public override int GetHashCode()
		{
            return time.GetHashCode();
		}

		public static bool operator ==(Momentum2D x, Momentum2D y)
		{
            return (
                    Mathf.Approximately(x.time, y.time)
                &&  x.position == y.position
                &&  x.rotation == y.rotation
                &&  x.velocity == y.velocity
                &&  Mathf.Approximately(x.angularVelocity, y.angularVelocity)
            );
		}

		public static bool operator !=(Momentum2D x, Momentum2D y)
		{
			return !(x == y);
		}

		public static Momentum2D InterpolateMomentum(float t, Momentum2D a, Momentum2D b)
		{
			float nt = (t - a.time) / (t - b.time);
			return new Momentum2D(
				t,
				Vector2.LerpUnclamped(a.position, b.position, nt),
				Quaternion.LerpUnclamped(a.rotation, b.rotation, nt),
				Vector2.LerpUnclamped(a.velocity, b.velocity, nt),
				Mathf.LerpUnclamped(a.angularVelocity, b.angularVelocity, nt)
			);
		}
	}
}