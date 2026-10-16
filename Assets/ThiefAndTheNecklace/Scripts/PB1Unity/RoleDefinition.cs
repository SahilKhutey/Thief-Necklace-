using UnityEngine;

namespace ThiefAndTheNecklace.PB1
{
    [CreateAssetMenu(fileName = "RoleDefinition", menuName = "Thief & the Necklace/PB1 Role Definition")]
    public sealed class RoleDefinition : ScriptableObject
    {
        [SerializeField] private RoleType role;
        [SerializeField] private string displayName;
        [SerializeField, Min(0)] private int scoreValue;
        [SerializeField] private bool thief;

        public RoleType Role { get { return role; } }
        public string DisplayName { get { return displayName; } }
        public int ScoreValue { get { return scoreValue; } }
        public bool IsThief { get { return thief; } }

        public static RoleDefinition CreateDefault(RoleType roleType)
        {
            if (roleType == RoleType.Thief) throw new System.ArgumentOutOfRangeException(nameof(roleType), "The Thief is an objective layered over a normal role.");
            var asset = CreateInstance<RoleDefinition>();
            asset.role = roleType;
            asset.displayName = roleType.ToString();
            asset.thief = false;
            asset.OnValidate();
            return asset;
        }

        private void OnValidate()
        {
            thief = false;
            if (string.IsNullOrWhiteSpace(displayName)) displayName = role.ToString();
            switch (role)
            {
                case RoleType.King: scoreValue = 8; break;
                case RoleType.Queen: scoreValue = 7; break;
                case RoleType.Minister: scoreValue = 6; break;
                case RoleType.Soldier:
                case RoleType.Guard: scoreValue = 5; break;
                case RoleType.Servant: scoreValue = 4; break;
                default: scoreValue = 0; break;
            }
        }
    }
}
