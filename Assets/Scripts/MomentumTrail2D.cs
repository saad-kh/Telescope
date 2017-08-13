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

        void Start()
        {
            body = GetComponent<Rigidbody2D>();
			momentums = new List<Momentum2D[]>(1);
			currentIndex = lastIndex = TrailIndex.zero;
			Momentum2D[] segment = new Momentum2D[segmentSize];
			segment[0] = ExtractMomentum((uint)(Time.fixedTime / Time.fixedDeltaTime));
			momentums.Add(segment);
        }

        #endregion

        #region Timeline
        public uint segmentSize = 20;

        TrailIndex currentIndex;
        TrailIndex lastIndex;
        List<Momentum2D[]> momentums;
        public Momentum2D first { get { return this[TrailIndex.zero]; } }
        public Momentum2D current { get { return this[currentIndex]; } }
        public Momentum2D last { get { return this[lastIndex]; } }
        Momentum2D this[TrailIndex i]
        {
            get { return momentums[i.segment][i.momentum]; }
            set { momentums[i.segment][i.momentum] = value; }
        }

        public bool GoToTime(uint tick, uint keepTick = 0)
        {
            TrailIndex i;
            if(this[currentIndex].tick + 1 == tick)
            {
                if (currentIndex.momentum < momentums[currentIndex.segment].Length - 1)
                {
                    if (momentums[currentIndex.segment][currentIndex.momentum + 1] != null)
                        i = new TrailIndex(currentIndex.segment, currentIndex.momentum + 1);
                    else
                        return false;
                }
                else
                {
                    if (currentIndex.segment < momentums.Count - 1)
                        i = new TrailIndex(currentIndex.segment + 1, 0);
                    else
                        return false;
                }
            }
            else
                i = MomentumIndexForTick(tick);

            bool success = GoToIndex(i);

            uint pastTick = Math.Max(tick - keepTick, 0);
            int pastSegI = 0;
            while (pastSegI + 1 <= i.segment
                  && momentums[pastSegI + 1][0].tick <= pastTick)
                pastSegI++;

            if (pastSegI > 0)
            {
                currentIndex = new TrailIndex(
                    currentIndex.segment - pastSegI,
                    currentIndex.momentum
                );
                lastIndex = new TrailIndex(
                    lastIndex.segment - pastSegI,
                    lastIndex.momentum
                );

                if (!Momentum2D.IsPoolFull())
                    for (int segI = 0; segI < pastSegI; segI++)
                    {
                        Momentum2D.Release(momentums[segI]);
                        if (Momentum2D.IsPoolFull()) break;
                    }

                momentums.RemoveRange(0, pastSegI);
            }

            return success;
        }

        private bool GoToIndex(TrailIndex i)
        {
            if (i.isInvalid) return false;

            if (i != currentIndex)
            {
                ClearContacts();
                currentIndex = i;
                ApplyMomentum(this[currentIndex]);
            }

            return true;
        }

        public IEnumerable<Momentum2D> MomentumIterator(uint startTick = 0, uint endTick = uint.MaxValue)
        {
            TrailIndex startIndex = startTick > 0 ? MomentumIndexForTick(startTick) : TrailIndex.zero;
            if (startIndex.isInvalid) yield break;

            bool checkEnd = endTick != uint.MaxValue;
            int segI = startIndex.segment;
            int momI = startIndex.momentum;
			Momentum2D[] segment = momentums[segI];
            while (  segI < lastIndex.segment 
                  || (segI == lastIndex.segment && momI <= lastIndex.momentum))
            {
                if (   checkEnd
                    && segment[momI].tick > endTick)
                    yield break;

                yield return segment[momI];

                if (segI == lastIndex.segment && momI == lastIndex.momentum)
                    yield break;

                if (   momI < segment.Length - 1
                    && segment[momI + 1] != null)
                    momI++;
                else
                {
                    segment = momentums[++segI];
                    momI = 0;
                }
            }
        }

        public Momentum2D MomentumForTick(uint tick)
        {
            TrailIndex i = MomentumIndexForTick(tick);
            if (!i.isInvalid) throw new ArgumentOutOfRangeException(
                $"{nameof(tick)} should be higher than the oldest {nameof(tick)} of {nameof(MomentumTrail2D)}"
               );
            return this[i];
        }

        TrailIndex MomentumIndexForTick(uint tick)
        {
            if (current.tick == tick)
                return currentIndex;

            if (first.tick == tick)
                return TrailIndex.zero;

            if (first.tick > tick)
                return TrailIndex.invalid;

            if (last.tick <= tick)
                return lastIndex;

            int segI = currentIndex.segment;
            int momI = currentIndex.momentum;
            bool done = false;
            while(!done)
            {
                if (momentums[segI][0].tick == tick)
                    done = true;
                if (momentums[segI][0].tick < tick)
                {
                    if (  segI < momentums.Count - 1
                       && momentums[segI + 1][0].tick <= tick)
                    {
                        segI++; 
                        momI = 0;
                    }
                    else
                        done = true;
                }
                else
                {
					if (  segI  > 0
					   && momentums[segI - 1][0].tick <= tick)
					{
						segI--;
						momI = 0;
					}
					else
						done = true;
                }
            }

            done = false;
            Momentum2D[] segment = momentums[segI];
            while (!done)
            {
                if (segment[momI].tick == tick)
                    done = true;
                else if(segment[momI].tick < tick)
                {
                    if (  momI < segment.Length - 1
                       && segment[momI + 1] != null
                       && segment[momI + 1].tick <= tick)
                        momI++;
					else
						done = true;
                }
                else
                {
                    if (   momI > 0
                        && segment[momI - 1].tick <= tick)
                        momI--;
					else
						done = true;
                }
            }

            return new TrailIndex(segI, momI);
        }

        public void BeginSimulation(uint tick)
        {
            lastIndex = MomentumIndexForTick(tick);
            if(lastIndex.isInvalid) lastIndex = TrailIndex.zero;

            if(currentIndex != lastIndex)
            {
                ClearContacts();
                ApplyMomentum(this[lastIndex]);
            }

            if (lastIndex.segment < momentums.Count - 1)
                momentums.RemoveRange(
                    lastIndex.segment + 1, 
                    momentums.Count - (lastIndex.segment + 1));

            int nextMomI = lastIndex.momentum + 1;
            while (nextMomI < momentums[lastIndex.segment].Length
                && momentums[lastIndex.segment][nextMomI] != null)
            {
                Momentum2D.Release(momentums[lastIndex.segment][nextMomI++]);
                momentums[lastIndex.segment][nextMomI++] = null;
            }
                            
        }

        public void EndSimulation(uint tick)
        {
            if (this[lastIndex].tick < tick && HasSimulationChangedLast())
            {
                Momentum2D[] segment = momentums[lastIndex.segment];
                if (lastIndex.momentum + 1 < segment.Length)
                    lastIndex = new TrailIndex(
                        lastIndex.segment,
                        lastIndex.momentum + 1);
                else
                {
                    momentums.Add(new Momentum2D[segmentSize]);
                    lastIndex = new TrailIndex(
                        lastIndex.segment + 1,
                        0
                    );
                }

                this[lastIndex] = ExtractMomentum(tick);
            }

            ClearContacts();
            ApplyMomentum(this[currentIndex]);
        }

        Momentum2D ExtractMomentum(uint tick)
        {
            return Momentum2D.GetMomentum(
                tick,
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
            transform.SetPositionAndRotation(
                momentum.position,
                momentum.rotation
            );
            body.velocity = momentum.velocity;
            body.angularVelocity = momentum.angularVelocity;
            if (body.IsSleeping() && !momentum.sleeping)
                body.WakeUp();
            else if (body.IsAwake() && momentum.sleeping)
                body.Sleep();
        }

        bool HasSimulationChangedLast()
        {
            Momentum2D last = this.last;
            return (
                   (Vector2)(transform.position) != last.position
                || transform.rotation != last.rotation
                || body.velocity != last.velocity
                || !Mathf.Approximately(body.angularVelocity, last.angularVelocity)
                || body.IsSleeping() != last.sleeping
                || collisionsEnter != null || collisionsExit != null
                || triggersEnter != null || triggersExit != null
                || !last.HasSameCollisionsStay(collisionsStay)
                || !last.HasSameTriggersStay(triggersStay)
                );
        }
        #endregion

        #region Contacts
        public event Collision2DEventDelegate CollisionEnterEvent;
        public event Collision2DEventDelegate CollisionStayEvent;
        public event Collision2DEventDelegate CollisionExitEvent;
        public event Trigger2DEventDelegate TriggerEnterEvent;
        public event Trigger2DEventDelegate TriggerStayEvent;
        public event Trigger2DEventDelegate TriggerExitEvent;

        List<Collision2D> collisionsEnter;
        List<Collision2D> collisionsStay;
        List<Collision2D> collisionsExit;
        List<Collider2D> triggersEnter;
        List<Collider2D> triggersStay;
        List<Collider2D> triggersExit;

        void OnCollisionEnter2D(Collision2D collision)
        {
            if (collisionsEnter == null)
                collisionsEnter = new List<Collision2D>();
            collisionsEnter.Add(collision);
        }

        void OnCollisionStay2D(Collision2D collision)
        {
            if (collisionsStay == null)
                collisionsStay = new List<Collision2D>();
            collisionsStay.Add(collision);
        }

        void OnCollisionExit2D(Collision2D collision)
        {
            if (collisionsExit == null)
                collisionsExit = new List<Collision2D>();
            collisionsExit.Add(collision);
        }

        void OnTriggerEnter2D(Collider2D collision)
        {
            if (triggersEnter == null)
                triggersEnter = new List<Collider2D>();
            triggersEnter.Add(collision);
        }

        void OnTriggerStay2D(Collider2D collision)
        {
            if (triggersStay == null)
                triggersStay = new List<Collider2D>();
            triggersStay.Add(collision);
        }

        void OnTriggerExit2D(Collider2D collision)
        {
            if (triggersExit == null)
                triggersExit = new List<Collider2D>();
            triggersExit.Add(collision);
        }

        public void SendContactEvents()
        {
            Momentum2D momentum = this[currentIndex];

            if (CollisionEnterEvent != null)
                foreach (Collision2D collision in momentum.CollisionEnterIterator())
                    CollisionEnterEvent(momentum.tick, collision);

			if (CollisionStayEvent != null)
				foreach (Collision2D collision in momentum.CollisionStayIterator())
					CollisionStayEvent(momentum.tick, collision);

			if (CollisionExitEvent != null)
				foreach (Collision2D collision in momentum.CollisionExitIterator())
					CollisionExitEvent(momentum.tick, collision);

            if (TriggerEnterEvent != null)
                foreach (Collider2D collision in momentum.TriggerEnterIterator())
					TriggerEnterEvent(momentum.tick, collision);

			if (TriggerEnterEvent != null)
				foreach (Collider2D collision in momentum.TriggerEnterIterator())
					TriggerEnterEvent(momentum.tick, collision);

			if (TriggerStayEvent != null)
				foreach (Collider2D collision in momentum.TriggerStayIterator())
					TriggerStayEvent(momentum.tick, collision);

			if (TriggerExitEvent != null)
				foreach (Collider2D collision in momentum.TriggerExitIterator())
					TriggerExitEvent(momentum.tick, collision);
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

        #region Trail Index Struct
        struct TrailIndex : IEquatable<TrailIndex>
        {
            static public TrailIndex zero = new TrailIndex(0, 0);
            static public TrailIndex invalid = new TrailIndex(-1, -1);
            public int segment { get; private set; }
            public int momentum { get; private set; }
            public bool isInvalid { get { return segment == -1 || momentum == -1; }}

            public TrailIndex(int segment, int momentum)
            {
                this.segment = segment;
                this.momentum = momentum;
            }

            public bool Equals(TrailIndex trailIndex)
            {
                return this == trailIndex;
            }

            public override bool Equals(System.Object obj)
            {
                return obj is TrailIndex && this == (TrailIndex)obj;
            }

            public override int GetHashCode()
            {
                return segment.GetHashCode() ^ momentum.GetHashCode();
            }

            public static bool operator ==(TrailIndex x, TrailIndex y)
            {
                return (
                       x.segment == y.segment
                    && x.momentum == y.momentum
                );
            }

			public static bool operator !=(TrailIndex x, TrailIndex y)
			{
				return !(x == y);
			}
        }
        #endregion
    }

    #region Momentum2D Class
    public class Momentum2D : IEquatable<Momentum2D>
    {
        public uint tick { get; private set; }
        public Vector2 position { get; private set; }
        public Quaternion rotation { get; private set; }
        public Vector2 velocity { get; private set; }
        public float angularVelocity { get; private set; }
        public bool sleeping { get; private set; }
        ICollection<Collision2D> collisionsEnter;
        ICollection<Collision2D> collisionsStay;
        ICollection<Collision2D> collisionsExit;
        ICollection<Collider2D> triggersEnter;
        ICollection<Collider2D> triggersStay;
        ICollection<Collider2D> triggersExit;

        private Momentum2D()
        {

        }

        public Momentum2D(
            uint tick,
            Vector2 position, Quaternion rotation,
            Vector2 velocity, float angularVelocity,
            bool sleeping,
            ICollection<Collision2D> collisionsEnter = null, 
            ICollection<Collision2D> collisionsStay = null,
            ICollection<Collision2D> collisionsExit = null,
            ICollection<Collider2D> triggersEnter = null, 
            ICollection<Collider2D> triggersStay = null, 
            ICollection<Collider2D> triggersExit = null
        )
        {
            Populate(
                tick,
                position, rotation,
                velocity, angularVelocity,
                sleeping,
                collisionsEnter, collisionsStay, collisionsExit,
                triggersEnter, triggersStay, triggersExit);
        }

        public Momentum2D(
            uint tick,
            Momentum2D source
        )
        {
            Populate(tick, source);
        }

        private Momentum2D Populate(
            uint tick,
            Vector2 position, Quaternion rotation,
            Vector2 velocity, float angularVelocity,
            bool sleeping,
            ICollection<Collision2D> collisionsEnter = null,
            ICollection<Collision2D> collisionsStay = null,
            ICollection<Collision2D> collisionsExit = null,
            ICollection<Collider2D> triggersEnter = null,
            ICollection<Collider2D> triggersStay = null,
            ICollection<Collider2D> triggersExit = null)
        {
            this.tick = tick;
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

            return this;
        }

        private Momentum2D Populate(
            uint tick,
            Momentum2D source
        )
        {
            this.tick = tick;
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

            return this;
        }

        public bool HasSameCollisionsStay(List<Collision2D> collisions)
        {
            if (collisionsStay == collisions) return true;
            if (collisionsStay == null ^ collisions == null) return false;
            if (collisionsStay.Count != collisions.Count) return false;
            return !collisionsStay.Except(collisions).Any();
        }

        public bool HasSameTriggersStay(List<Collider2D> collisions)
        {
            if (triggersStay == collisions) return true;
            if (triggersStay == null ^ collisions == null) return false;
            if (triggersStay.Count != collisions.Count) return false;
            return !triggersStay.Except(collisions).Any();
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

        public bool Equals(Momentum2D momentum2D)
        {
            return ReferenceEquals(this, momentum2D);
        }

        public override bool Equals(System.Object obj)
        {
            return obj is Momentum2D 
                && ReferenceEquals(this, (Momentum2D)obj);
        }

        public override int GetHashCode()
        {
            return tick.GetHashCode();
        }

        public static uint poolSize = 50;
        private static Stack<Momentum2D> pool;

        Momentum2D Clean()
        {
            collisionsEnter = collisionsStay = collisionsExit = null;
            triggersEnter = triggersStay = triggersExit = null;

            return this;
        }

        public static Momentum2D GetMomentum(
            uint tick,
            Vector2 position, Quaternion rotation,
            Vector2 velocity, float angularVelocity,
            bool sleeping,
            ICollection<Collision2D> collisionsEnter = null,
            ICollection<Collision2D> collisionsStay = null,
            ICollection<Collision2D> collisionsExit = null,
            ICollection<Collider2D> triggersEnter = null,
            ICollection<Collider2D> triggersStay = null,
            ICollection<Collider2D> triggersExit = null)
        {
            return GetMomentum().Populate(
                tick,
                position, rotation,
                velocity, angularVelocity,
                sleeping,
                collisionsEnter, collisionsStay, collisionsExit,
                triggersEnter, triggersStay, triggersExit);
        }

        public static Momentum2D GetMomentum(
            uint tick,
            Momentum2D source
        )
        {
            return GetMomentum().Populate(tick, source);
        }

        private static Momentum2D GetMomentum()
        {
            if (pool != null && pool.Count > 0)
                return pool.Pop();

            else return new Momentum2D();
        }

        public static void Release(Momentum2D[] momentums)
        {
            if (IsPoolFull()) return;
            foreach (Momentum2D momentum in momentums)
            {
                Release(momentum);
                if (IsPoolFull()) break;
            }
        }

        public static void Release(Momentum2D momentum)
        {
            if (momentum == null) return;
            if (pool != null && pool.Count >= poolSize) return;
            if (pool == null)
                pool = new Stack<Momentum2D>((int)poolSize);

            pool.Push(momentum.Clean());
        }

        public static bool IsPoolFull()
        {
            return pool != null && pool.Count >= poolSize;
        }
    }
    #endregion
}