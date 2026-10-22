using System;
using System.Collections;
using UnityEngine;
using ThiefAndTheNecklace.PB1;

namespace ThiefAndTheNecklace.PB1.Unity
{
    public sealed class RevealController : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float cardRevealSeconds = 0.65f;
        public event Action RevealStarted;
        public event Action<RoleType, bool> RoleRevealed;
        public event Action RevealFinished;

        public static RevealController Create(GameObject owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            return owner.AddComponent<RevealController>();
        }

        public void BeginReveal(int accusedPlayerId, RoleType revealedRole, bool thiefCaught)
        {
            if (accusedPlayerId <= 0) throw new ArgumentOutOfRangeException(nameof(accusedPlayerId));
            Begin(revealedRole, thiefCaught, true);
        }

        public void BeginEscapeReveal(RoleType revealedRole)
        {
            Begin(revealedRole, false, true);
        }

        public void BeginOutcomeOnly()
        {
            Begin(null, false, false);
        }

        private void Begin(RoleType? role, bool thiefCaught, bool revealRole)
        {
            StopAllCoroutines();
            RevealStarted?.Invoke();
            StartCoroutine(RevealRoutine(role, thiefCaught, revealRole));
        }

        private IEnumerator RevealRoutine(RoleType? role, bool thiefCaught, bool revealRole)
        {
            if (cardRevealSeconds > 0f) yield return new WaitForSeconds(cardRevealSeconds);
            if (revealRole && role.HasValue) RoleRevealed?.Invoke(role.Value, thiefCaught);
            RevealFinished?.Invoke();
        }
    }
}
