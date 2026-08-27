using HarmonyLib;
//using SmeltingOverhaul.UI;
using System;
using System.Collections.Generic;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace SmeltingOverhaul
{   
    [HarmonyPatch(typeof(CraftingVM), "ExecuteMainAction")]
    class FixExecuteMainActionPatch
    {     // =========================
          // 工具方法：判断是否可用金属补足零件
          // =========================
        private static bool CanCraftWithMetals(CraftingVM __instance, SmeltingMechanicBehavior behavior)
        {
            if (__instance.WeaponDesign == null) return false;

            // 遍历每个零件类别，只看已选中的零件
            Dictionary<CraftingMaterials, int> totalMetalCosts = new Dictionary<CraftingMaterials, int>();

            foreach (var pieceListVM in __instance.WeaponDesign.PieceLists)
            {
                var pieceVM = pieceListVM.SelectedPiece;
                if (pieceVM == null) continue;

                var piece = pieceVM.CraftingPiece;
                if (piece == null) continue;

                string pieceId = piece.CraftingPiece.StringId;

                if (pieceId!=null && behavior.GetPartCount(pieceId) > 0) continue; // 库存足够

                // 累加金属需求
                foreach (var mat in piece.CraftingPiece.MaterialsUsed)
                {
                    if (totalMetalCosts.ContainsKey(mat.Item1))
                        totalMetalCosts[mat.Item1] += mat.Item2;
                    else
                        totalMetalCosts[mat.Item1] = mat.Item2;
                }
            }

            // 检查玩家库存
            foreach (var kv in totalMetalCosts)
            {
                var materialItem = Campaign.Current.Models.SmithingModel.GetCraftingMaterialItem(kv.Key);
                int playerStock = MobileParty.MainParty.ItemRoster.GetItemNumber(materialItem);
                if (playerStock < kv.Value)
                    return false;
            }

            return true;
        }
        // =========================
        // Patch 2: 修复 HaveMaterialsNeeded，让 ExecuteMainAction 可用
        // =========================
        [HarmonyPatch("HaveMaterialsNeeded")]
        [HarmonyPostfix]
        public static void HaveMaterialsNeededPostfix(CraftingVM __instance, ref bool __result)
        {
            if (__result) return; // 原版材料充足
            var behavior = Campaign.Current?.GetCampaignBehavior<SmeltingMechanicBehavior>();
            if (behavior == null) return;

            if (CanCraftWithMetals(__instance, behavior))
            {
                __result = true; // 材料不足，但金属可补足
            }
        }
        static bool Prefix(CraftingVM __instance)
        {
            if (__instance.IsInSmeltingMode || __instance.IsInRefinementMode)
                return true; // 让原版处理熔炼/精炼

            if (__instance.IsMainActionEnabled) // 你的按钮已经亮
            {
                // 绕过原版 HaveMaterialsNeeded() 检查
                return true;
            }

            // 如果按钮没亮，也不用锻造
            return false;
        }
    }
    [HarmonyPatch(typeof(CraftingVM), "RefreshEnableMainAction")]
    public class FixCraftingButtonPatch
    {
        private static bool HaveEnergy(CraftingVM instance)
        {
            var currentCraftingHero = (CraftingAvailableHeroItemVM)AccessTools.Field(typeof(CraftingVM), "_currentCraftingHero").GetValue(instance);
            if (currentCraftingHero?.Hero == null) return true;

            var craftingBehavior = (ICraftingCampaignBehavior)AccessTools.Field(typeof(CraftingVM), "_craftingBehavior").GetValue(instance);
            return craftingBehavior.GetHeroCraftingStamina(currentCraftingHero.Hero) > 10;
        }
        /// <summary>
        /// 修复锻造界面按钮的可用性问题：
        /// 1. 当自由制作模式下零件不足时，原版错误禁用按钮（因未考虑金属补充）
        /// 2. 本补丁在原版检查后，用我们的混合资源逻辑重新判定
        /// 
        /// 【设计验证】
        /// - 仅在锻造模式（非熔炼/精炼）生效
        /// - 保留原版零件解锁检查
        /// - 精确计算零件不足时的金属需求
        /// - 100%匹配 `PrefixSpendMaterials` 的混合消耗逻辑
        /// </summary>
        static void Postfix(CraftingVM __instance)
        {
            // 1️⃣ 如果按钮已亮，说明原版已通过检查（材料/体力充足）
            if (__instance.IsMainActionEnabled) return;

            // 2️⃣ 排除体力不足/熔炼/精炼模式（保持原版行为）
            if (!HaveEnergy(__instance) || __instance.IsInSmeltingMode || __instance.IsInRefinementMode) return;

            // 3️⃣ 保留原版零件解锁检查（未解锁零件不能制作）
            if (__instance.WeaponDesign == null || !__instance.WeaponDesign.HaveUnlockedAllSelectedPieces()) return;

            var behavior = Campaign.Current?.GetCampaignBehavior<SmeltingMechanicBehavior>();
            if (behavior == null) return;

            var designVM = __instance.WeaponDesign;

            // 🔍 用字典存储总金属需求 (CraftingMaterials -> 数量)
            Dictionary<CraftingMaterials, int> totalMetalCosts = new Dictionary<CraftingMaterials, int>();

            // 遍历每个零件类别，只看已选中的零件
            foreach (var pieceListVM in designVM.PieceLists)
            {
                var pieceVM = pieceListVM.SelectedPiece; // 当前选中零件
                if (pieceVM == null) continue;

                var piece = pieceVM.CraftingPiece;
                if (piece == null) continue;

                string pieceId = piece.CraftingPiece.StringId;

                // 零件库存足够 -> 跳过金属
                if (pieceId!=null && behavior.GetPartCount(pieceId) > 0) continue;

                // 零件库存不足 -> 累加金属消耗
                foreach (var (material, amount) in piece.CraftingPiece.MaterialsUsed)
                {
                    if (!totalMetalCosts.ContainsKey(material))
                        totalMetalCosts[material] = 0;

                    totalMetalCosts[material] += amount;
                }
            }

            // 4️⃣ 检查玩家库存是否满足总金属需求
            bool canCraft = true;
            foreach (var kvp in totalMetalCosts)
            {
                var materialItem = Campaign.Current.Models.SmithingModel.GetCraftingMaterialItem(kvp.Key);
                int playerStock = MobileParty.MainParty.ItemRoster.GetItemNumber(materialItem);
                if (playerStock < kvp.Value)
                {
                    canCraft = false;
                    break; // 任意一种金属不足即失败
                }
            }

            // 5️⃣ 如果混合资源检查通过，强制启用按钮
            if (canCraft)
            {
                __instance.IsMainActionEnabled = true;
                __instance.MainActionHint = new TaleWorlds.Core.ViewModelCollection.Information.BasicTooltipViewModel();
                InformationManager.DisplayMessage(
                    new InformationMessage("锻造按钮已启用（零件不足部分用金属补充）", Color.FromUint(0xFF00FF00)));
            }

 
        }
    }
}