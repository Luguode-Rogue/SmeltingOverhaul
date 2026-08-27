using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.CraftingSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.GameState;
using HarmonyLib;

namespace SmeltingOverhaul
{
    [HarmonyPatch(typeof(CraftingCampaignBehavior))]
    public static class CraftingAndSmeltingPatches
    {
        // =============================
        // 当前制作模式记录
        // =============================
        private static bool _currentIsFreeMode = true;

        [HarmonyPatch("CreateCraftedWeaponInternal")]
        [HarmonyPrefix]
        static void PrefixCreateCraftedWeaponInternal(bool isFreeMode)
        {
            _currentIsFreeMode = isFreeMode;
        }

        // =============================
        // 屏蔽自由制作随机解锁
        // =============================
        [HarmonyPatch("AddResearchPoints")]
        [HarmonyPrefix]
        static bool BlockAddResearchPoints(
            CraftingCampaignBehavior __instance,
            CraftingTemplate craftingTemplate,
            int researchPoints)
        {
            // 自由制作屏蔽
            if (_currentIsFreeMode)
                return false;

            // 订单制作允许
            return true;
        }

        // =======================================================
        // 【修复版】混合消耗逻辑 (Hybrid Consumption)
        // =======================================================
        /// <summary>
        /// 替代原版的 SpendMaterials 方法。
        /// 实现逻辑：
        /// 1. 遍历所有配件。
        /// 2. 优先消耗“零件库存”。
        /// 3. 库存不足的配件，自动回退为消耗“金属材料”。
        /// </summary>
        [HarmonyPatch("SpendMaterials")]
        [HarmonyPrefix]
        static bool PrefixSpendMaterials(WeaponDesign weaponDesign)
        {
            var behavior = Campaign.Current?.GetCampaignBehavior<SmeltingMechanicBehavior>();
            if (behavior == null || weaponDesign?.UsedPieces == null)
                return true; // 安全回退原版

            // 统计金属消耗（9种材料）
            int[] totalMetalCosts = new int[9];

            // 逐零件处理
            foreach (var element in weaponDesign.UsedPieces)
            {
                var piece = element?.CraftingPiece;
                if (piece.BladeData == null || piece.StringId == null)
                    continue;

                // 尝试消耗零件库存
                bool consumed = behavior.ConsumePart(piece.StringId, 1);

                if (!consumed)
                {
                    // 库存不足 → 仅该零件走金属
                    var pieceCosts = piece.MaterialsUsed;

                    if (pieceCosts != null)
                    {

                        if (pieceCosts != null)
                        {
                            foreach (var cost in pieceCosts)
                            {
                                CraftingMaterials material = cost.Item1;
                                int amount = cost.Item2;

                                totalMetalCosts[(int)material] += amount;
                            }
                        }
                    }
                }
            }

            // 统一扣金属
            var partyItemRoster = MobileParty.MainParty.ItemRoster;
            var smithingModel = Campaign.Current.Models.SmithingModel;

            for (int i = 0; i < 9; i++)
            {
                if (totalMetalCosts[i] > 0)
                {
                    var materialEnum = (CraftingMaterials)i;
                    var materialItem = smithingModel.GetCraftingMaterialItem(materialEnum);

                    if (materialItem != null)
                    {
                        partyItemRoster.AddToCounts(materialItem, -totalMetalCosts[i]);
                    }
                }
            }
            // 完全跳过原版
            return false;
        }


        // =============================
        // 反射缓存（避免频繁调用）
        // =============================
        private static readonly MethodInfo _isOpenedMethod =
            AccessTools.Method(typeof(CraftingCampaignBehavior), "IsOpened");

        private static readonly MethodInfo _openPartMethod =
            AccessTools.Method(typeof(CraftingCampaignBehavior), "OpenPart");

        // =============================
        // 完全接管 DoSmelting
        // =============================
        [HarmonyPatch("DoSmelting")]
        [HarmonyPrefix]
        static bool PrefixDoSmelting(
            CraftingCampaignBehavior __instance,
            Hero currentCraftingHero,
            EquipmentElement equipmentElement)
        {
            if (currentCraftingHero == null || equipmentElement.Item == null)
                return false;

            var item = equipmentElement.Item;
            var design = item.WeaponDesign;

            if (design?.UsedPieces == null || design.Template == null)
                return false;

            var behavior = Campaign.Current.GetCampaignBehavior<SmeltingMechanicBehavior>();
            if (behavior == null)
                return false;

            var itemRoster = MobileParty.MainParty.ItemRoster;

            // 1️⃣ 扣装备
            itemRoster.AddToCounts(equipmentElement, -1);

            // 2️⃣ 给经验
            currentCraftingHero.AddSkillXp(
                DefaultSkills.Crafting,
                (float)Campaign.Current.Models.SmithingModel.GetSkillXpForSmelting(item));

            // 3️⃣ 扣体力
            int energyCost = Campaign.Current.Models.SmithingModel
                .GetEnergyCostForSmelting(item, currentCraftingHero);

            __instance.SetHeroCraftingStamina(
                currentCraftingHero,
                __instance.GetHeroCraftingStamina(currentCraftingHero) - energyCost);

            // 4️⃣ 逆向工程
            float perkBonus = currentCraftingHero.GetPerkValue(DefaultPerks.Crafting.CuriousSmelter)
                ? ModConfig.CuriousSmelterMultiplier
                : 1f;

            float modifierWeight = ModConfig.GetModifierWeight(
                equipmentElement.ItemModifier?.PriceMultiplier ?? 1f);

            float earnedProgress =
                ModConfig.BaseProgressUnit * perkBonus * modifierWeight;

            foreach (var usedPart in design.UsedPieces)
            {
                var piece = usedPart?.CraftingPiece;
                if (piece?.StringId == null)
                    continue;

                // 回收零件
                behavior.AddPart(piece.StringId, 1);

                bool isOpened = false;

                try
                {
                    if (_isOpenedMethod != null)
                    {
                        isOpened = (bool)_isOpenedMethod.Invoke(
                            __instance,
                            new object[] { piece, design.Template });
                    }
                }
                catch
                {
                    continue;
                }

                if (!isOpened)
                {
                    float required = piece.PieceTier * ModConfig.DifficultyFactor;
                    float current = behavior.AddProgress(piece.StringId, earnedProgress);

                    if (current >= required)
                    {
                        try
                        {
                            _openPartMethod?.Invoke(
                                __instance,
                                new object[] { piece, design.Template, false });
                        }
                        catch { }

                        InformationManager.DisplayMessage(
                            new InformationMessage(
                                $"【逆向解析完成】已掌握：{piece.Name}",
                                Color.FromUint(0xFF00FF00)));
                    }
                    else
                    {
                        int percent = (int)((current / required) * 100);

                        InformationManager.DisplayMessage(
                            new InformationMessage(
                                $"解析中：{piece.Name} [回收+1] 进度:{percent}%",
                                Color.FromUint(0xFFFFFF00)));
                    }
                }
                else
                {
                    InformationManager.DisplayMessage(
                        new InformationMessage(
                            $"配件回收：{piece.Name} [库存:{behavior.GetPartCount(piece.StringId)}]",
                            Color.FromUint(0xFFDAA520)));
                }
            }

            // 保留原版事件
            CampaignEventDispatcher.Instance
                .OnEquipmentSmeltedByHero(currentCraftingHero, equipmentElement);

            return false;
        }
    }
    
}