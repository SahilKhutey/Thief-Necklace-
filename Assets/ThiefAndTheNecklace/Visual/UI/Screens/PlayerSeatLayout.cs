using System;
using System.Collections.Generic;
using UnityEngine;

namespace ThiefAndTheNecklace.Visual.Gameplay
{
    /// <summary>Returns evenly spaced player seats around a normalized table center.</summary>
    public sealed class PlayerSeatLayout
    {
        public List<Vector2> Generate(int playerCount, Vector2 center, float radius, float localSeatAngle = 270f)
        {
            if (playerCount < 2) throw new ArgumentException("At least two players required.", nameof(playerCount));
            var seats = new List<Vector2>(playerCount);
            var angleStep = 360f / playerCount;
            for (var i = 0; i < playerCount; i++)
            {
                var radians = (localSeatAngle + i * angleStep) * Mathf.Deg2Rad;
                seats.Add(center + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius);
            }
            return seats;
        }
    }
}
