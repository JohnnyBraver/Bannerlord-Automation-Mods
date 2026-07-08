using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
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
        public int MaxDamage { get; set; }
        public int RawXp { get; set; }
        public int PartResearchGain { get; set; }
        public int StaminaCost { get; set; }
        public float XpPerStamina { get; set; }
        public int Difficulty { get; set; }
        public string ItemModifierName { get; set; } = string.Empty;
        public int[] MaterialCosts { get; set; } = new int[9];
    }

    public class OptimizerEngine
    {
        public static List<CraftingDesignCandidate> Optimize(
            Crafting craftingLogic,
            Hero crafter,
            CraftingTemplate template,
            bool limitToInventory,
            OptimizationGoal goal)
        {
            var results = new List<CraftingDesignCandidate>();
            var behavior = Campaign.Current.GetCampaignBehavior<CraftingCampaignBehavior>();
            if (behavior == null) return results;

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

            // 2. Identify usable pieces that are unlocked
            var emptyPiece = WeaponDesignElement.GetInvalidPieceForType(CraftingPiece.PieceTypes.Blade).CraftingPiece;

            List<CraftingPiece> blades = template.IsPieceTypeUsable(CraftingPiece.PieceTypes.Blade)
                ? template.Pieces.Where(p => p.PieceType == CraftingPiece.PieceTypes.Blade && behavior.IsOpened(p, template)).ToList()
                : new List<CraftingPiece> { emptyPiece };

            List<CraftingPiece> guards = template.IsPieceTypeUsable(CraftingPiece.PieceTypes.Guard)
                ? template.Pieces.Where(p => p.PieceType == CraftingPiece.PieceTypes.Guard && behavior.IsOpened(p, template)).ToList()
                : new List<CraftingPiece> { emptyPiece };

            List<CraftingPiece> grips = template.IsPieceTypeUsable(CraftingPiece.PieceTypes.Handle)
                ? template.Pieces.Where(p => p.PieceType == CraftingPiece.PieceTypes.Handle && behavior.IsOpened(p, template)).ToList()
                : new List<CraftingPiece> { emptyPiece };

            List<CraftingPiece> pommels = template.IsPieceTypeUsable(CraftingPiece.PieceTypes.Pommel)
                ? template.Pieces.Where(p => p.PieceType == CraftingPiece.PieceTypes.Pommel && behavior.IsOpened(p, template)).ToList()
                : new List<CraftingPiece> { emptyPiece };

            // Backup original design
            var originalDesign = craftingLogic.CurrentWeaponDesign;

            // 3. Phase 1: Grid Search over part combinations at default scale (100)
            var candidates = new List<CraftingDesignCandidate>();

            foreach (var blade in blades)
            {
                foreach (var guard in guards)
                {
                    foreach (var grip in grips)
                    {
                        foreach (var pommel in pommels)
                        {
                            // Calculate total material costs
                            int[] costs = new int[9];
                            AccumulateCosts(blade, costs);
                            AccumulateCosts(guard, costs);
                            AccumulateCosts(grip, costs);
                            AccumulateCosts(pommel, costs);

                            // Check budget constraints
                            if (limitToInventory && !CanAfford(costs, availableMaterials))
                                continue;

                            var candidate = new CraftingDesignCandidate
                            {
                                Blade = blade,
                                Guard = guard,
                                Grip = grip,
                                Pommel = pommel,
                                MaterialCosts = costs
                            };

                            // Evaluate score at default scale
                            EvaluateCandidate(candidate, craftingLogic, crafter, goal);
                            candidates.Add(candidate);
                        }
                    }
                }
            }

            // If no combinations match, return empty
            if (candidates.Count == 0)
            {
                SetCurrentWeaponDesign(craftingLogic, originalDesign);
                return results;
            }

            // Take the top 3 candidate combinations to fine-tune
            var topCandidates = candidates.OrderByDescending(c => c.Score).Take(3).ToList();

            // 4. Phase 2: Slider Hill-Climbing on the top candidates
            foreach (var candidate in topCandidates)
            {
                OptimizeSliders(candidate, craftingLogic, crafter, goal);
                results.Add(candidate);
            }

            // Restore original design
            SetCurrentWeaponDesign(craftingLogic, originalDesign);

            // Return sorted results
            var sortedResults = results.OrderByDescending(c => c.Score).ToList();
            if (sortedResults.Count > 0)
            {
                var best = sortedResults[0];
                string message = $"Optimized template '{template?.TemplateName?.ToString() ?? "Unknown"}' (Goal: {goal}). Best design: " +
                    $"Blade={best.Blade?.Name?.ToString() ?? "None"}, Guard={best.Guard?.Name?.ToString() ?? "None"}, " +
                    $"Grip={best.Grip?.Name?.ToString() ?? "None"}, Pommel={best.Pommel?.Name?.ToString() ?? "None"}. " +
                    $"Score={best.Score:F1}, RawXP={best.RawXp}, XP/Stamina={best.XpPerStamina:F2}, " +
                    $"Stamina={best.StaminaCost}, ExpectedValue={best.Value}, Modifier={best.ItemModifierName}, MaxDamage={best.MaxDamage}, Slider Scales: " +
                    $"[{best.BladeScale}%, {best.GuardScale}%, {best.GripScale}%, {best.PommelScale}%]";
                WriteLog(message);
            }
            return sortedResults;
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

        private static void EvaluateCandidate(CraftingDesignCandidate candidate, Crafting craftingLogic, Hero crafter, OptimizationGoal goal)
        {
            var pieces = new WeaponDesignElement[4];
            pieces[0] = CreatePieceElement(candidate.Blade, candidate.BladeScale);
            pieces[1] = CreatePieceElement(candidate.Guard, candidate.GuardScale);
            pieces[2] = CreatePieceElement(candidate.Grip, candidate.GripScale);
            pieces[3] = CreatePieceElement(candidate.Pommel, candidate.PommelScale);

            var design = new WeaponDesign(craftingLogic.CurrentCraftingTemplate, new TextObject("Optimized Weapon"), pieces, "optimizer");
            
            // Swap in design to evaluate using game's engine
            SetCurrentWeaponDesign(craftingLogic, design);
            ItemObject item = craftingLogic.GetCurrentCraftedItemObject(true, "optimizer");

            if (item == null)
            {
                candidate.Score = -1;
                return;
            }

            int value = item.Value;
            int maxDmg = 0;
            if (item.WeaponComponent != null && item.WeaponComponent.PrimaryWeapon != null)
            {
                var w = item.WeaponComponent.PrimaryWeapon;
                maxDmg = Math.Max(w.SwingDamage, w.ThrustDamage);
            }

            var craftingScore = SmithingScoreEstimator.ScoreCraftingItem(item, design, crafter);

            candidate.Value = craftingScore?.Value ?? value;
            candidate.MaxDamage = maxDmg;
            candidate.RawXp = craftingScore?.RawXp ?? 0;
            candidate.PartResearchGain = craftingScore?.PartResearchGain ?? 0;
            candidate.StaminaCost = craftingScore?.StaminaCost ?? 0;
            candidate.XpPerStamina = craftingScore?.XpPerStamina ?? 0;
            candidate.Difficulty = craftingScore?.Difficulty ?? 0;
            candidate.ItemModifierName = craftingScore?.ItemModifierName ?? string.Empty;

            if (goal == OptimizationGoal.Damage)
            {
                candidate.Score = maxDmg;
            }
            else if (goal == OptimizationGoal.SellValue)
            {
                candidate.Score = candidate.Value;
            }
            else
            {
                if (craftingScore == null || craftingScore.RawXp <= 0 || craftingScore.StaminaCost <= 0)
                {
                    candidate.Score = -1;
                    return;
                }

                candidate.Score = craftingScore.XpPerStamina;
            }
        }

        private static WeaponDesignElement CreatePieceElement(CraftingPiece piece, int scale)
        {
            if (piece == null || piece.IsEmptyPiece)
            {
                return WeaponDesignElement.GetInvalidPieceForType(CraftingPiece.PieceTypes.Blade);
            }
            return WeaponDesignElement.CreateUsablePiece(piece, scale);
        }

        private static void OptimizeSliders(CraftingDesignCandidate candidate, Crafting craftingLogic, Hero crafter, OptimizationGoal goal)
        {
            // Hill-climb sizes: starting at 100%, range is 90% to 110% (scale value 90 to 110)
            bool improved = true;
            int steps = 0;

            while (improved && steps < 15)
            {
                improved = false;
                steps++;

                // Try tweaking each slider up/down by 5 units first, then 1 unit
                int[] deltas = { 5, -5, 1, -1 };
                foreach (int delta in deltas)
                {
                    // Tweak Blade
                    if (!candidate.Blade.IsEmptyPiece)
                    {
                        int prev = candidate.BladeScale;
                        int next = Math.Max(90, Math.Min(110, prev + delta));
                        if (next != prev)
                        {
                            candidate.BladeScale = next;
                            float oldScore = candidate.Score;
                            var oldState = CandidateEvaluationState.Capture(candidate);
                            EvaluateCandidate(candidate, craftingLogic, crafter, goal);
                            if (candidate.Score > oldScore) { improved = true; continue; }
                            candidate.BladeScale = prev; // Revert
                            oldState.Restore(candidate);
                        }
                    }

                    // Tweak Guard
                    if (!candidate.Guard.IsEmptyPiece)
                    {
                        int prev = candidate.GuardScale;
                        int next = Math.Max(90, Math.Min(110, prev + delta));
                        if (next != prev)
                        {
                            candidate.GuardScale = next;
                            float oldScore = candidate.Score;
                            var oldState = CandidateEvaluationState.Capture(candidate);
                            EvaluateCandidate(candidate, craftingLogic, crafter, goal);
                            if (candidate.Score > oldScore) { improved = true; continue; }
                            candidate.GuardScale = prev; // Revert
                            oldState.Restore(candidate);
                        }
                    }

                    // Tweak Grip
                    if (!candidate.Grip.IsEmptyPiece)
                    {
                        int prev = candidate.GripScale;
                        int next = Math.Max(90, Math.Min(110, prev + delta));
                        if (next != prev)
                        {
                            candidate.GripScale = next;
                            float oldScore = candidate.Score;
                            var oldState = CandidateEvaluationState.Capture(candidate);
                            EvaluateCandidate(candidate, craftingLogic, crafter, goal);
                            if (candidate.Score > oldScore) { improved = true; continue; }
                            candidate.GripScale = prev; // Revert
                            oldState.Restore(candidate);
                        }
                    }

                    // Tweak Pommel
                    if (!candidate.Pommel.IsEmptyPiece)
                    {
                        int prev = candidate.PommelScale;
                        int next = Math.Max(90, Math.Min(110, prev + delta));
                        if (next != prev)
                        {
                            candidate.PommelScale = next;
                            float oldScore = candidate.Score;
                            var oldState = CandidateEvaluationState.Capture(candidate);
                            EvaluateCandidate(candidate, craftingLogic, crafter, goal);
                            if (candidate.Score > oldScore) { improved = true; continue; }
                            candidate.PommelScale = prev; // Revert
                            oldState.Restore(candidate);
                        }
                    }
                }
            }
        }

        private struct CandidateEvaluationState
        {
            private readonly float _score;
            private readonly int _value;
            private readonly int _maxDamage;
            private readonly int _rawXp;
            private readonly int _partResearchGain;
            private readonly int _staminaCost;
            private readonly float _xpPerStamina;
            private readonly int _difficulty;
            private readonly string _itemModifierName;

            private CandidateEvaluationState(CraftingDesignCandidate candidate)
            {
                _score = candidate.Score;
                _value = candidate.Value;
                _maxDamage = candidate.MaxDamage;
                _rawXp = candidate.RawXp;
                _partResearchGain = candidate.PartResearchGain;
                _staminaCost = candidate.StaminaCost;
                _xpPerStamina = candidate.XpPerStamina;
                _difficulty = candidate.Difficulty;
                _itemModifierName = candidate.ItemModifierName;
            }

            public static CandidateEvaluationState Capture(CraftingDesignCandidate candidate)
            {
                return new CandidateEvaluationState(candidate);
            }

            public void Restore(CraftingDesignCandidate candidate)
            {
                candidate.Score = _score;
                candidate.Value = _value;
                candidate.MaxDamage = _maxDamage;
                candidate.RawXp = _rawXp;
                candidate.PartResearchGain = _partResearchGain;
                candidate.StaminaCost = _staminaCost;
                candidate.XpPerStamina = _xpPerStamina;
                candidate.Difficulty = _difficulty;
                candidate.ItemModifierName = _itemModifierName;
            }
        }

        private static void SetCurrentWeaponDesign(Crafting craftingLogic, WeaponDesign design)
        {
            if (craftingLogic == null) return;
            try
            {
                var prop = typeof(Crafting).GetProperty("CurrentWeaponDesign", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (prop != null)
                {
                    prop.SetValue(craftingLogic, design);
                }
            }
            catch (Exception)
            {
                // Ignore errors
            }
        }

        internal static void WriteLog(string message)
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
