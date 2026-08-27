using System;

namespace SmeltingOverhaul
{
    /// <summary>
    /// 全局配置参数
    /// </summary>
    public static class ModConfig
    {
        public static float BaseProgressUnit = 1.0f;
        public static float CuriousSmelterMultiplier = 2.0f;
        public static float DifficultyFactor = 1.0f;
        public static float PartRecycleRate { get; set; } = 0.5f; // 默认50%回收

        public static float GetModifierWeight(float priceMultiplier)
        {
            if (priceMultiplier >= 10.0f) return 5.0f;
            if (priceMultiplier >= 3.0f) return 3.0f;
            if (priceMultiplier >= 1.2f) return 1.5f;
            if (priceMultiplier >= 0.8f) return 1.0f;
            if (priceMultiplier >= 0.5f) return 0.5f;
            return 0.2f;
        }
    }
}