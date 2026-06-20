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
            bool isShieldOrAmmo,
            bool isAmmo,
            bool isThrown)
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
            IsAmmo = isAmmo;
            IsThrown = isThrown;
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
        public bool IsAmmo { get; }
        public bool IsThrown { get; }
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

        public static bool StrictlyBeatsWeapon(
            WeaponStats candidate,
            WeaponStats current,
            ProjectileUpgradePreference ammoPreference = ProjectileUpgradePreference.CountAndDamage,
            ProjectileUpgradePreference throwingPreference = ProjectileUpgradePreference.CountAndDamage,
            bool ignoreThrowingMeleeStats = true)
        {
            if (!candidate.IsValid || !current.IsValid) return false;
            if (candidate.WeaponClass != current.WeaponClass) return false;
            if (candidate.IsAmmo || current.IsAmmo)
            {
                return candidate.IsAmmo && current.IsAmmo && StrictlyBeatsProjectile(candidate, current, ammoPreference);
            }

            if (candidate.IsThrown || current.IsThrown)
            {
                return candidate.IsThrown &&
                       current.IsThrown &&
                       StrictlyBeatsThrowingWeapon(candidate, current, throwingPreference, ignoreThrowingMeleeStats);
            }

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

        public static float GetWeaponScore(
            WeaponStats stats,
            ProjectileUpgradePreference ammoPreference = ProjectileUpgradePreference.CountAndDamage,
            ProjectileUpgradePreference throwingPreference = ProjectileUpgradePreference.CountAndDamage,
            bool ignoreThrowingMeleeStats = true)
        {
            if (!stats.IsValid) return -9999f;
            if (stats.IsAmmo)
            {
                return GetProjectileScore(stats, ammoPreference);
            }

            if (stats.IsThrown)
            {
                float projectileScore = GetProjectileScore(stats, throwingPreference);
                return ignoreThrowingMeleeStats ? projectileScore : projectileScore + GetMeleeWeaponScore(stats);
            }

            if (stats.IsMelee)
            {
                return GetMeleeWeaponScore(stats);
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

        private static bool StrictlyBeatsThrowingWeapon(
            WeaponStats candidate,
            WeaponStats current,
            ProjectileUpgradePreference throwingPreference,
            bool ignoreMeleeStats)
        {
            if (!StrictlyBeatsProjectile(candidate, current, throwingPreference)) return false;
            if (ignoreMeleeStats) return true;

            return candidate.ThrustSpeed >= current.ThrustSpeed &&
                   candidate.SwingSpeed >= current.SwingSpeed &&
                   candidate.ThrustDamage >= current.ThrustDamage &&
                   candidate.SwingDamage >= current.SwingDamage &&
                   candidate.WeaponLength >= current.WeaponLength &&
                   candidate.Handling >= current.Handling;
        }

        private static bool StrictlyBeatsProjectile(WeaponStats candidate, WeaponStats current, ProjectileUpgradePreference preference)
        {
            switch (preference)
            {
                case ProjectileUpgradePreference.CountOnly:
                    return candidate.Durability > current.Durability;
                case ProjectileUpgradePreference.DamageOnly:
                    return candidate.MissileDamage > current.MissileDamage;
                default:
                    bool equalOrBetter =
                        candidate.Durability >= current.Durability &&
                        candidate.MissileDamage >= current.MissileDamage;
                    bool strictlyBetter =
                        candidate.Durability > current.Durability ||
                        candidate.MissileDamage > current.MissileDamage;
                    return equalOrBetter && strictlyBetter;
            }
        }

        private static float GetProjectileScore(WeaponStats stats, ProjectileUpgradePreference preference)
        {
            switch (preference)
            {
                case ProjectileUpgradePreference.CountOnly:
                    return stats.Durability;
                case ProjectileUpgradePreference.DamageOnly:
                    return stats.MissileDamage;
                default:
                    return stats.Durability * 10f + stats.MissileDamage;
            }
        }

        private static float GetMeleeWeaponScore(WeaponStats stats)
        {
            return stats.ThrustDamage * stats.ThrustSpeed * 0.01f +
                   stats.SwingDamage * stats.SwingSpeed * 0.01f +
                   stats.Handling * 10f +
                   stats.WeaponLength;
        }

        public static int GetCascadeIterationLimit(int slotTargetCount, int initialQueueCount)
        {
            return Math.Max(1, slotTargetCount * Math.Max(1, initialQueueCount + 1) * 2);
        }
    }
}
