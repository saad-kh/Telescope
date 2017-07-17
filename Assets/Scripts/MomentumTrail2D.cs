using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class MomentumTrail2D : MonoBehaviour
{

	public float time { get; private set; } = 0f;

    Rigidbody2D body;
    int currentIndex;
    List<Momentum2D> momentums;

    void OnEnable ()
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
        else if(currentMomentumTime > toTime)
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

                if(!found)
                {
                    if (currentIndex < momentums.Count - 1)
                    {
                        momentums.Insert(
                            currentIndex + 1,
                            InterpolateMomentum(
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
                            InterpolateMomentum(
                                toTime,
                                momentums[currentIndex - 1],
                                momentums[currentIndex]
                            )
                        );
                        currentIndex++;
                    }
                    else
                        momentums.Add(ExtractMomentum(toTime));
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
        while(!done && futurIndex >= 0)
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
                        InterpolateMomentum(
                            atTime,
                            momentums[futurIndex],
                            momentums[futurIndex + 1]
                        )
                    );
                else
                    momentums.Insert(
                        futurIndex + 1,
                        InterpolateMomentum(
                            atTime,
                            momentums[futurIndex - 1],
                            momentums[futurIndex]
                        )
                    );
                futurIndex++;
            }
        }

        if (!synced)
        {
            ApplyMomentum(momentums[momentums.Count - 1]);
            time = atTime;
        }
            
    }

    public void EndSimulation(float atTime)
    {
        momentums.Add(ExtractMomentum(atTime));

        bool synced = false;
        bool done = false;
        int pastIndex = momentums.Count - 2;
        while (!done && pastIndex >= 0)
        {
            float futurMomentumTime = momentums[pastIndex].time;
            if (    futurMomentumTime > atTime
               &&   !Mathf.Approximately(futurMomentumTime, atTime))
                done = true;
            else
                pastIndex--;
        }

        if(pastIndex >= 0 && pastIndex < momentums.Count - 2)
        {
            momentums.RemoveRange(pastIndex, momentums.Count - 1 - pastIndex);
            if (pastIndex == currentIndex)
            {
                currentIndex = 0;
                synced = true;
            }
        }

        if (!synced)
        {
            ApplyMomentum(momentums[currentIndex]);
            time = momentums[currentIndex].time;
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

    Momentum2D InterpolateMomentum(float time, Momentum2D a, Momentum2D b)
    {
        //TODO Interpolation
        return new Momentum2D(
            time,
            b.position,
            b.rotation,
            b.velocity,
            b.angularVelocity
        );
    }
}

public struct Momentum2D
{
    public float time { get; }
    public Vector2 position { get; }
    public Quaternion rotation { get; }
    public Vector2 velocity { get; }
    public float angularVelocity { get; }

    public Momentum2D (
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

    public bool Same(Momentum2D other, float tolerance = 0)
    {
        if (    
                !Mathf.Approximately(time, other.time)
            ||  !Mathf.Approximately(position.x, other.position.x)
            ||  !Mathf.Approximately(position.y, other.position.y)
            ||  rotation != other.rotation
            ||  !Mathf.Approximately(velocity.x, other.velocity.x)
            ||  !Mathf.Approximately(velocity.y, other.velocity.y)
            ||  !Mathf.Approximately(angularVelocity, other.angularVelocity)
        )
            return false;

        return true;
    }
}
