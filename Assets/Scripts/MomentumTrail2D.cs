using System;
using System.Collections.Generic;
using UnityEngine;

namespace Telescope2D
{
	[RequireComponent(typeof(Rigidbody2D))]
	public class MomentumTrail2D : MonoBehaviour
	{
        public int suggestedTrailCapacity = 100;

		Rigidbody2D body;
		int index;
		List<Momentum2D> momentums;

		void OnEnable()
		{
			body = GetComponent<Rigidbody2D>();
			if (momentums == null)
			{
                momentums = new List<Momentum2D>(suggestedTrailCapacity);
				momentums.Add(ExtractMomentum(Time.fixedTime));
			}
		}

        public void GoToTime(float time)
        {
            int indexForTime = MomentumIndexForTime(time);
            float momentumTime = momentums[indexForTime].time;

            if (!Mathf.Approximately(momentumTime, time))
            {
                int index0 = indexForTime;
                int index1 = indexForTime;

                if(momentumTime < time)
                {
					if (indexForTime < momentums.Count - 1)
						index1 = indexForTime + 1;
					else if (indexForTime > 0)
						index0 = indexForTime - 1;
                    indexForTime++;
                }
                else
                {
                    if (indexForTime > 0)
                        index0 = indexForTime - 1;
                    else if (indexForTime < momentums.Count - 1)
                        index1 = indexForTime + 1;
                }

                momentums.Insert(
                    indexForTime,
                    Momentum2D.InterpolateMomentum(
                        time,
                        momentums[index0],
                        momentums[index1]
                    )
                );
            }

            int oldIndex = index;
			index = indexForTime;
			ApplyMomentum(momentums[index]);

			UnifyMomentums(oldIndex);
            if (index > 0)
                UnifyMomentums(index - 1);
            if (index < momentums.Count - 1)
                UnifyMomentums(index + 1);
        }

        public void DigestMomentum(float time)
        {
            Momentum2D momentum = ExtractMomentum(time);
			int indexForTime = MomentumIndexForTime(time);
			float momentumTime = momentums[indexForTime].time;

            if (!Mathf.Approximately(momentumTime, time))
            {
                indexForTime = momentumTime < time 
                    ? indexForTime + 1
                    : indexForTime;
                
                index = indexForTime <= index
                    ? index + 1
                    : index;
                    
				momentums.Insert(
					indexForTime,
					momentum
				);
            }
            else
                momentums[indexForTime] = momentum;
            
            UnifyMomentums(indexForTime);
        }

        public int PurgeMomentumsNewerThan(float time)
        {
            int indexForTime = MomentumIndexForTime(time);

            bool done = false;
            while (!done && indexForTime < momentums.Count)
            {
                float momentumTime = momentums[indexForTime].time;
                if (momentumTime > time && !Mathf.Approximately(momentumTime, time))
                    done = true;
                else
                    indexForTime++;
            }

            if (indexForTime < momentums.Count){
				if (indexForTime == 0)
					indexForTime++;
                
                int oldIndex = index;
                index = Mathf.Min(index, indexForTime - 1);
                
                momentums.RemoveRange(
                    indexForTime,
                    momentums.Count - indexForTime
                );

                if(oldIndex != index)
                    ApplyMomentum(momentums[index]);

                UnifyMomentums(momentums.Count - 1);
            }

            return momentums.Count - indexForTime;
        }

		public int PurgeMomentumsOlderThan(float time)
		{
			int indexForTime = MomentumIndexForTime(time);

			bool done = false;
			while (!done && indexForTime >= 0)
			{
				float momentumTime = momentums[indexForTime].time;
				if (momentumTime < time && !Mathf.Approximately(momentumTime, time))
					done = true;
				else
					indexForTime--;
			}

			if (indexForTime >= 0)
			{
                if (indexForTime == momentums.Count - 1)
                    indexForTime--;
                
                int oldIndex = index;
                index = Mathf.Max(index - indexForTime, 0);
				
                momentums.RemoveRange(
					0,
					indexForTime + 1
				);

				if (oldIndex != index)
					ApplyMomentum(momentums[index]);

                UnifyMomentums(0);
			}

            return indexForTime + 1;
		}


        int MomentumIndexForTime(float time)
        {
            if (    Mathf.Approximately(momentums[index].time, time))
                return index;

			if (    index != 0
				&&  (   time <= momentums[0].time
                    ||  Mathf.Approximately(momentums[0].time, time)))
				return 0;

            if (    index != momentums.Count - 1
				&&  (   time >= momentums[momentums.Count - 1].time
			        ||  Mathf.Approximately(momentums[momentums.Count - 1].time, time)))
                return momentums.Count - 1;

            float coeff =   (time - momentums[0].time) 
                          / (momentums[momentums.Count - 1].time - momentums[0].time);

            int indexForTime = (int)(Mathf.Clamp(coeff, 0, 1) * (momentums.Count - 1));

			float opMomentumTime = momentums[indexForTime].time;

            if (Mathf.Approximately(opMomentumTime, time))
                return indexForTime;
            
            if (opMomentumTime < time)
            {
                bool done = false;
                while(!done && indexForTime  < momentums.Count - 1)
                {
                    float nextOpMomentumTime = momentums[indexForTime + 1].time;

                    if (Mathf.Approximately(nextOpMomentumTime, time)){
                        indexForTime++;
                        done = true;
                    }
                    else if (nextOpMomentumTime > time)
                        done = true;
                    else
                        indexForTime++;
                }
                return indexForTime;
            }
            else
            {
                bool done = false;
                while(!done && indexForTime > 0)
                {
                    float prevOpMomentumTime = momentums[--indexForTime].time;
                    done = prevOpMomentumTime < time
                        || Mathf.Approximately(prevOpMomentumTime, time);
                }
				return indexForTime;
            }
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

        void UnifyMomentums(int midIndex)
        {
            Debug.Assert(midIndex >= 0 && midIndex < momentums.Count);
            if (midIndex == index) return;

			Momentum2D momentum = momentums[midIndex];
            int index0 = midIndex;
            int index1 = midIndex;

            while (     index0 != index
                   &&   index0 > 0
                   &&   momentums[index0 - 1].Same(momentum))
                index0--;

            while (     index1 != index
				   &&   index1 < momentums.Count - 1
				   &&   momentums[index1 + 1].Same(momentum))
				index1++;

            int removeCount = index1 - (index0 + 1);
            if(removeCount > 0)
            {
                if (index >= index1)
                    index = index - removeCount;
                momentums.RemoveRange(index0 + 1, removeCount);
            }
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

		public Momentum2D(
			float time,
            Momentum2D source
		)
		{
			this.time = time;
			position = source.position;
			rotation = source.rotation;
			velocity = source.velocity;
			angularVelocity = source.angularVelocity;
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

        public bool Same(Momentum2D other)
        {
            return Same(this, other);
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

		public static bool Same(Momentum2D x, Momentum2D y)
		{
			return (
                    x.position == y.position
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
            if (    t >= b.time    
                ||  Mathf.Approximately(a.time, b.time)
                ||  Mathf.Approximately(t, b.time))
                return new Momentum2D(t, b);

            if (    t <= a.time
                ||  Mathf.Approximately(t, a.time))
                return new Momentum2D(t, a);
            
            float nt = (t - a.time) / (b.time - a.time);
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