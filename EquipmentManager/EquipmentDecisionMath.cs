using System;

namespace EquipmentManager
{
    internal readonly struct ArmorStats
    {
        public ArmorStats(int head, int body, int leg, int arm, int stealth)
        {
            Head = head;
            Body = body;
            Leg = leg;
            Arm = arm;
            Stealth = stealth;
        }

        public int Head { get; }
        public int Body { get; }
        public int Leg { get; }
        public int Arm { get; }
        public int Stealth { get; }
        public int ProtectionTotal => Head + Body + Leg + Arm;
    }

    internal readonly struct WeaponStats
    {
        public WeaponStats(
            bool isValid,
            int weaponClass,
            int thrustSpeed,
            int swingSpeed,
            int missileSpeed,
            int thrustDamage,
            int swingDamage,
            int missileDamage,
            int weaponLength,
            int handling,
            int accuracy,
            int durability,
            long flags,
            bool isMelee,
            bool isRanged,
            bool isShieldOrAmmo)
        {
            IsValid = isValid;
            WeaponClass = weaponClass;
            ThrustSpeed = thrustSpeed;
            SwingSpeed = swingSpeed;
            MissileSpeed = missileSpeed;
            ThrustDamage = thrustDamage;
            SwingDamage = swingDamage;
            MissileDamage = missileDamage;
            WeaponLength = weaponLength;
            Handling = handling;
            Accuracy = accuracy;
            Durability = durability;
            Flags = flags;
            IsMelee = isMelee;
            IsRanged = isRanged;
            IsShieldOrAmmo = isShieldOrAmmo;
        }

        public bool IsValid { get; }
        public int WeaponClass { get; }
        public int ThrustSpeed { get; }
        public int SwingSpeed { get; }
        public int MissileSpeed { get; }
        public int ThrustDamage { get; }
        public int SwingDamage { get; }
        public int MissileDamage { get; }
        public int WeaponLength { get; }
        public int Handling { get; }
        public int Accuracy { get; }
        public int Durability { get; }
        public long Flags { get; }
        public bool IsMelee { get; }
        public bool IsRanged { get; }
        public bool IsShieldOrAmmo { get; }
    }

    internal static class EquipmentDecisionMath
    {
        public static bool StrictlyBeatsArmor(ArmorStats candidate, ArmorStats current, bool prioritizeStealth)
        {
            bool protectionEqualOrBetter =
                candidate.Head >= current.Head &&
                candidate.Body >= current.Body &&
                candidate.Leg >= current.Leg &&
                candidate.Arm >= current.Arm;

            bool protectionStrictlyBetter =
                candidate.Head > current.Head ||
                candidate.Body > current.Body ||
                candidate.Leg > current.Leg ||
                candidate.Arm > current.Arm;

            if (prioritizeStealth)
            {
                if (candidate.Stealth > current.Stealth) return true;
                if (candidate.Stealth < current.Stealth) return false;
            }

            return protectionEqualOrBetter && protectionStrictlyBetter;
        }

        public static float GetArmorScore(ArmorStats stats, bool prioritizeStealth)
        {
            return prioritizeStealth
                ? stats.Stealth * 1000f + stats.ProtectionTotal
                : stats.ProtectionTotal;
        }

        public static bool StrictlyBeatsWeapon(WeaponStats candidate, WeaponStats current)
        {
            if (!candidate.IsValid || !current.IsValid) return false;
            if (candidate.WeaponClass != current.WeaponClass) return false;

            bool statsEqualOrBetter =
                candidate.ThrustSpeed >= current.ThrustSpeed &&
                candidate.SwingSpeed >= current.SwingSpeed &&
                candidate.MissileSpeed >= current.MissileSpeed &&
                candidate.ThrustDamage >= current.ThrustDamage &&
                candidate.SwingDamage >= current.SwingDamage &&
                candidate.MissileDamage >= current.MissileDamage &&
                candidate.WeaponLength >= current.WeaponLength &&
                candidate.Handling >= current.Handling &&
                candidate.Accuracy >= current.Accuracy &&
                candidate.Durability >= current.Durability;

            bool statsStrictlyBetter =
                candidate.ThrustSpeed > current.ThrustSpeed ||
                candidate.SwingSpeed > current.SwingSpeed ||
                candidate.MissileSpeed > current.MissileSpeed ||
                candidate.ThrustDamage > current.ThrustDamage ||
                candidate.SwingDamage > current.SwingDamage ||
                candidate.MissileDamage > current.MissileDamage ||
                candidate.WeaponLength > current.WeaponLength ||
                candidate.Handling > current.Handling ||
                candidate.Accuracy > current.Accuracy ||
                candidate.Durability > current.Durability;

            if (!statsEqualOrBetter || !statsStrictlyBetter) return false;

            return (candidate.Flags & current.Flags) == current.Flags;
        }

        public static float GetWeaponScore(WeaponStats stats)
        {
            if (!stats.IsValid) return -9999f;
            if (stats.IsMelee)
            {
                return stats.ThrustDamage * stats.ThrustSpeed * 0.01f +
                       stats.SwingDamage * stats.SwingSpeed * 0.01f +
                       stats.Handling * 10f +
                       stats.WeaponLength;
            }

            if (stats.IsRanged)
            {
                return stats.MissileDamage * stats.MissileSpeed * 0.01f + stats.Accuracy * 10f;
            }

            if (stats.IsShieldOrAmmo)
            {
                return stats.Durability;
            }

            return 0f;
        }

        public static int GetCascadeIterationLimit(int slotTargetCount, int initialQueueCount)
        {
            return Math.Max(1, slotTargetCount * Math.Max(1, initialQueueCount + 1) * 2);
        }
    }
}
