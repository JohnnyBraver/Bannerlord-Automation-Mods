using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace SmithingOptimizer
{
    public sealed class SmithingTrainingRecommendation
    {
        public string Description { get; }
        public int RawXp { get; }
        public int StaminaCost { get; }
        public int PartResearchGain { get; }
        public int AvailableCount { get; }
        public int MaterialReferenceValue { get; }
        public float XpPerStamina => RawXp / (float)Math.Max(1, StaminaCost);
        public float XpPerMaterialValue => RawXp / (float)Math.Max(1, MaterialReferenceValue);

        public SmithingTrainingRecommendation(string description, int rawXp, int staminaCost, int partResearchGain = 0, int availableCount = 1, int materialReferenceValue = 0)
        {
            Description = description;
            RawXp = rawXp;
            StaminaCost = staminaCost;
            PartResearchGain = partResearchGain;
            AvailableCount = availableCount;
            MaterialReferenceValue = materialReferenceValue;
        }
    }

    // Uses the game's XP and stamina methods, independently of crafting-design scoring.
    public static class SmithingTrainingAdvisor
    {
        public static SmithingTrainingRecommendation? FindBetterAlternative(Hero crafter, OptimizationEfficiency efficiency, float minimumScore)
        {
            if (crafter == null || Campaign.Current?.Models?.SmithingModel == null || MobileParty.MainParty?.ItemRoster == null)
                return null;

            // Recommendations are advisory. Stop at the first real improvement instead of
            // evaluating every item in a large player inventory to prove it is the absolute best.
            foreach (var candidate in EnumerateSmelting(crafter))
                if (GetScore(candidate, efficiency) > minimumScore + 0.001f) return candidate;
            foreach (var candidate in EnumerateRefining(crafter))
                if (GetScore(candidate, efficiency) > minimumScore + 0.001f) return candidate;
            return null;
        }

        public static float GetScore(SmithingTrainingRecommendation candidate, OptimizationEfficiency efficiency) => efficiency switch
        {
            OptimizationEfficiency.PerStamina => candidate.XpPerStamina,
            OptimizationEfficiency.PerMaterialValue => candidate.XpPerMaterialValue,
            _ => candidate.RawXp
        };

        private static IEnumerable<SmithingTrainingRecommendation> EnumerateSmelting(Hero crafter)
        {
            var smithingModel = Campaign.Current.Models.SmithingModel;
            var roster = MobileParty.MainParty.ItemRoster;
            for (int index = 0; index < roster.Count; index++)
            {
                ItemRosterElement entry = roster.GetElementCopyAtIndex(index);
                if (entry.Amount <= 0 || entry.EquipmentElement.IsEmpty || entry.EquipmentElement.IsQuestItem) continue;
                ItemObject item = entry.EquipmentElement.Item;
                if (item?.WeaponComponent == null) continue;

                int xp = smithingModel.GetSkillXpForSmelting(item);
                int stamina = smithingModel.GetEnergyCostForSmelting(item, crafter);
                if (xp <= 0 || stamina <= 0) continue;
                yield return new SmithingTrainingRecommendation(
                    $"Smelt {item.Name?.ToString() ?? "weapon"}", xp, stamina,
                    smithingModel.GetPartResearchGainForSmeltingItem(item, crafter), entry.Amount, item.Value);
            }
        }

        private static IEnumerable<SmithingTrainingRecommendation> EnumerateRefining(Hero crafter)
        {
            var smithingModel = Campaign.Current.Models.SmithingModel;
            var roster = MobileParty.MainParty.ItemRoster;
            foreach (var formula in smithingModel.GetRefiningFormulas(crafter))
            {
                if (!HasMaterial(formula.Input1, formula.Input1Count) || !HasMaterial(formula.Input2, formula.Input2Count)) continue;

                Crafting.RefiningFormula xpFormula = formula;
                Crafting.RefiningFormula staminaFormula = formula;
                int xp = smithingModel.GetSkillXpForRefining(ref xpFormula);
                int stamina = smithingModel.GetEnergyCostForRefining(ref staminaFormula, crafter);
                if (xp <= 0 || stamina <= 0) continue;
                yield return new SmithingTrainingRecommendation(FormatFormula(formula), xp, stamina, materialReferenceValue: GetInputMaterialValue(formula));
            }

            bool HasMaterial(CraftingMaterials material, int count)
            {
                if (count <= 0) return true;
                ItemObject item = smithingModel.GetCraftingMaterialItem(material);
                return item != null && roster.GetItemNumber(item) >= count;
            }
        }

        private static string FormatFormula(Crafting.RefiningFormula formula) =>
            $"Refine {FormatMaterial(formula.Input1, formula.Input1Count)} -> {FormatMaterial(formula.Output, formula.OutputCount)}";

        private static string FormatMaterial(CraftingMaterials material, int count)
        {
            string name = Campaign.Current.Models.SmithingModel.GetCraftingMaterialItem(material)?.Name?.ToString() ?? material.ToString();
            return $"{count}x {name}";
        }

        private static int GetInputMaterialValue(Crafting.RefiningFormula formula)
        {
            return GetMaterialValue(formula.Input1, formula.Input1Count) + GetMaterialValue(formula.Input2, formula.Input2Count);
        }

        private static int GetMaterialValue(CraftingMaterials material, int count)
        {
            if (count <= 0) return 0;
            return count * (Campaign.Current.Models.SmithingModel.GetCraftingMaterialItem(material)?.Value ?? 0);
        }
    }
}
