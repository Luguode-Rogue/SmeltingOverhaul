using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CraftingSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.Refinement;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace SmeltingOverhaul.Patches
{
    [HarmonyPatch(typeof(CraftingCampaignBehavior), "AddItemToHistory")]
    public class CraftingHistoryEnhancedPatch
    {
        // 配置常量
        private const int MAX_HISTORY_SIZE = 30; // 1. 修改上限为 30

        // 缓存反射字段
        private static FieldInfo _historyListField = null;

        [HarmonyPrefix]
        public static bool Prefix(ItemObject craftedObject, CraftingCampaignBehavior __instance)
        {
            // --- 基础检查 ---
            if (craftedObject == null || !craftedObject.IsCraftedWeapon || craftedObject.WeaponDesign == null)
            {
                return true; // 非锻造物品，交给原版处理（虽然原版可能也不处理）
            }

            // --- 获取历史记录列表 ---
            if (_historyListField == null)
            {
                _historyListField = typeof(CraftingCampaignBehavior).GetField("_cratingItemsHistory", BindingFlags.Instance | BindingFlags.NonPublic);
            }

            if (_historyListField == null)
            {
                /* 此代码看不到log：Debug.Print 不会写入可查看的日志文件，已禁用。 */;
                return true;
            }

            List<ItemObject> historyList = _historyListField.GetValue(__instance) as List<ItemObject>;
            if (historyList == null)
            {
                // 如果列表还没初始化，可能需要调用原方法去初始化，或者手动初始化
                // 这里假设原方法会处理初始化，如果为空则让原方法运行一次
                return true;
            }

            WeaponDesign newDesign = craftedObject.WeaponDesign;

            // --- 1. 查重逻辑 (复用之前的优化) ---
            foreach (ItemObject historyItem in historyList)
            {
                if (historyItem != null && historyItem.IsCraftedWeapon && historyItem.WeaponDesign != null)
                {
                    if (AreDesignsIdentical(newDesign, historyItem.WeaponDesign))
                    {
                        /* 此代码看不到log：Debug.Print 不会写入可查看的日志文件，已禁用。 */;
                        return false; // 阻止原版执行
                    }
                }
            }

            // --- 2. 添加逻辑 ---

            // 情况 A: 列表未满，直接添加
            if (historyList.Count < MAX_HISTORY_SIZE)
            {
                historyList.Add(craftedObject);
                /* 此代码看不到log：Debug.Print 不会写入可查看的日志文件，已禁用。 */;
                return false; // 阻止原版执行，因为我们已经手动添加了
            }

            // 情况 B: 列表已满 (30个)，需要移除一个旧的才能添加新的
            if (HandleFullHistory(historyList, craftedObject))
            {
                /* 此代码看不到log：Debug.Print 不会写入可查看的日志文件，已禁用。 */;
                return false; // 阻止原版执行
            }
            else
            {
                /* 此代码看不到log：Debug.Print 不会写入可查看的日志文件，已禁用。 */;
                return false; // 阻止原版执行，且不添加
            }
        }

        /// <summary>
        /// 处理历史记录满员的情况
        /// 返回 true 表示成功替换并添加，false 表示无法安全替换
        /// </summary>
        private static bool HandleFullHistory(List<ItemObject> historyList, ItemObject newItem)
        {
            WeaponClass newWeaponClass = GetWeaponClass(newItem);
            if (newWeaponClass == WeaponClass.Undefined)
            {
                // 如果新物品没有有效的武器分类，可能不需要特殊保护，直接移除最旧的？
                // 这里为了安全，如果没有分类，我们默认移除列表第一个（最旧）
                historyList.RemoveAt(0);
                historyList.Add(newItem);
                return true;
            }

            // 策略：从最旧的（索引0）开始遍历，寻找第一个可以安全移除的记录
            for (int i = 0; i < historyList.Count; i++)
            {
                ItemObject oldItem = historyList[i];
                if (oldItem == null || !oldItem.IsCraftedWeapon) continue;

                WeaponClass oldWeaponClass = GetWeaponClass(oldItem);

                // 如果新旧武器类型相同，移除旧的肯定没问题（因为新的马上会补上这个类型）
                if (oldWeaponClass == newWeaponClass)
                {
                    historyList.RemoveAt(i);
                    historyList.Add(newItem);
                    /* 此代码看不到log：Debug.Print 不会写入可查看的日志文件，已禁用。 */;
                    return true;
                }

                // 如果类型不同，需要检查：移除这个旧记录后，列表中是否还有其他该类型的记录？
                if (!IsWeaponTypeUniqueInList(historyList, i, oldWeaponClass))
                {
                    // 如果不唯一（即移除后还有别的），则可以安全移除
                    historyList.RemoveAt(i);
                    historyList.Add(newItem);
                    /* 此代码看不到log：Debug.Print 不会写入可查看的日志文件，已禁用。 */;
                    return true;
                }

                // 如果唯一，则不能移除这个，继续尝试下一个
            }

            // 如果遍历完所有记录，发现每一个记录都是其对应武器类型的“独苗”
            // 那么为了遵守规则，我们不能移除任何一个，因此无法添加新记录
            return false;
        }

        /// <summary>
        /// 检查在移除 index 位置的物品后，列表中是否还存在 weaponClass 类型的物品
        /// 返回 true 表示它是唯一的 (移除后会消失)，返回 false 表示不唯一 (移除后还有)
        /// </summary>
        private static bool IsWeaponTypeUniqueInList(List<ItemObject> list, int indexToIgnore, WeaponClass targetClass)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (i == indexToIgnore) continue; // 跳过即将被移除的那个

                ItemObject item = list[i];
                if (item != null && item.IsCraftedWeapon)
                {
                    if (GetWeaponClass(item) == targetClass)
                    {
                        return false; // 发现了另一个同类型的，说明不是唯一的
                    }
                }
            }
            return true; // 遍历完都没发现其他的，说明它是唯一的
        }

        /// <summary>
        /// 辅助方法：从 ItemObject 获取 WeaponClass
        /// </summary>
        private static WeaponClass GetWeaponClass(ItemObject item)
        {
            if (item == null || item.PrimaryWeapon == null)
            {
                return WeaponClass.Undefined;
            }
            return item.PrimaryWeapon.WeaponClass;
        }

        /// <summary>
        /// 比对两个 WeaponDesign 是否完全一致 (复用之前的逻辑)
        /// </summary>
        private static bool AreDesignsIdentical(WeaponDesign designA, WeaponDesign designB)
        {
            if (designA == null || designB == null) return false;

            WeaponDesignElement[] piecesA = designA.UsedPieces;
            WeaponDesignElement[] piecesB = designB.UsedPieces;

            if (piecesA.Length != piecesB.Length) return false;

            for (int i = 0; i < piecesA.Length; i++)
            {
                WeaponDesignElement elemA = piecesA[i];
                WeaponDesignElement elemB = piecesB[i];

                if ((elemA == null && elemB != null) || (elemA != null && elemB == null)) return false;
                if (elemA == null && elemB == null) continue;

                if (elemA.CraftingPiece == null || elemB.CraftingPiece == null) return false;
                if (elemA.CraftingPiece.StringId != elemB.CraftingPiece.StringId) return false;
                if (elemA.ScaleFactor != elemB.ScaleFactor) return false;
            }

            return true;
        }
    }
    [HarmonyPatch(typeof(CraftingCampaignBehavior), "CreateCraftedWeaponInCraftingOrderMode")]
    public class Patch_CreateCraftedWeaponInCraftingOrderMode
    {
        [HarmonyPostfix]
        public static void Postfix(CraftingCampaignBehavior __instance, Hero crafterHero, CraftingOrder craftingOrder, WeaponDesign weaponDesign)
        {
        }
    }

    [HarmonyPatch(typeof(CraftingCampaignBehavior), "GetWeaponPieces")]
    public class CraftingHistoryPriorityPatch
    {
        // 前置补丁 - 在原始方法执行前运行
        [HarmonyPrefix]
        public static bool Prefix(CraftingTemplate craftingTemplate, int pieceTier,
            ref WeaponDesignElement[] __result, CraftingCampaignBehavior __instance)
        {
            try
            {
                // 获取锻造历史记录
                var craftingHistory = GetCraftingHistory(__instance);

                if (craftingHistory != null && craftingHistory.Count > 0)
                {
                    // 查找与当前模板匹配的历史记录
                    WeaponDesign lastMatchingDesign = null;

                    foreach (var itemObject in craftingHistory)
                    {
                        if (itemObject != null &&
                            itemObject.WeaponDesign != null &&
                            itemObject.WeaponDesign.Template == craftingTemplate)
                        {
                            lastMatchingDesign = itemObject.WeaponDesign;
                        }
                    }

                    // 如果找到匹配的历史设计，使用其零件组合
                    if (lastMatchingDesign != null && lastMatchingDesign.UsedPieces != null)
                    {
                        // 验证历史零件是否仍然可用（未隐藏且已解锁）
                        if (ArePiecesValid(lastMatchingDesign.UsedPieces, craftingTemplate, __instance))
                        {
                            __result = CloneWeaponDesignElements(lastMatchingDesign.UsedPieces);
                            return false; // 跳过原始方法
                        }
                    }
                }

                // 没有历史记录或历史记录无效，继续执行原始方法
                return true;
            }
            catch (Exception ex)
            {
                // 发生错误时不干扰原始逻辑
                Console.WriteLine($"[CraftingHistoryPriority] Error in prefix: {ex.Message}");
                return true;
            }
        }

        /// <summary>
        /// 获取锻造历史记录列表
        /// </summary>
        private static List<ItemObject> GetCraftingHistory(CraftingCampaignBehavior behavior)
        {
            var field = typeof(CraftingCampaignBehavior).GetField("_cratingItemsHistory",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (field != null)
            {
                return field.GetValue(behavior) as List<ItemObject>;
            }

            return null;
        }

        /// <summary>
        /// 验证零件是否仍然可用
        /// </summary>
        private static bool ArePiecesValid(WeaponDesignElement[] pieces,
            CraftingTemplate template, CraftingCampaignBehavior behavior)
        {
            if (pieces == null || pieces.Length != 4)
            {
                return false;
            }

            foreach (var piece in pieces)
            {
                if (piece == null)
                {
                    continue; // 允许空零件位置
                }

                var craftingPiece = piece.CraftingPiece;
                if (craftingPiece == null)
                {
                    return false;
                }

                // 检查零件是否被隐藏
                if (craftingPiece.IsHiddenOnDesigner)
                {
                    return false;
                }

                // 检查零件是否已解锁
                if (!craftingPiece.IsGivenByDefault)
                {
                    var isOpened = behavior.IsOpened(craftingPiece, template);
                    if (!isOpened)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// 克隆武器设计元素数组
        /// </summary>
        private static WeaponDesignElement[] CloneWeaponDesignElements(WeaponDesignElement[] source)
        {
            if (source == null)
            {
                return new WeaponDesignElement[4];
            }

            var result = new WeaponDesignElement[4];
            for (int i = 0; i < source.Length && i < 4; i++)
            {
                if (source[i] != null)
                {
                    result[i] = source[i].GetCopy();
                }
            }

            return result;
        }
    }
}
