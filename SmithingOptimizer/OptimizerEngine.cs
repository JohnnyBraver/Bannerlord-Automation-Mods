using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace SmithingOptimizer
{
    public class CraftingDesignCandidate
    {
        public CraftingPiece Blade { get; set; } = null!;
        public CraftingPiece Guard { get; set; } = null!;
        public CraftingPiece Grip { get; set; } = null!;
        public CraftingPiece Pommel { get; set; } = null!;

        public int BladeScale { get; set; } = 100;
        public int GuardScale { get; set; } = 100;
        public int GripScale { get; set; } = 100;
        public int PommelScale { get; set; } = 100;

        public float Score { get; set; }
        public int Value { get; set; }
        public int SmithingXp { get; set; }
        public int MaxDamage { get; set; }
        public int Difficulty { get; set; }
        public int StaminaCost { get; set; }
        public int MaterialReferenceValue { get; set; }
        public float QualityChance { get; set; }
        public bool UsesExpectedQualityScore { get; set; }
        public int[] MaterialCosts { get; set; } = new int[9];
    }

    public sealed class OptimizationRunResult
    {
        public CraftingDesignCandidate? Current { get; }
        public CraftingDesignCandidate? Best { get; }
        public bool ExpectedQualityScoringDisabled { get; }

        public OptimizationRunResult(CraftingDesignCandidate? current, CraftingDesignCandidate? best, bool expectedQualityScoringDisabled = false)
        {
            Current = current;
            Best = best;
            ExpectedQualityScoringDisabled = expectedQualityScoringDisabled;
        }
    }

    public class OptimizerEngine
    {
        private const int MaximumPartPasses = 3;
        private const int MaximumSliderPasses = 3;
        // This is the MVID of the CampaignSystem assembly supported by this release.
        // The quality helper is private, so do not silently use it after a game update.
        private static readonly Guid SupportedCampaignSystemMvid = new Guid("99bbf418-c6b6-48b0-9a72-623c629df3fb");
        private static readonly PropertyInfo? CurrentWeaponDesignProperty = typeof(Crafting).GetProperty(
            "CurrentWeaponDesign", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly MethodInfo? ModifierQualityProbabilitiesMethod = typeof(DefaultSmithingModel).GetMethod(
            "GetModifierQualityProbabilities", BindingFlags.Static | BindingFlags.NonPublic,
            null, new[] { typeof(WeaponDesign), typeof(Hero) }, null);
        private static readonly PropertyInfo? EquipmentElementItemValueProperty = typeof(EquipmentElement).GetProperty(
            "ItemValue", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly bool ExpectedQualityScoringAvailable = IsExpectedQualityScoringAvailable();
        private static bool _expectedQualityCompatibilityLogged;

        public static OptimizationRunResult Optimize(
            Crafting craftingLogic,
            Hero crafter,
            CraftingTemplate template,
            bool limitToInventory,
            OptimizationGoal goal,
            OptimizationEfficiency efficiency,
            MinimumCraftQuality damageMinimumQuality,
            int damageMinimumQualityChance)
        {
            damageMinimumQualityChance = Math.Max(0, Math.Min(100, damageMinimumQualityChance));
            bool usesDamageQualityGate = goal == OptimizationGoal.Damage && damageMinimumQualityChance > 0;
            bool expectedQualityScoringDisabled = (goal == OptimizationGoal.SellValue || usesDamageQualityGate) && !ExpectedQualityScoringAvailable;
            var behavior = Campaign.Current.GetCampaignBehavior<CraftingCampaignBehavior>();
            if (behavior == null || craftingLogic == null || crafter == null || template == null)
                return new OptimizationRunResult(null, null, expectedQualityScoringDisabled);

            // 1. Gather player's available materials
            int[] availableMaterials = new int[9];
            var smithingModel = Campaign.Current.Models.SmithingModel;
            if (limitToInventory)
            {
                var roster = MobileParty.MainParty.ItemRoster;
                for (int i = 0; i < 9; i++)
                {
                    var matType = (CraftingMaterials)i;
                    var matItem = smithingModel.GetCraftingMaterialItem(matType);
                    availableMaterials[i] = matItem != null ? roster.GetItemNumber(matItem) : 0;
                }
            }

            // Identify usable pieces in the selected template only.
            var emptyPiece = WeaponDesignElement.GetInvalidPieceForType(CraftingPiece.PieceTypes.Blade).CraftingPiece;

            List<CraftingPiece> blades = template.IsPieceTypeUsable(CraftingPiece.PieceTypes.Blade)
                ? GetOpenedPieces(template, CraftingPiece.PieceTypes.Blade, behavior)
                : new List<CraftingPiece> { emptyPiece };

            List<CraftingPiece> guards = template.IsPieceTypeUsable(CraftingPiece.PieceTypes.Guard)
                ? GetOpenedPieces(template, CraftingPiece.PieceTypes.Guard, behavior)
                : new List<CraftingPiece> { emptyPiece };

            List<CraftingPiece> grips = template.IsPieceTypeUsable(CraftingPiece.PieceTypes.Handle)
                ? GetOpenedPieces(template, CraftingPiece.PieceTypes.Handle, behavior)
                : new List<CraftingPiece> { emptyPiece };

            List<CraftingPiece> pommels = template.IsPieceTypeUsable(CraftingPiece.PieceTypes.Pommel)
                ? GetOpenedPieces(template, CraftingPiece.PieceTypes.Pommel, behavior)
                : new List<CraftingPiece> { emptyPiece };

            var originalDesign = craftingLogic.CurrentWeaponDesign;
            try
            {
                var current = CreateCandidateFromDesign(originalDesign, emptyPiece);
                CraftingDesignCandidate? scoredCurrent = null;
                if (current != null && IsValid(current, limitToInventory, availableMaterials) &&
                    EvaluateCandidate(current, craftingLogic, crafter, template, goal, efficiency, damageMinimumQuality, damageMinimumQualityChance))
                    scoredCurrent = current;

                if (blades.Count == 0 || guards.Count == 0 || grips.Count == 0 || pommels.Count == 0)
                    return new OptimizationRunResult(scoredCurrent, null, expectedQualityScoringDisabled);

                CraftingDesignCandidate? best = null;
                if (scoredCurrent != null && (!limitToInventory || CanAfford(scoredCurrent.MaterialCosts, availableMaterials)))
                {
                    best = Clone(scoredCurrent);
                }
                else
                {
                    var seed = CreateLowestCostSeed(blades, guards, grips, pommels);
                    if (IsValid(seed, limitToInventory, availableMaterials) &&
                        EvaluateCandidate(seed, craftingLogic, crafter, template, goal, efficiency, damageMinimumQuality, damageMinimumQualityChance))
                    {
                        best = seed;
                    }
                }

                if (best == null)
                    return new OptimizationRunResult(scoredCurrent, null, expectedQualityScoringDisabled);

                var slots = new[] { blades, guards, grips, pommels };
                for (int pass = 0; pass < MaximumPartPasses; pass++)
                {
                    bool improved = false;
                    for (int slot = 0; slot < slots.Length; slot++)
                    {
                        foreach (var piece in slots[slot])
                        {
                            var trial = Clone(best);
                            SetPiece(trial, slot, piece);
                            if (!IsValid(trial, limitToInventory, availableMaterials) ||
                                !EvaluateCandidate(trial, craftingLogic, crafter, template, goal, efficiency, damageMinimumQuality, damageMinimumQualityChance) ||
                                trial.Score <= best.Score)
                                continue;

                            best = trial;
                            improved = true;
                        }
                    }

                    if (!improved) break;
                }

                OptimizeSliders(ref best, craftingLogic, crafter, template, goal, efficiency, damageMinimumQuality, damageMinimumQualityChance, limitToInventory, availableMaterials);

                string message = $"Optimized template '{template?.TemplateName?.ToString() ?? "Unknown"}' (Goal: {goal}). Best design: " +
                    $"Blade={best.Blade?.Name?.ToString() ?? "None"}, Guard={best.Guard?.Name?.ToString() ?? "None"}, " +
                    $"Grip={best.Grip?.Name?.ToString() ?? "None"}, Pommel={best.Pommel?.Name?.ToString() ?? "None"}. " +
                    $"Score={best.Score:F1}, Value={best.Value}, XP={best.SmithingXp}, Damage={best.MaxDamage}, " +
                    $"Stamina={best.StaminaCost}, MaterialValue={best.MaterialReferenceValue}, QualityChance={best.QualityChance:P0}, " +
                    $"Difficulty={best.Difficulty}, Smithing={crafter.GetSkillValue(DefaultSkills.Crafting)}, Slider Scales: " +
                    $"[{best.BladeScale}%, {best.GuardScale}%, {best.GripScale}%, {best.PommelScale}%]";
                WriteLog(message);
                return new OptimizationRunResult(scoredCurrent, best, expectedQualityScoringDisabled);
            }
            finally
            {
                SetCurrentWeaponDesign(craftingLogic, originalDesign);
            }
        }

        private static List<CraftingPiece> GetOpenedPieces(CraftingTemplate template, CraftingPiece.PieceTypes type, CraftingCampaignBehavior behavior)
        {
            var pieces = new List<CraftingPiece>();
            foreach (var piece in template.Pieces)
            {
                if (piece.PieceType == type && behavior.IsOpened(piece, template)) pieces.Add(piece);
            }
            return pieces;
        }

        private static void AccumulateCosts(CraftingPiece piece, int[] costs)
        {
            if (piece == null || piece.IsEmptyPiece) return;
            foreach (var cost in piece.MaterialsUsed)
            {
                int index = (int)cost.Item1;
                if (index >= 0 && index < 9)
                {
                    costs[index] += cost.Item2;
                }
            }
        }

        private static bool CanAfford(int[] costs, int[] available)
        {
            for (int i = 0; i < 9; i++)
            {
                if (costs[i] > available[i]) return false;
            }
            return true;
        }

        private static bool EvaluateCandidate(
            CraftingDesignCandidate candidate,
            Crafting craftingLogic,
            Hero crafter,
            CraftingTemplate template,
            OptimizationGoal goal,
            OptimizationEfficiency efficiency,
            MinimumCraftQuality damageMinimumQuality,
            int damageMinimumQualityChance)
        {
            var design = CreateDesign(candidate, template);
            candidate.MaterialCosts = Campaign.Current.Models.SmithingModel.GetSmithingCostsForWeaponDesign(design);
            candidate.Difficulty = Campaign.Current.Models.SmithingModel.CalculateWeaponDesignDifficulty(design);

            SetCurrentWeaponDesign(craftingLogic, design);
            ItemObject item = craftingLogic.GetCurrentCraftedItemObject(true, "optimizer");

            if (item == null)
            {
                return false;
            }

            candidate.QualityChance = 1f;
            if (goal == OptimizationGoal.Damage && damageMinimumQualityChance > 0)
            {
                if (!TryGetModifierQualityProbabilities(design, crafter, out var probabilities)) return false;
                candidate.QualityChance = 0f;
                foreach (var probability in probabilities)
                {
                    if ((int)probability.Item1 >= (int)damageMinimumQuality)
                        candidate.QualityChance += probability.Item2;
                }
                if (candidate.QualityChance < damageMinimumQualityChance / 100f) return false;
            }

            int value = item.Value;
            candidate.UsesExpectedQualityScore = false;
            if (goal == OptimizationGoal.SellValue && TryGetExpectedValue(design, crafter, item, out int expectedValue))
            {
                value = expectedValue;
                candidate.UsesExpectedQualityScore = true;
            }
            int maxDmg = 0;
            if (item.WeaponComponent != null && item.WeaponComponent.PrimaryWeapon != null)
            {
                var w = item.WeaponComponent.PrimaryWeapon;
                maxDmg = Math.Max(w.SwingDamage, w.ThrustDamage);
            }

            candidate.Value = value;
            // Vanilla awards free-build XP from this base ItemObject before the quality modifier is attached.
            // Its expected value across all quality outcomes is therefore this same exact game-model result.
            candidate.SmithingXp = Campaign.Current.Models.SmithingModel.GetSkillXpForSmithingInFreeBuildMode(item);
            candidate.MaxDamage = maxDmg;
            candidate.StaminaCost = Campaign.Current.Models.SmithingModel.GetEnergyCostForSmithing(item, crafter);
            candidate.MaterialReferenceValue = GetMaterialReferenceValue(candidate.MaterialCosts);

            float rawScore;
            switch (goal)
            {
                case OptimizationGoal.Damage:
                    rawScore = maxDmg;
                    break;
                case OptimizationGoal.SmithingXp:
                    rawScore = candidate.SmithingXp;
                    break;
                default:
                    rawScore = value;
                    break;
            }
            candidate.Score = ApplyEfficiency(rawScore, candidate, efficiency);
            return true;
        }

        private static WeaponDesignElement CreatePieceElement(CraftingPiece piece, int scale)
        {
            if (piece == null || piece.IsEmptyPiece)
            {
                return WeaponDesignElement.GetInvalidPieceForType(CraftingPiece.PieceTypes.Blade);
            }
            return WeaponDesignElement.CreateUsablePiece(piece, scale);
        }

        private static void OptimizeSliders(ref CraftingDesignCandidate candidate, Crafting craftingLogic, Hero crafter, CraftingTemplate template, OptimizationGoal goal, OptimizationEfficiency efficiency, MinimumCraftQuality damageMinimumQuality, int damageMinimumQualityChance, bool limitToInventory, int[] availableMaterials)
        {
            int[] deltas = { 5, -5, 1, -1 };
            for (int pass = 0; pass < MaximumSliderPasses; pass++)
            {
                bool improved = false;
                for (int slot = 0; slot < 4; slot++)
                {
                    foreach (int delta in deltas)
                    {
                        var trial = Clone(candidate);
                        int previous = GetScale(trial, slot);
                        if (GetPiece(trial, slot).IsEmptyPiece) continue;
                        int next = Math.Max(90, Math.Min(110, previous + delta));
                        if (next == previous) continue;
                        SetScale(trial, slot, next);
                        if (!IsValid(trial, limitToInventory, availableMaterials) ||
                            !EvaluateCandidate(trial, craftingLogic, crafter, template, goal, efficiency, damageMinimumQuality, damageMinimumQualityChance) ||
                            trial.Score <= candidate.Score)
                            continue;

                        candidate = trial;
                        improved = true;
                    }
                }
                if (!improved) break;
            }
        }

        private static CraftingDesignCandidate CreateLowestCostSeed(List<CraftingPiece> blades, List<CraftingPiece> guards, List<CraftingPiece> grips, List<CraftingPiece> pommels)
        {
            return new CraftingDesignCandidate
            {
                Blade = GetLowestCostPiece(blades),
                Guard = GetLowestCostPiece(guards),
                Grip = GetLowestCostPiece(grips),
                Pommel = GetLowestCostPiece(pommels)
            };
        }

        private static CraftingPiece GetLowestCostPiece(List<CraftingPiece> pieces)
        {
            CraftingPiece best = pieces[0];
            int bestCost = GetPieceMaterialCost(best);
            for (int i = 1; i < pieces.Count; i++)
            {
                int cost = GetPieceMaterialCost(pieces[i]);
                if (cost < bestCost) { best = pieces[i]; bestCost = cost; }
            }
            return best;
        }

        private static int GetPieceMaterialCost(CraftingPiece piece)
        {
            if (piece == null || piece.IsEmptyPiece) return 0;
            int cost = 0;
            foreach (var material in piece.MaterialsUsed) cost += material.Item2;
            return cost;
        }

        private static CraftingDesignCandidate? CreateCandidateFromDesign(WeaponDesign design, CraftingPiece emptyPiece)
        {
            if (design == null || design.UsedPieces == null || design.UsedPieces.Length < 4) return null;
            return new CraftingDesignCandidate
            {
                Blade = design.UsedPieces[0]?.CraftingPiece ?? emptyPiece,
                Guard = design.UsedPieces[1]?.CraftingPiece ?? emptyPiece,
                Grip = design.UsedPieces[2]?.CraftingPiece ?? emptyPiece,
                Pommel = design.UsedPieces[3]?.CraftingPiece ?? emptyPiece,
                BladeScale = design.UsedPieces[0]?.ScalePercentage ?? 100,
                GuardScale = design.UsedPieces[1]?.ScalePercentage ?? 100,
                GripScale = design.UsedPieces[2]?.ScalePercentage ?? 100,
                PommelScale = design.UsedPieces[3]?.ScalePercentage ?? 100
            };
        }

        private static CraftingDesignCandidate Clone(CraftingDesignCandidate source)
        {
            return new CraftingDesignCandidate
            {
                Blade = source.Blade, Guard = source.Guard, Grip = source.Grip, Pommel = source.Pommel,
                BladeScale = source.BladeScale, GuardScale = source.GuardScale,
                GripScale = source.GripScale, PommelScale = source.PommelScale,
                Score = source.Score, Value = source.Value, SmithingXp = source.SmithingXp,
                MaxDamage = source.MaxDamage, Difficulty = source.Difficulty,
                StaminaCost = source.StaminaCost, MaterialReferenceValue = source.MaterialReferenceValue,
                QualityChance = source.QualityChance,
                UsesExpectedQualityScore = source.UsesExpectedQualityScore,
                MaterialCosts = (int[])source.MaterialCosts.Clone()
            };
        }

        private static bool IsExpectedQualityScoringAvailable()
        {
            return ModifierQualityProbabilitiesMethod != null && EquipmentElementItemValueProperty != null &&
                   typeof(DefaultSmithingModel).Assembly.ManifestModule.ModuleVersionId == SupportedCampaignSystemMvid;
        }

        private static bool TryGetExpectedValue(WeaponDesign design, Hero crafter, ItemObject item, out int expectedValue)
        {
            expectedValue = item.Value;
            try
            {
                if (!TryGetModifierQualityProbabilities(design, crafter, out var probabilities)) return false;

                float total = 0f;
                foreach (var probability in probabilities)
                {
                    var modifiers = design.Template.ItemModifierGroup.GetModifiersBasedOnQuality(probability.Item1);
                    if (modifiers == null || modifiers.Count == 0)
                    {
                        total += probability.Item2 * item.Value;
                        continue;
                    }

                    float qualityValue = 0f;
                    foreach (var modifier in modifiers)
                    {
                        var equipment = new EquipmentElement(item, modifier, null, false);
                        qualityValue += (int)EquipmentElementItemValueProperty!.GetValue(equipment)!;
                    }
                    total += probability.Item2 * (qualityValue / modifiers.Count);
                }

                expectedValue = (int)Math.Round(total, MidpointRounding.AwayFromZero);
                return true;
            }
            catch (Exception)
            {
                if (!_expectedQualityCompatibilityLogged)
                {
                    _expectedQualityCompatibilityLogged = true;
                    WriteLog("Expected-quality scoring is disabled because the game's quality data could not be read safely. Sell Value uses the base crafted-item value until Smithing Optimizer is updated.");
                }
                return false;
            }
        }

        private static bool TryGetModifierQualityProbabilities(WeaponDesign design, Hero crafter, out List<ValueTuple<ItemQuality, float>> probabilities)
        {
            probabilities = null!;
            if (!ExpectedQualityScoringAvailable)
            {
                LogQualityCompatibilityFailure("the supported TaleWorlds.CampaignSystem assembly changed");
                return false;
            }

            try
            {
                probabilities = ModifierQualityProbabilitiesMethod!.Invoke(null, new object[] { design, crafter })
                    as List<ValueTuple<ItemQuality, float>> ?? null!;
                if (probabilities != null && probabilities.Count > 0) return true;
            }
            catch (Exception)
            {
                // The common fallback below logs once and leaves the candidate unscored.
            }

            LogQualityCompatibilityFailure("the game's quality data could not be read safely");
            return false;
        }

        private static void LogQualityCompatibilityFailure(string reason)
        {
            if (_expectedQualityCompatibilityLogged) return;
            _expectedQualityCompatibilityLogged = true;
            WriteLog($"Expected-quality scoring and damage quality gating are disabled because {reason}. Sell Value uses base item value until Smithing Optimizer is updated.");
        }

        private static int GetMaterialReferenceValue(int[] materialCosts)
        {
            int total = 0;
            var smithingModel = Campaign.Current.Models.SmithingModel;
            for (int i = 0; i < materialCosts.Length; i++)
            {
                if (materialCosts[i] <= 0) continue;
                var material = smithingModel.GetCraftingMaterialItem((CraftingMaterials)i);
                total += materialCosts[i] * (material?.Value ?? 0);
            }
            return total;
        }

        private static float ApplyEfficiency(float rawScore, CraftingDesignCandidate candidate, OptimizationEfficiency efficiency)
        {
            return efficiency switch
            {
                OptimizationEfficiency.PerStamina => rawScore / Math.Max(1, candidate.StaminaCost),
                OptimizationEfficiency.PerMaterialValue => rawScore / Math.Max(1, candidate.MaterialReferenceValue),
                _ => rawScore
            };
        }

        private static WeaponDesign CreateDesign(CraftingDesignCandidate candidate, CraftingTemplate template)
        {
            return new WeaponDesign(template, new TextObject("Optimized Weapon"), new[]
            {
                CreatePieceElement(candidate.Blade, candidate.BladeScale),
                CreatePieceElement(candidate.Guard, candidate.GuardScale),
                CreatePieceElement(candidate.Grip, candidate.GripScale),
                CreatePieceElement(candidate.Pommel, candidate.PommelScale)
            }, "optimizer");
        }

        private static bool IsValid(CraftingDesignCandidate candidate, bool limitToInventory, int[] availableMaterials)
        {
            int[] costs = new int[9];
            AccumulateCosts(candidate.Blade, costs);
            AccumulateCosts(candidate.Guard, costs);
            AccumulateCosts(candidate.Grip, costs);
            AccumulateCosts(candidate.Pommel, costs);
            candidate.MaterialCosts = costs;
            return !limitToInventory || CanAfford(costs, availableMaterials);
        }

        private static CraftingPiece GetPiece(CraftingDesignCandidate candidate, int slot) => slot switch
        {
            0 => candidate.Blade, 1 => candidate.Guard, 2 => candidate.Grip, _ => candidate.Pommel
        };

        private static void SetPiece(CraftingDesignCandidate candidate, int slot, CraftingPiece piece)
        {
            switch (slot) { case 0: candidate.Blade = piece; break; case 1: candidate.Guard = piece; break; case 2: candidate.Grip = piece; break; default: candidate.Pommel = piece; break; }
        }

        private static int GetScale(CraftingDesignCandidate candidate, int slot) => slot switch
        {
            0 => candidate.BladeScale, 1 => candidate.GuardScale, 2 => candidate.GripScale, _ => candidate.PommelScale
        };

        private static void SetScale(CraftingDesignCandidate candidate, int slot, int scale)
        {
            switch (slot) { case 0: candidate.BladeScale = scale; break; case 1: candidate.GuardScale = scale; break; case 2: candidate.GripScale = scale; break; default: candidate.PommelScale = scale; break; }
        }

        private static void SetCurrentWeaponDesign(Crafting craftingLogic, WeaponDesign design)
        {
            if (craftingLogic == null) return;
            try
            {
                if (CurrentWeaponDesignProperty != null)
                {
                    CurrentWeaponDesignProperty.SetValue(craftingLogic, design);
                }
            }
            catch (Exception)
            {
                // Ignore errors
            }
        }

        private static void WriteLog(string message)
        {
            try
            {
                string path = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Mount and Blade II Bannerlord",
                    "Configs",
                    "SmithingOptimizer_Log.txt"
                );
                string dir = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                {
                    System.IO.Directory.CreateDirectory(dir);
                }
                System.IO.File.AppendAllText(path, "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + message + "\n");
            }
            catch {}
        }
    }
}
