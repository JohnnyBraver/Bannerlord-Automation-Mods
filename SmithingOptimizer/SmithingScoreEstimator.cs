using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace SmithingOptimizer
{
    public enum SmithingTrainingActionKind
    {
        Craft,
        Smelt,
        Refine
    }

    public sealed class SmithingTrainingActionScore
    {
        public SmithingTrainingActionKind Kind { get; set; }
        public string Description { get; set; } = string.Empty;
        public int RawXp { get; set; }
        public int PartResearchGain { get; set; }
        public int StaminaCost { get; set; }
        public int Difficulty { get; set; }
        public int CrafterSkill { get; set; }
        public int AvailableCount { get; set; } = 1;
        public int Value { get; set; }
        public string ItemModifierName { get; set; } = string.Empty;
        public string KnownPerks { get; set; } = string.Empty;
        public float XpPerStamina => RawXp / (float)Math.Max(1, StaminaCost);
    }

    public static class SmithingScoreEstimator
    {
        public static SmithingTrainingActionScore? ScoreCraftingItem(ItemObject item, WeaponDesign design, Hero crafter)
        {
            try
            {
                var smithingModel = Campaign.Current?.Models?.SmithingModel;
                if (smithingModel == null || item == null || design == null || crafter == null)
                {
                    return null;
                }

                ItemModifier modifier = smithingModel.GetCraftedWeaponModifier(design, crafter);
                int expectedValue = GetExpectedSellValue(item, modifier);
                int rawXp = smithingModel.GetSkillXpForSmithingInFreeBuildMode(item);
                int stamina = smithingModel.GetEnergyCostForSmithing(item, crafter);

                return new SmithingTrainingActionScore
                {
                    Kind = SmithingTrainingActionKind.Craft,
                    Description = $"Craft {item.Name?.ToString() ?? "weapon"}",
                    RawXp = rawXp,
                    PartResearchGain = smithingModel.GetPartResearchGainForSmithingItem(item, crafter, true),
                    StaminaCost = stamina,
                    Difficulty = smithingModel.CalculateWeaponDesignDifficulty(design),
                    CrafterSkill = crafter.GetSkillValue(DefaultSkills.Crafting),
                    Value = expectedValue,
                    ItemModifierName = modifier?.Name?.ToString() ?? string.Empty,
                    KnownPerks = BuildKnownSmithingPerkSummary(crafter)
                };
            }
            catch (Exception ex)
            {
                OptimizerEngine.WriteLog($"Failed to score crafting XP: {ex.Message}");
                return null;
            }
        }

        public static SmithingTrainingActionScore? FindBestAlternative(Hero crafter)
        {
            return EnumerateAlternativeTrainingActions(crafter)
                .OrderByDescending(score => score.XpPerStamina)
                .ThenByDescending(score => score.RawXp)
                .ThenByDescending(score => score.PartResearchGain)
                .FirstOrDefault();
        }

        public static IEnumerable<SmithingTrainingActionScore> EnumerateAlternativeTrainingActions(Hero crafter)
        {
            if (crafter == null)
            {
                yield break;
            }

            foreach (var score in EnumerateSmeltingScores(crafter))
            {
                yield return score;
            }

            foreach (var score in EnumerateRefiningScores(crafter))
            {
                yield return score;
            }
        }

        public static string FormatRecommendation(SmithingTrainingActionScore score, CraftingDesignCandidate bestCraft)
        {
            string count = score.AvailableCount > 1 ? $" ({score.AvailableCount} available)" : string.Empty;
            string research = score.PartResearchGain > 0 ? $", {score.PartResearchGain} part research" : string.Empty;
            string perks = string.IsNullOrWhiteSpace(score.KnownPerks) ? string.Empty : $", perks: {score.KnownPerks}";
            string alternativeDetails = $"{score.RawXp} XP / {score.StaminaCost} stamina = {score.XpPerStamina:F2}{research}{perks}";
            return $"Smithing Optimizer: Better raw XP/stamina: {score.Description}{count} " +
                $"({alternativeDetails}) vs craft ({bestCraft.RawXp} XP / {bestCraft.StaminaCost} stamina = {bestCraft.XpPerStamina:F2}).";
        }

        private static IEnumerable<SmithingTrainingActionScore> EnumerateSmeltingScores(Hero crafter)
        {
            var smithingModel = Campaign.Current?.Models?.SmithingModel;
            var roster = MobileParty.MainParty?.ItemRoster;
            if (smithingModel == null || roster == null)
            {
                yield break;
            }

            for (int index = 0; index < roster.Count; index++)
            {
                ItemRosterElement rosterElement = roster.GetElementCopyAtIndex(index);
                if (rosterElement.Amount <= 0 ||
                    rosterElement.EquipmentElement.IsEmpty ||
                    rosterElement.EquipmentElement.IsQuestItem)
                {
                    continue;
                }

                ItemObject item = rosterElement.EquipmentElement.Item;
                if (item == null || item.WeaponComponent == null)
                {
                    continue;
                }

                SmithingTrainingActionScore? score = ScoreSmeltingItem(item, rosterElement.Amount, crafter);
                if (score != null)
                {
                    yield return score;
                }
            }
        }

        private static SmithingTrainingActionScore? ScoreSmeltingItem(ItemObject item, int availableCount, Hero crafter)
        {
            try
            {
                var smithingModel = Campaign.Current?.Models?.SmithingModel;
                if (smithingModel == null)
                {
                    return null;
                }

                int rawXp = smithingModel.GetSkillXpForSmelting(item);
                int stamina = smithingModel.GetEnergyCostForSmelting(item, crafter);
                if (rawXp <= 0 || stamina <= 0)
                {
                    return null;
                }

                return new SmithingTrainingActionScore
                {
                    Kind = SmithingTrainingActionKind.Smelt,
                    Description = $"Smelt {item.Name?.ToString() ?? "weapon"}",
                    RawXp = rawXp,
                    PartResearchGain = smithingModel.GetPartResearchGainForSmeltingItem(item, crafter),
                    StaminaCost = stamina,
                    CrafterSkill = crafter.GetSkillValue(DefaultSkills.Crafting),
                    AvailableCount = availableCount,
                    Value = item.Value,
                    KnownPerks = BuildKnownSmithingPerkSummary(crafter)
                };
            }
            catch (Exception ex)
            {
                OptimizerEngine.WriteLog($"Failed to score smelting XP for {item?.StringId ?? "unknown item"}: {ex.Message}");
                return null;
            }
        }

        private static IEnumerable<SmithingTrainingActionScore> EnumerateRefiningScores(Hero crafter)
        {
            var smithingModel = Campaign.Current?.Models?.SmithingModel;
            var roster = MobileParty.MainParty?.ItemRoster;
            if (smithingModel == null || roster == null)
            {
                yield break;
            }

            IEnumerable<Crafting.RefiningFormula> formulas;
            try
            {
                formulas = smithingModel.GetRefiningFormulas(crafter).ToList();
            }
            catch (Exception ex)
            {
                OptimizerEngine.WriteLog($"Failed to enumerate refining formulas: {ex.Message}");
                yield break;
            }

            foreach (Crafting.RefiningFormula sourceFormula in formulas)
            {
                if (!CanAffordRefinement(sourceFormula))
                {
                    continue;
                }

                SmithingTrainingActionScore? score = ScoreRefiningFormula(sourceFormula, crafter);
                if (score != null)
                {
                    yield return score;
                }
            }

            bool CanAffordRefinement(Crafting.RefiningFormula formula)
            {
                return HasMaterial(formula.Input1, formula.Input1Count) &&
                    HasMaterial(formula.Input2, formula.Input2Count);
            }

            bool HasMaterial(CraftingMaterials material, int count)
            {
                if (count <= 0)
                {
                    return true;
                }

                ItemObject materialItem = smithingModel.GetCraftingMaterialItem(material);
                return materialItem != null && roster.GetItemNumber(materialItem) >= count;
            }
        }

        private static SmithingTrainingActionScore? ScoreRefiningFormula(Crafting.RefiningFormula sourceFormula, Hero crafter)
        {
            try
            {
                var smithingModel = Campaign.Current?.Models?.SmithingModel;
                if (smithingModel == null)
                {
                    return null;
                }

                Crafting.RefiningFormula formulaForXp = sourceFormula;
                Crafting.RefiningFormula formulaForStamina = sourceFormula;
                int rawXp = smithingModel.GetSkillXpForRefining(ref formulaForXp);
                int stamina = smithingModel.GetEnergyCostForRefining(ref formulaForStamina, crafter);
                if (rawXp <= 0 || stamina <= 0)
                {
                    return null;
                }

                return new SmithingTrainingActionScore
                {
                    Kind = SmithingTrainingActionKind.Refine,
                    Description = FormatRefiningFormula(sourceFormula),
                    RawXp = rawXp,
                    StaminaCost = stamina,
                    CrafterSkill = crafter.GetSkillValue(DefaultSkills.Crafting),
                    KnownPerks = BuildKnownSmithingPerkSummary(crafter)
                };
            }
            catch (Exception ex)
            {
                OptimizerEngine.WriteLog($"Failed to score refining XP: {ex.Message}");
                return null;
            }
        }

        private static string FormatRefiningFormula(Crafting.RefiningFormula formula)
        {
            string input = FormatMaterialAmount(formula.Input1, formula.Input1Count);
            if (formula.Input2Count > 0)
            {
                input += " + " + FormatMaterialAmount(formula.Input2, formula.Input2Count);
            }

            string output = FormatMaterialAmount(formula.Output, formula.OutputCount);
            if (formula.Output2Count > 0)
            {
                output += " + " + FormatMaterialAmount(formula.Output2, formula.Output2Count);
            }

            return $"Refine {input} -> {output}";
        }

        private static int GetExpectedSellValue(ItemObject item, ItemModifier modifier)
        {
            try
            {
                var equipmentElement = new EquipmentElement(item, modifier, null, false);
                return equipmentElement.ItemValue;
            }
            catch (Exception ex)
            {
                OptimizerEngine.WriteLog($"Failed to score crafter-modified sell value for {item?.StringId ?? "unknown item"}: {ex.Message}");
                return item?.Value ?? 0;
            }
        }

        private static string FormatMaterialAmount(CraftingMaterials material, int count)
        {
            var smithingModel = Campaign.Current?.Models?.SmithingModel;
            string name = smithingModel?.GetCraftingMaterialItem(material)?.Name?.ToString() ?? material.ToString();
            return $"{count}x {name}";
        }

        private static string BuildKnownSmithingPerkSummary(Hero crafter)
        {
            var known = new List<string>();
            AddKnownPerk(crafter, DefaultPerks.Crafting.PracticalSmith, known, "Practical Smith");
            AddKnownPerk(crafter, DefaultPerks.Crafting.PracticalSmelter, known, "Practical Smelter");
            AddKnownPerk(crafter, DefaultPerks.Crafting.PracticalRefiner, known, "Practical Refiner");
            AddKnownPerk(crafter, DefaultPerks.Crafting.CuriousSmith, known, "Curious Smith");
            AddKnownPerk(crafter, DefaultPerks.Crafting.CuriousSmelter, known, "Curious Smelter");
            AddKnownPerk(crafter, DefaultPerks.Crafting.ExperiencedSmith, known, "Experienced Smith");
            AddKnownPerk(crafter, DefaultPerks.Crafting.MasterSmith, known, "Master Smith");
            AddKnownPerk(crafter, DefaultPerks.Crafting.LegendarySmith, known, "Legendary Smith");
            return string.Join(", ", known);
        }

        private static void AddKnownPerk(Hero crafter, PerkObject perk, List<string> known, string displayName)
        {
            try
            {
                if (perk != null && crafter.GetPerkValue(perk))
                {
                    known.Add(displayName);
                }
            }
            catch
            {
                // Perk objects may not be initialized during early load; scoring should still continue.
            }
        }
    }
}
