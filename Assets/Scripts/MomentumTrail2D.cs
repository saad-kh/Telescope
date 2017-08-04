using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace Telescope2D
{
    #region Delegate Definition
    public delegate void Collision2DEventDelegate(float time, Collision2D collision);
    public delegate void Trigger2DEventDelegate(float time, Collider2D collisions);
    #endregion

    [RequireComponent(typeof(Rigidbody2D))]
    public class MomentumTrail2D : MonoBehaviour
    {
        #region Scaffold
        Rigidbody2D body;

        void OnEnable()
        {
            body = GetComponent<Rigidbody2D>();
            Clean();
        }

        public void Clean()
        {
            if (momentums == null)
                momentums = new List<Momentum2D>(suggestedTrailCapacity);
            else
                momentums.Clear();

            momentums.Add(ExtractMomentum(Time.fixedTime));
        }
        #endregion

        #region Timeline
        public int suggestedTrailCapacity = 100;

        int index;
        List<Momentum2D> momentums;

        public void GoToTime(float time)
        {
            int indexForTime = MomentumIndexForTime(time);
            float momentumTime = momentums[indexForTime].time;

            if (!Mathf.Approximately(momentumTime, time))
            {
                int index0 = indexForTime;
                int index1 = indexForTime;

                if (momentumTime < time)
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

                if (index >= indexForTime)
                    index = index + 1;

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

            if (oldIndex != index)
            {
                ClearContacts();
                ApplyMomentum(momentums[index]);
                UnifyMomentums(oldIndex);

                if (index > 0)
                    UnifyMomentums(index - 1);
                if (index < momentums.Count - 1)
                    UnifyMomentums(index + 1);
            }

        }



        public IEnumerable<Momentum2D> MomentumIterator(float startTime = float.NegativeInfinity, float endTime = float.PositiveInfinity)
        {
            int i = !float.IsNegativeInfinity(startTime) ? MomentumIndexForTime(startTime) : 0;
            bool checkEnd = !float.IsPositiveInfinity(endTime);

            bool done = false;
            while (!done && i < momentums.Count)
            {
                Momentum2D momentum = momentums[i];
                if (    checkEnd
                   &&   momentum.time > endTime
                   &&   !Mathf.Approximately(momentum.time, endTime))
                    yield break;

                yield return momentum;
                i++;
            }
        }

        public Momentum2D MomentumForTime(float time)
        {
            int indexForTime = MomentumIndexForTime(time);
            Momentum2D momentum = momentums[indexForTime];

            if(     !Mathf.Approximately(momentum.time, time)
               &&   momentum.time < time 
               &&   indexForTime < momentums.Count - 1)
                return Momentum2D.InterpolateMomentum(
                    time,
                    momentum,
                    momentums[indexForTime + 1]
                );

            return momentum;
        }

        int MomentumIndexForTime(float time)
        {
            if (Mathf.Approximately(momentums[index].time, time))
                return index;

            if (index != 0
                && (time <= momentums[0].time
                    || Mathf.Approximately(momentums[0].time, time)))
                return 0;

            if (index != momentums.Count - 1
                && (time >= momentums[momentums.Count - 1].time
                    || Mathf.Approximately(momentums[momentums.Count - 1].time, time)))
                return momentums.Count - 1;

            float coeff = (time - momentums[0].time)
                          / (momentums[momentums.Count - 1].time - momentums[0].time);

            int indexForTime = (int)(Mathf.Clamp(coeff, 0, 1) * (momentums.Count - 1));

            float opMomentumTime = momentums[indexForTime].time;

            if (Mathf.Approximately(opMomentumTime, time))
                return indexForTime;

            if (opMomentumTime < time)
            {
                bool done = false;
                while (!done && indexForTime < momentums.Count - 1)
                {
                    float nextOpMomentumTime = momentums[indexForTime + 1].time;

                    if (Mathf.Approximately(nextOpMomentumTime, time))
                    {
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
                while (!done && indexForTime > 0)
                {
                    float prevOpMomentumTime = momentums[--indexForTime].time;
                    done = prevOpMomentumTime < time
                        || Mathf.Approximately(prevOpMomentumTime, time);
                }
                return indexForTime;
            }
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

        Momentum2D ExtractMomentum(float forTime)
        {
            return new Momentum2D(
                forTime,
                transform.position,
                transform.rotation,
                body.velocity,
                body.angularVelocity,
                body.IsSleeping(),
                collisionsEnter, collisionsStay, collisionsExit,
                triggersEnter, triggersStay, triggersExit
            );
        }

        void ApplyMomentum(Momentum2D momentum)
        {
            transform.position = momentum.position;
            transform.rotation = momentum.rotation;
            body.velocity = momentum.velocity;
            body.angularVelocity = momentum.angularVelocity;
            if (body.IsSleeping() && !momentum.sleeping)
                body.WakeUp();
            else if (body.IsAwake() && momentum.sleeping)
                body.Sleep();
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

            if (indexForTime < momentums.Count)
            {
                if (indexForTime == 0)
                    indexForTime++;

                int oldIndex = index;
                index = Mathf.Min(index, indexForTime - 1);

                momentums.RemoveRange(
                    indexForTime,
                    momentums.Count - indexForTime
                );

                if (oldIndex != index)
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

        void UnifyMomentums(int midIndex)
        {
            Debug.Assert(midIndex >= 0 && midIndex < momentums.Count);
            if (midIndex == index) return;

            Momentum2D momentum = momentums[midIndex];
            int index0 = midIndex;
            int index1 = midIndex;

            while (index0 != index
                   && index0 > 0
                   && momentums[index0 - 1].Same(momentum))
                index0--;

            while (index1 != index
                   && index1 < momentums.Count - 1
                   && momentums[index1 + 1].Same(momentum))
                index1++;

            int removeCount = index1 - (index0 + 1);
            if (removeCount > 0)
            {
                if (index >= index1)
                    index = index - removeCount;
                momentums.RemoveRange(index0 + 1, removeCount);
            }
        }
        #endregion

        #region Contacts
        public event Collision2DEventDelegate CollisionEnterEvent;
        public event Collision2DEventDelegate CollisionStayEvent;
        public event Collision2DEventDelegate CollisionExitEvent;
        public event Trigger2DEventDelegate TriggerEnterEvent;
        public event Trigger2DEventDelegate TriggerStayEvent;
        public event Trigger2DEventDelegate TriggerExitEvent;

        HashSet<Collision2D> collisionsEnter;
        HashSet<Collision2D> collisionsStay;
        HashSet<Collision2D> collisionsExit;
        HashSet<Collider2D> triggersEnter;
        HashSet<Collider2D> triggersStay;
        HashSet<Collider2D> triggersExit;

        void OnCollisionEnter2D(Collision2D collision)
        {
            if (collisionsEnter == null)
                collisionsEnter = new HashSet<Collision2D>();
            collisionsEnter.Add(collision);
        }

        void OnCollisionStay2D(Collision2D collision)
        {
            if (collisionsStay == null)
                collisionsStay = new HashSet<Collision2D>();
            collisionsStay.Add(collision);
        }

        void OnCollisionExit2D(Collision2D collision)
        {
            if (collisionsExit == null)
                collisionsExit = new HashSet<Collision2D>();
            collisionsExit.Add(collision);
        }

        void OnTriggerEnter2D(Collider2D collision)
        {
            if (triggersEnter == null)
                triggersEnter = new HashSet<Collider2D>();
            triggersEnter.Add(collision);
        }

        void OnTriggerStay2D(Collider2D collision)
        {
            if (triggersStay == null)
                triggersStay = new HashSet<Collider2D>();
            triggersStay.Add(collision);
        }

        void OnTriggerExit2D(Collider2D collision)
        {
            if (triggersExit == null)
                triggersExit = new HashSet<Collider2D>();
            triggersExit.Add(collision);
        }

        public void SendContactEvents()
        {
            Momentum2D momentum = momentums[index];

            if(CollisionEnterEvent != null)
                momentum.SendCollisionEnterEvents(CollisionEnterEvent);

			if (CollisionStayEvent != null)
				momentum.SendCollisionStayEvents(CollisionStayEvent);

			if (CollisionExitEvent != null)
				momentum.SendCollisionExitEvents(CollisionExitEvent);

            if (TriggerEnterEvent != null)
                momentum.SendTriggerEnterEvents(TriggerEnterEvent);

			if (TriggerStayEvent != null)
				momentum.SendTriggerStayEvents(TriggerStayEvent);
            
			if (TriggerExitEvent != null)
				momentum.SendTriggerExitEvents(TriggerExitEvent);
        }

        void ClearContacts()
        {
            collisionsEnter = collisionsStay = collisionsExit = null;
            triggersEnter = triggersStay = triggersExit = null;
        }
        #endregion

        #region Joints
        void OnJointBreak2D(Joint2D joint)
        {
            Debug.Assert(false, "TODO Handle Joint Breaks");
        }
        #endregion
    }

    public struct Momentum2D : IEquatable<Momentum2D>
    {
        public float time { get; }
        public Vector2 position { get; }
        public Quaternion rotation { get; }
        public Vector2 velocity { get; }
        public float angularVelocity { get; }
        public bool sleeping { get; }
        ISet<Collision2D> collisionsEnter;
        ISet<Collision2D> collisionsStay;
        ISet<Collision2D> collisionsExit;
        ISet<Collider2D> triggersEnter;
        ISet<Collider2D> triggersStay;
        ISet<Collider2D> triggersExit;


        public Momentum2D(
            float time,
            Vector2 position, Quaternion rotation,
            Vector2 velocity, float angularVelocity,
            bool sleeping,
            ISet<Collision2D> collisionsEnter = null, 
            ISet<Collision2D> collisionsStay = null,
            ISet<Collision2D> collisionsExit = null,
            ISet<Collider2D> triggersEnter = null, 
            ISet<Collider2D> triggersStay = null, 
            ISet<Collider2D> triggersExit = null
        )
        {
            this.time = time;
            this.position = position;
            this.rotation = rotation;
            this.velocity = velocity;
            this.angularVelocity = angularVelocity;
            this.sleeping = sleeping;
            this.collisionsEnter = collisionsEnter;
            this.collisionsStay = collisionsStay;
            this.collisionsExit = collisionsExit;
            this.triggersEnter = triggersEnter;
            this.triggersStay = triggersStay;
            this.triggersExit = triggersExit;
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
            sleeping = source.sleeping;
            collisionsEnter = source.collisionsEnter;
            collisionsStay = source.collisionsStay;
            collisionsExit = source.collisionsExit;
            triggersEnter = source.triggersEnter;
            triggersStay = source.triggersStay;
            triggersExit = source.triggersExit;
        }

        public IEnumerable<Collision2D> CollisionEnterIterator()
        {
            if (collisionsEnter == null || collisionsEnter.Count == 0)
                yield break;
            foreach (Collision2D collision in collisionsEnter)
                yield return collision;
        }

		public IEnumerable<Collision2D> CollisionStayIterator()
		{
			if (collisionsStay == null || collisionsStay.Count == 0)
				yield break;
			foreach (Collision2D collision in collisionsStay)
				yield return collision;
		}

		public IEnumerable<Collision2D> CollisionExitIterator()
		{
			if (collisionsExit == null || collisionsExit.Count == 0)
				yield break;
			foreach (Collision2D collision in collisionsExit)
				yield return collision;
		}

		public IEnumerable<Collider2D> TriggerEnterIterator()
		{
			if (triggersEnter == null || triggersEnter.Count == 0)
				yield break;
			foreach (Collider2D collision in triggersEnter)
				yield return collision;
		}

		public IEnumerable<Collider2D> TriggerStayIterator()
		{
			if (triggersStay == null || triggersStay.Count == 0)
				yield break;
			foreach (Collider2D collision in triggersStay)
				yield return collision;
		}

        public IEnumerable<Collider2D> TriggerExitIterator()
		{
			if (triggersExit == null || triggersExit.Count == 0)
				yield break;
            foreach (Collider2D collision in triggersExit)
				yield return collision;
		}

        public void SendCollisionEnterEvents(Collision2DEventDelegate collision2DEventDelegate)
        {
			if (    collision2DEventDelegate == null
				||  collisionsEnter == null || collisionsEnter.Count == 0)
                return;
            
            foreach (Collision2D collision in collisionsEnter)
                collision2DEventDelegate(time, collision);
        }

        public void SendCollisionStayEvents(Collision2DEventDelegate collision2DEventDelegate)
        {
			if (    collision2DEventDelegate == null
				||  collisionsStay == null || collisionsStay.Count == 0)
                return;

            foreach (Collision2D collision in collisionsStay)
                collision2DEventDelegate(time, collision);
        }


        public void SendCollisionExitEvents(Collision2DEventDelegate collision2DEventDelegate)
        {
			if (    collision2DEventDelegate == null
				||  collisionsExit == null || collisionsExit.Count == 0)
                return;

            foreach (Collision2D collision in collisionsExit)
                collision2DEventDelegate(time, collision);
        }

		public void SendTriggerEnterEvents(Trigger2DEventDelegate trigger2DEventDelegate)
		{
			if (    trigger2DEventDelegate == null
				||  triggersEnter == null || triggersEnter.Count == 0)
				return;

            foreach (Collider2D collision in triggersEnter)
				trigger2DEventDelegate(time, collision);
		}

		public void SendTriggerStayEvents(Trigger2DEventDelegate trigger2DEventDelegate)
		{
            if (    trigger2DEventDelegate == null 
                ||  triggersStay == null || triggersStay.Count == 0)
				return;

			foreach (Collider2D collision in triggersStay)
				trigger2DEventDelegate(time, collision);
		}

		public void SendTriggerExitEvents(Trigger2DEventDelegate trigger2DEventDelegate)
		{
            if (    trigger2DEventDelegate == null 
                ||  triggersExit == null || triggersExit.Count == 0)
				return;

			foreach (Collider2D collision in triggersExit)
				trigger2DEventDelegate(time, collision);
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
                && x.position == y.position
                && x.rotation == y.rotation
                && x.velocity == y.velocity
                && x.sleeping == y.sleeping
                && Mathf.Approximately(x.angularVelocity, y.angularVelocity)
                && EqualsContacts(x, y)
            );
        }

        public static bool Same(Momentum2D x, Momentum2D y)
        {
            return (
                    x.position == y.position
                && x.rotation == y.rotation
                && x.velocity == y.velocity
                && x.sleeping == y.sleeping
                && Mathf.Approximately(x.angularVelocity, y.angularVelocity)
                && SameContacts(x, y)
            );
        }

        public static bool operator !=(Momentum2D x, Momentum2D y)
        {
            return !(x == y);
        }

        public static bool EqualsContacts(Momentum2D x, Momentum2D y)
        {
            return (
                    x.collisionsEnter == y.collisionsEnter
                &&  x.collisionsStay == y.collisionsStay
                &&  x.collisionsExit == y.collisionsExit
                &&  x.triggersEnter == y.triggersEnter
                &&  x.triggersStay == y.triggersStay
                &&  x.triggersExit == y.triggersExit
            );
        }

        public static bool SameContacts(Momentum2D x, Momentum2D y)
        {
            return (
                    SameSets(x.collisionsEnter, y.collisionsEnter)
                && SameSets(x.collisionsStay, y.collisionsStay)
                && SameSets(x.collisionsExit, y.collisionsExit)
                && SameSets(x.triggersEnter, y.triggersEnter)
                && SameSets(x.triggersStay, y.triggersStay)
                && SameSets(x.triggersExit, y.triggersExit)
            );
        }

        public static bool SameSets<T>(ISet<T> setX, ISet<T> setY)
        {
            if (setX == null && setY == null) return true;
            if (setX == null || setY == null) return false;
            if (setX.Count != setY.Count) return false;
            return !setX.Except(setY).Any();
        }

        public static Momentum2D InterpolateMomentum(float t, Momentum2D a, Momentum2D b)
        {
            if (t >= b.time
                || Mathf.Approximately(a.time, b.time)
                || Mathf.Approximately(t, b.time))
                return new Momentum2D(t, b);

            if (t <= a.time
                || Mathf.Approximately(t, a.time))
                return new Momentum2D(t, a);

            float nt = (t - a.time) / (b.time - a.time);
            return new Momentum2D(
                t,
                Vector2.LerpUnclamped(a.position, b.position, nt),
                Quaternion.LerpUnclamped(a.rotation, b.rotation, nt),
                Vector2.LerpUnclamped(a.velocity, b.velocity, nt),
                Mathf.LerpUnclamped(a.angularVelocity, b.angularVelocity, nt),
                a.sleeping && b.sleeping,
                null, a.collisionsStay, null,
                null, a.triggersStay, null
            );
        }
    }
}