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
			momentums = new List<Momentum2D[]>();
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
            TrailIndex i = MomentumIndexForTick(tick);
            if (i.isInvalid) return false;

            if(i != currentIndex)
            {
				ClearContacts();
                currentIndex = i;
                ApplyMomentum(this[currentIndex]);
            }

            uint pastTick = Math.Max(tick - keepTick, 0);
            int pastSegI = -1;
            while (  pastSegI + 1 < i.segment
                  && momentums[pastSegI + 1][0].tick <= pastTick)
                pastSegI++;

            if(pastSegI > 0){
                currentIndex = new TrailIndex(
                    currentIndex.segment - pastSegI,
                    currentIndex.momentum
                );
                lastIndex = new TrailIndex(
					lastIndex.segment - pastSegI,
					lastIndex.momentum
				);
                momentums.RemoveRange(0, pastSegI);
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
            while (segI <= lastIndex.segment && momI <= lastIndex.momentum)
            {
                Momentum2D momentum = segment[momI];
                if (   checkEnd
                    && momentum.tick > endTick)
                    yield break;

                yield return momentum;

                if (segI == lastIndex.segment && momI == lastIndex.momentum)
                    yield break;

                if (momI < segment.Length - 1
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
            if (!i.isInvalid) return null;
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

            if(lastIndex.momentum < momentums[lastIndex.segment].Length - 1)
            {
                Momentum2D[] segment = momentums[lastIndex.segment];
                for (int momI = lastIndex.momentum + 1; momI < segment.Length; momI++)
                    segment[momI] = null;
            }                   
        }

        public void EndSimulation(uint tick)
        {
            Momentum2D momentum = ExtractMomentum(tick);

            if (currentIndex != lastIndex)
            {
                ClearContacts();
                ApplyMomentum(this[currentIndex]);
            }

            Momentum2D lastMomentum = this[lastIndex];
            if (lastMomentum.tick < tick && !lastMomentum.Same(momentum))
            {
                Momentum2D[] segment = momentums[lastIndex.segment];
                if (   lastIndex.momentum + 1 < segment.Length)
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
				this[lastIndex] = momentum;
            }
        }

        Momentum2D ExtractMomentum(uint tick)
        {
            return new Momentum2D(
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
            transform.position = momentum.position;
            transform.rotation = momentum.rotation;
            body.velocity = momentum.velocity;
            body.angularVelocity = momentum.angularVelocity;
            if (body.IsSleeping() && !momentum.sleeping)
                body.WakeUp();
            else if (body.IsAwake() && momentum.sleeping)
                body.Sleep();
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
                    && x.momentum == y.segment
                );
            }

			public static bool operator !=(TrailIndex x, TrailIndex y)
			{
				return !(x == y);
			}

			public static bool operator < (TrailIndex x, TrailIndex y)
			{
                if (x.segment < y.segment) return true;
                if (x.segment > y.segment) return false;
                return x.momentum < y.momentum;
			}

            public static bool operator > (TrailIndex x, TrailIndex y)
            {
                return !(x < y) && (x != y);
            }

			public static bool operator <=(TrailIndex x, TrailIndex y)
			{
				return (x < y) || (x == y);
			}

			public static bool operator >=(TrailIndex x, TrailIndex y)
			{
				return (x > y) || (x == y);
			}
        }
        #endregion
    }

    public class Momentum2D : IEquatable<Momentum2D>
    {
        public uint tick { get; }
        public Vector2 position { get; }
        public Quaternion rotation { get; }
        public Vector2 velocity { get; }
        public float angularVelocity { get; }
        public bool sleeping { get; }
        ICollection<Collision2D> collisionsEnter;
        ICollection<Collision2D> collisionsStay;
        ICollection<Collision2D> collisionsExit;
        ICollection<Collider2D> triggersEnter;
        ICollection<Collider2D> triggersStay;
        ICollection<Collider2D> triggersExit;


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
        }

        public Momentum2D(
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
            return this == momentum2D;
        }

        public override bool Equals(System.Object obj)
        {
            return obj is Momentum2D && this == (Momentum2D)obj;
        }

        public override int GetHashCode()
        {
            return tick.GetHashCode();
        }

        public bool Same(Momentum2D other)
        {
            return Same(this, other);
        }

        public static bool operator ==(Momentum2D x, Momentum2D y)
        {
			if (x == null && y == null) return true;
			if (x == null || y == null) return false;

            return (
                   x.tick == y.tick
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
			if (x == null && y == null) return true;
			if (x == null || y == null) return false;

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

        static bool EqualsContacts(Momentum2D x, Momentum2D y)
        {
            return (
                   x.collisionsEnter == y.collisionsEnter
                && x.collisionsStay == y.collisionsStay
                && x.collisionsExit == y.collisionsExit
                && x.triggersEnter == y.triggersEnter
                && x.triggersStay == y.triggersStay
                && x.triggersExit == y.triggersExit
            );
        }

        static bool SameContacts(Momentum2D x, Momentum2D y)
        {
            return (
                   SameCollections(x.collisionsEnter, y.collisionsEnter)
                && SameCollections(x.collisionsStay, y.collisionsStay)
                && SameCollections(x.collisionsExit, y.collisionsExit)
                && SameCollections(x.triggersEnter, y.triggersEnter)
                && SameCollections(x.triggersStay, y.triggersStay)
                && SameCollections(x.triggersExit, y.triggersExit)
            );
        }

        static bool SameCollections<T>(ICollection<T> collectionX, ICollection<T> collectionY)
        {
            if (collectionX == null && collectionY == null) return true;
            if (collectionX == null || collectionY == null) return false;
            if (collectionX.Count != collectionY.Count) return false;
            return !collectionX.Except(collectionY).Any();
        }
    }
}