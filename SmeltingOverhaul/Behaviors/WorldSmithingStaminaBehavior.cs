using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace SmeltingOverhaul
{
    public class WorldSmithingStaminaBehavior : CampaignBehaviorBase
    {
        private const int StaminaPerHour = 2; // 野外每小时恢复量

        public override void RegisterEvents()
        {
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void OnHourlyTick()
        {
            if (MobileParty.MainParty == null)
                return;

            // ✅ 只在野外恢复
            if (MobileParty.MainParty.CurrentSettlement != null)
                return;

            var craftingBehavior = Campaign.Current.GetCampaignBehavior<CraftingCampaignBehavior>();
            if (craftingBehavior == null)
                return;

            foreach (var troop in MobileParty.MainParty.MemberRoster.GetTroopRoster())
            {
                if (!troop.Character.IsHero || troop.Character.HeroObject == null)
                    continue;

                Hero hero = troop.Character.HeroObject;

                int current = craftingBehavior.GetHeroCraftingStamina(hero);
                int max = craftingBehavior.GetMaxHeroCraftingStamina(hero);

                if (current >= max)
                    continue;

                int newValue = current + StaminaPerHour;

                if (newValue > max)
                    newValue = max;

                craftingBehavior.SetHeroCraftingStamina(hero, newValue);
            }
        }
    }
}