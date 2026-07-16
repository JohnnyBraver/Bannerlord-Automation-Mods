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

    internal enum WeaponRole
    {
        Other,
        OneHandedSword,
        OneHandedAxeMace,
        Dagger,
        TwoHanded,
        ThrustPolearm,
        SwingPolearm,
        Ranged,
        Shield,
        Ammo,
        Throwing
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
            bool isThrown,
            WeaponRole role = WeaponRole.Other,
            int itemTier = 0,
            string itemUsage = "")
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
            Role = role;
            ItemTier = itemTier;
            ItemUsage = itemUsage ?? string.Empty;
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
        public WeaponRole Role { get; }
        public int ItemTier { get; }
        public string ItemUsage { get; }
    }

    internal readonly struct WeaponEvaluationContext
    {
        public WeaponEvaluationContext(
            ProjectileUpgradePreference ammoPreference = ProjectileUpgradePreference.CountAndDamage,
            ProjectileUpgradePreference throwingPreference = ProjectileUpgradePreference.CountAndDamage,
            bool ignoreThrowingMeleeStats = true,
            MeleeWeaponUpgradePreference oneHandedSwordPreference = MeleeWeaponUpgradePreference.Balanced,
            MeleeWeaponUpgradePreference oneHandedAxeMacePreference = MeleeWeaponUpgradePreference.Balanced,
            MeleeWeaponUpgradePreference twoHandedPreference = MeleeWeaponUpgradePreference.Balanced,
            MeleeWeaponUpgradePreference thrustPolearmPreference = MeleeWeaponUpgradePreference.Balanced,
            MeleeWeaponUpgradePreference swingPolearmPreference = MeleeWeaponUpgradePreference.Balanced,
            RangedWeaponUpgradePreference rangedPreference = RangedWeaponUpgradePreference.Balanced,
            ShieldUpgradePreference shieldPreference = ShieldUpgradePreference.Balanced,
            WeaponPropertyMatching propertyMatching = WeaponPropertyMatching.IgnoreMinor,
            bool isMountedBattle = false,
            bool canUseAllBowsMounted = false,
            bool canReloadAllCrossbowsMounted = false,
            long hardRestrictionFlags = 0,
            long hardCapabilityFlags = 0,
            long minorPropertyFlags = 0,
            long cantReloadOnHorsebackFlag = 0)
        {
            AmmoPreference = ammoPreference;
            ThrowingPreference = throwingPreference;
            IgnoreThrowingMeleeStats = ignoreThrowingMeleeStats;
            OneHandedSwordPreference = oneHandedSwordPreference;
            OneHandedAxeMacePreference = oneHandedAxeMacePreference;
            TwoHandedPreference = twoHandedPreference;
            ThrustPolearmPreference = thrustPolearmPreference;
            SwingPolearmPreference = swingPolearmPreference;
            RangedPreference = rangedPreference;
            ShieldPreference = shieldPreference;
            PropertyMatching = propertyMatching;
            IsMountedBattle = isMountedBattle;
            CanUseAllBowsMounted = canUseAllBowsMounted;
            CanReloadAllCrossbowsMounted = canReloadAllCrossbowsMounted;
            HardRestrictionFlags = hardRestrictionFlags;
            HardCapabilityFlags = hardCapabilityFlags;
            MinorPropertyFlags = minorPropertyFlags;
            CantReloadOnHorsebackFlag = cantReloadOnHorsebackFlag;
        }

        public ProjectileUpgradePreference AmmoPreference { get; }
        public ProjectileUpgradePreference ThrowingPreference { get; }
        public bool IgnoreThrowingMeleeStats { get; }
        public MeleeWeaponUpgradePreference OneHandedSwordPreference { get; }
        public MeleeWeaponUpgradePreference OneHandedAxeMacePreference { get; }
        public MeleeWeaponUpgradePreference TwoHandedPreference { get; }
        public MeleeWeaponUpgradePreference ThrustPolearmPreference { get; }
        public MeleeWeaponUpgradePreference SwingPolearmPreference { get; }
        public RangedWeaponUpgradePreference RangedPreference { get; }
        public ShieldUpgradePreference ShieldPreference { get; }
        public WeaponPropertyMatching PropertyMatching { get; }
        public bool IsMountedBattle { get; }
        public bool CanUseAllBowsMounted { get; }
        public bool CanReloadAllCrossbowsMounted { get; }
        public long HardRestrictionFlags { get; }
        public long HardCapabilityFlags { get; }
        public long MinorPropertyFlags { get; }
        public long CantReloadOnHorsebackFlag { get; }
    }

    internal readonly struct WeaponEvaluationResult
    {
        public WeaponEvaluationResult(bool isUpgrade, float candidateScore, float currentScore, string rejectionReason)
        {
            IsUpgrade = isUpgrade;
            CandidateScore = candidateScore;
            CurrentScore = currentScore;
            RejectionReason = rejectionReason ?? string.Empty;
        }

        public bool IsUpgrade { get; }
        public float CandidateScore { get; }
        public float CurrentScore { get; }
        public float ScoreIncrease => CandidateScore - CurrentScore;
        public string RejectionReason { get; }
    }

    internal static class EquipmentDecisionMath
    {
        private const float ScoreEpsilon = 0.01f;

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
            var context = new WeaponEvaluationContext(
                ammoPreference: ammoPreference,
                throwingPreference: throwingPreference,
                ignoreThrowingMeleeStats: ignoreThrowingMeleeStats);
            return EvaluateWeaponUpgrade(candidate, current, context).IsUpgrade;
        }

        public static WeaponEvaluationResult EvaluateWeaponUpgrade(
            WeaponStats candidate,
            WeaponStats current,
            WeaponEvaluationContext context)
        {
            float candidateScore = GetWeaponScore(candidate, context);
            float currentScore = GetWeaponScore(current, context);

            if (!candidate.IsValid)
            {
                return new WeaponEvaluationResult(false, candidateScore, currentScore, "candidate is not a valid weapon");
            }

            if (!current.IsValid)
            {
                return new WeaponEvaluationResult(true, candidateScore, currentScore, string.Empty);
            }

            if (!RolesAreCompatible(candidate, current))
            {
                return new WeaponEvaluationResult(false, candidateScore, currentScore, "weapon role does not match the current item");
            }

            string compatibilityReason;
            if (!PassesHardCompatibility(candidate, current, context, out compatibilityReason))
            {
                return new WeaponEvaluationResult(false, candidateScore, currentScore, compatibilityReason);
            }

            if (candidate.IsAmmo || current.IsAmmo)
            {
                bool isUpgrade = candidate.IsAmmo && current.IsAmmo && StrictlyBeatsProjectile(candidate, current, context.AmmoPreference);
                return new WeaponEvaluationResult(isUpgrade, candidateScore, currentScore, isUpgrade ? string.Empty : "ammo count/damage preference is not improved");
            }

            if (candidate.IsThrown || current.IsThrown)
            {
                bool isUpgrade = candidate.IsThrown &&
                                 current.IsThrown &&
                                 StrictlyBeatsThrowingWeapon(candidate, current, context.ThrowingPreference, context.IgnoreThrowingMeleeStats);
                return new WeaponEvaluationResult(isUpgrade, candidateScore, currentScore, isUpgrade ? string.Empty : "throwing weapon preference is not improved");
            }

            if (candidateScore > currentScore + ScoreEpsilon)
            {
                return new WeaponEvaluationResult(true, candidateScore, currentScore, string.Empty);
            }

            return new WeaponEvaluationResult(false, candidateScore, currentScore, "preferred weapon score is not higher");
        }

        public static float GetWeaponScore(
            WeaponStats stats,
            ProjectileUpgradePreference ammoPreference = ProjectileUpgradePreference.CountAndDamage,
            ProjectileUpgradePreference throwingPreference = ProjectileUpgradePreference.CountAndDamage,
            bool ignoreThrowingMeleeStats = true)
        {
            var context = new WeaponEvaluationContext(
                ammoPreference: ammoPreference,
                throwingPreference: throwingPreference,
                ignoreThrowingMeleeStats: ignoreThrowingMeleeStats);
            return GetWeaponScore(stats, context);
        }

        public static float GetWeaponScore(WeaponStats stats, WeaponEvaluationContext context)
        {
            if (!stats.IsValid) return -9999f;
            if (stats.IsAmmo)
            {
                return GetProjectileScore(stats, context.AmmoPreference);
            }

            if (stats.IsThrown)
            {
                float projectileScore = GetProjectileScore(stats, context.ThrowingPreference);
                return context.IgnoreThrowingMeleeStats ? projectileScore : projectileScore + GetMeleeWeaponScore(stats, context);
            }

            if (stats.Role == WeaponRole.Shield || (stats.IsShieldOrAmmo && !stats.IsAmmo))
            {
                return GetShieldScore(stats, context.ShieldPreference);
            }

            if (stats.Role == WeaponRole.Ranged || (stats.IsRanged && !stats.IsMelee))
            {
                return GetRangedWeaponScore(stats, context.RangedPreference);
            }

            if (stats.IsMelee)
            {
                return GetMeleeWeaponScore(stats, context);
            }

            return 0f;
        }

        private static bool RolesAreCompatible(WeaponStats candidate, WeaponStats current)
        {
            if (candidate.Role != current.Role) return false;

            switch (candidate.Role)
            {
                case WeaponRole.Ammo:
                case WeaponRole.Throwing:
                case WeaponRole.Ranged:
                case WeaponRole.Dagger:
                case WeaponRole.Other:
                    return candidate.WeaponClass == current.WeaponClass;
                default:
                    return true;
            }
        }

        private static bool PassesHardCompatibility(
            WeaponStats candidate,
            WeaponStats current,
            WeaponEvaluationContext context,
            out string reason)
        {
            reason = string.Empty;

            long introducedRestrictions = candidate.Flags & ~current.Flags & context.HardRestrictionFlags;
            if (introducedRestrictions != 0)
            {
                reason = "candidate adds a hard weapon restriction";
                return false;
            }

            if (context.IsMountedBattle)
            {
                if (context.CantReloadOnHorsebackFlag != 0 &&
                    (candidate.Flags & context.CantReloadOnHorsebackFlag) != 0 &&
                    !context.CanReloadAllCrossbowsMounted)
                {
                    reason = "candidate cannot reload on horseback for this mounted character";
                    return false;
                }

                if (CandidateRequiresNoMount(candidate) && !context.CanUseAllBowsMounted)
                {
                    reason = "candidate cannot be used on horseback for this mounted character";
                    return false;
                }
            }

            long requiredCapabilities = GetRequiredCapabilityFlags(current, context);
            long missingCapabilities = requiredCapabilities & ~candidate.Flags;
            if (missingCapabilities != 0)
            {
                reason = "candidate does not preserve required weapon properties";
                return false;
            }

            return true;
        }

        private static long GetRequiredCapabilityFlags(WeaponStats current, WeaponEvaluationContext context)
        {
            long currentCapabilities = current.Flags & ~context.HardRestrictionFlags;
            switch (context.PropertyMatching)
            {
                case WeaponPropertyMatching.PreserveAll:
                    return currentCapabilities;
                case WeaponPropertyMatching.StatsOnly:
                    return currentCapabilities & context.HardCapabilityFlags;
                case WeaponPropertyMatching.IgnoreMinor:
                default:
                    return currentCapabilities & ~context.MinorPropertyFlags;
            }
        }

        private static bool CandidateRequiresNoMount(WeaponStats candidate)
        {
            // Native long bows use the "long_bow" item usage set, which declares requires_no_mount.
            return candidate.Role == WeaponRole.Ranged &&
                   candidate.ItemUsage.Equals("long_bow", StringComparison.OrdinalIgnoreCase);
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

        private static float GetMeleeWeaponScore(WeaponStats stats, WeaponEvaluationContext context)
        {
            var preference = GetMeleePreference(stats.Role, context);
            float primaryDamage = GetPrimaryMeleeDamage(stats);
            float primarySpeed = GetPrimaryMeleeSpeed(stats);
            float reach = Math.Min(Math.Max(stats.WeaponLength, 0), 250);
            float tier = Math.Max(stats.ItemTier, 0);

            float damageWeight;
            float speedWeight;
            float handlingWeight;
            float reachWeight;

            switch (preference)
            {
                case MeleeWeaponUpgradePreference.DamageAndReach:
                    damageWeight = 3.0f;
                    speedWeight = 0.3f;
                    handlingWeight = 0.3f;
                    reachWeight = 1.0f;
                    break;
                case MeleeWeaponUpgradePreference.SpeedAndHandling:
                    damageWeight = 1.2f;
                    speedWeight = 1.2f;
                    handlingWeight = 1.4f;
                    reachWeight = 0.2f;
                    break;
                default:
                    damageWeight = 2.2f;
                    speedWeight = 0.6f;
                    handlingWeight = 0.6f;
                    reachWeight = 0.5f;
                    break;
            }

            if (stats.Role == WeaponRole.ThrustPolearm || stats.Role == WeaponRole.SwingPolearm)
            {
                reachWeight += preference == MeleeWeaponUpgradePreference.SpeedAndHandling ? 0.1f : 0.3f;
            }

            return primaryDamage * damageWeight +
                   primarySpeed * speedWeight +
                   stats.Handling * handlingWeight +
                   reach * reachWeight +
                   tier * 2f;
        }

        private static MeleeWeaponUpgradePreference GetMeleePreference(WeaponRole role, WeaponEvaluationContext context)
        {
            switch (role)
            {
                case WeaponRole.OneHandedAxeMace:
                    return context.OneHandedAxeMacePreference;
                case WeaponRole.TwoHanded:
                    return context.TwoHandedPreference;
                case WeaponRole.ThrustPolearm:
                    return context.ThrustPolearmPreference;
                case WeaponRole.SwingPolearm:
                    return context.SwingPolearmPreference;
                case WeaponRole.Dagger:
                case WeaponRole.OneHandedSword:
                default:
                    return context.OneHandedSwordPreference;
            }
        }

        private static float GetPrimaryMeleeDamage(WeaponStats stats)
        {
            if (stats.Role == WeaponRole.ThrustPolearm)
            {
                return stats.ThrustDamage > 0 ? stats.ThrustDamage : Math.Max(stats.SwingDamage, stats.ThrustDamage);
            }

            if (stats.Role == WeaponRole.SwingPolearm)
            {
                return stats.SwingDamage > 0 ? stats.SwingDamage : Math.Max(stats.SwingDamage, stats.ThrustDamage);
            }

            return stats.SwingDamage > 0 ? stats.SwingDamage : Math.Max(stats.SwingDamage, stats.ThrustDamage);
        }

        private static float GetPrimaryMeleeSpeed(WeaponStats stats)
        {
            if (stats.Role == WeaponRole.ThrustPolearm)
            {
                return stats.ThrustDamage > 0 ? stats.ThrustSpeed : Math.Max(stats.SwingSpeed, stats.ThrustSpeed);
            }

            if (stats.Role == WeaponRole.SwingPolearm)
            {
                return stats.SwingDamage > 0 ? stats.SwingSpeed : Math.Max(stats.SwingSpeed, stats.ThrustSpeed);
            }

            return stats.SwingDamage > 0 ? stats.SwingSpeed : Math.Max(stats.SwingSpeed, stats.ThrustSpeed);
        }

        private static float GetRangedWeaponScore(WeaponStats stats, RangedWeaponUpgradePreference preference)
        {
            float tier = Math.Max(stats.ItemTier, 0);
            switch (preference)
            {
                case RangedWeaponUpgradePreference.Damage:
                    return stats.MissileDamage * 5.0f + stats.Accuracy * 0.3f + stats.MissileSpeed * 0.2f + tier * 2f;
                case RangedWeaponUpgradePreference.Accuracy:
                    return stats.Accuracy * 2.5f + stats.MissileDamage * 1.0f + stats.MissileSpeed * 0.2f + tier * 2f;
                default:
                    return stats.MissileDamage * 3.0f + stats.Accuracy * 1.0f + stats.MissileSpeed * 0.3f + tier * 2f;
            }
        }

        private static float GetShieldScore(WeaponStats stats, ShieldUpgradePreference preference)
        {
            float hitPoints = stats.Durability;
            float size = Math.Max(stats.WeaponLength, 0);
            float tier = Math.Max(stats.ItemTier, 0);

            switch (preference)
            {
                case ShieldUpgradePreference.MaxHitPoints:
                    return hitPoints * 2.0f + size * 0.5f + tier * 2f;
                case ShieldUpgradePreference.MaxSize:
                    return size * 5.0f + hitPoints * 0.3f + tier * 2f;
                default:
                    return hitPoints * 1.0f + size * 3.0f + tier * 2f;
            }
        }

        public static int GetCascadeIterationLimit(int slotTargetCount, int initialQueueCount)
        {
            return Math.Max(1, slotTargetCount * Math.Max(1, initialQueueCount + 1) * 2);
        }
    }
}
