using HarmonyLib;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.Refinement;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;

namespace SmeltingOverhaul
{
    /// <summary>
    /// 批量精炼补丁 - 拦截主VM的执行方法
    /// Shift: 执行5次 | Ctrl: 执行直到资源不足
    /// </summary>
    [HarmonyPatch(typeof(RefinementVM), "ExecuteSelectedRefinement")]
    public class RefinementBatchExecutePatch
    {
        private static bool _isBatchProcessing = false;

        static bool Prefix(RefinementVM __instance, Hero currentCraftingHero)
        {
            // 防止递归
            if (_isBatchProcessing)
                return true;

            // 检测批量操作按键
            if (Input.IsKeyDown(InputKey.LeftShift) || Input.IsKeyDown(InputKey.LeftControl))
            {
                _isBatchProcessing = true;

                try
                {
                    int maxCount = Input.IsKeyDown(InputKey.LeftShift) ? 5 : int.MaxValue;
                    ExecuteBatchRefinement(__instance, currentCraftingHero, maxCount);
                }
                finally
                {
                    _isBatchProcessing = false;
                }

                return false; // 阻止原方法执行
            }

            return true; // 正常执行原方法
        }

        private static void ExecuteBatchRefinement(RefinementVM vm, Hero hero, int maxCount)
        {
            // 获取私有字段
            var craftingBehaviorField = typeof(RefinementVM).GetField("_craftingBehavior",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var currentActionProperty = typeof(RefinementVM).GetProperty("CurrentSelectedAction",
                BindingFlags.Public | BindingFlags.Instance);

            if (craftingBehaviorField == null || currentActionProperty == null)
                return;

            var craftingBehavior = craftingBehaviorField.GetValue(vm) as ICraftingCampaignBehavior;
            var currentAction = currentActionProperty.GetValue(vm) as RefinementActionItemVM;

            if (craftingBehavior == null || currentAction == null || !currentAction.IsEnabled)
                return;

            var refineFormula = currentAction.RefineFormula;
            int executedCount = 0;

            // 批量执行循环
            while (executedCount < maxCount)
            {
                // 每次执行前检查资源
                if (!HasSufficientResources(hero, refineFormula))
                    break;

                // 执行单次精炼
                craftingBehavior.DoRefinement(hero, refineFormula);
                executedCount++;

                // 刷新VM状态
                var refreshMethod = typeof(RefinementVM).GetMethod("RefreshRefinementActionsList",
                    BindingFlags.Public | BindingFlags.Instance);
                refreshMethod?.Invoke(vm, new object[] { hero });

                // 重新检查当前选择是否仍可用
                currentAction = currentActionProperty.GetValue(vm) as RefinementActionItemVM;
                if (currentAction == null || !currentAction.IsEnabled)
                    break;
            }

            InformationManager.DisplayMessage(new InformationMessage($"精炼完成 {executedCount} 次"));
        }

        private static bool HasSufficientResources(Hero hero, object refineFormula)
        {
            try
            {
                var itemRoster = MobileParty.MainParty.ItemRoster;
                var smithingModel = Campaign.Current.Models.SmithingModel;

                // 反射获取配方材料信息
                var input1Field = refineFormula.GetType().GetField("Input1", BindingFlags.Public | BindingFlags.Instance);
                var input1CountField = refineFormula.GetType().GetField("Input1Count", BindingFlags.Public | BindingFlags.Instance);
                var input2Field = refineFormula.GetType().GetField("Input2", BindingFlags.Public | BindingFlags.Instance);
                var input2CountField = refineFormula.GetType().GetField("Input2Count", BindingFlags.Public | BindingFlags.Instance);

                // 检查输入材料1
                if (input1Field != null && input1CountField != null)
                {
                    var input1 = input1Field.GetValue(refineFormula);
                    var input1Count = (int)input1CountField.GetValue(refineFormula);

                    if (input1Count > 0)
                    {
                        var getItemMethod = smithingModel.GetType().GetMethod("GetCraftingMaterialItem",
                            BindingFlags.Public | BindingFlags.Instance);
                        var materialItem = getItemMethod?.Invoke(smithingModel, new object[] { input1 }) as ItemObject;

                        if (materialItem != null)
                        {
                            var currentCount = itemRoster.GetItemNumber(materialItem);
                            if (currentCount < input1Count)
                                return false;
                        }
                    }
                }

                // 检查输入材料2
                if (input2Field != null && input2CountField != null)
                {
                    var input2 = input2Field.GetValue(refineFormula);
                    var input2Count = (int)input2CountField.GetValue(refineFormula);

                    if (input2Count > 0)
                    {
                        var getItemMethod = smithingModel.GetType().GetMethod("GetCraftingMaterialItem",
                            BindingFlags.Public | BindingFlags.Instance);
                        var materialItem = getItemMethod?.Invoke(smithingModel, new object[] { input2 }) as ItemObject;

                        if (materialItem != null)
                        {
                            var currentCount = itemRoster.GetItemNumber(materialItem);
                            if (currentCount < input2Count)
                                return false;
                        }
                    }
                }

                // 检查体力
                var behavior = Campaign.Current.GetCampaignBehavior<ICraftingCampaignBehavior>();
                var getStaminaMethod = behavior.GetType().GetMethod("GetHeroCraftingStamina",
                    BindingFlags.Public | BindingFlags.Instance);
                var getCostMethod = smithingModel.GetType().GetMethod("GetEnergyCostForRefining",
                    BindingFlags.Public | BindingFlags.Instance);

                if (getStaminaMethod != null && getCostMethod != null)
                {
                    var currentStamina = (int)getStaminaMethod.Invoke(behavior, new object[] { hero });
                    var energyCost = (int)getCostMethod.Invoke(smithingModel, new object[] { refineFormula, hero });

                    if (currentStamina < energyCost)
                        return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"资源检查错误: {ex.Message}");
                return false;
            }
        }
    }
}