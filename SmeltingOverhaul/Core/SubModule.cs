using Bannerlord.UIExtenderEx;
using HarmonyLib;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.WeaponDesign;
using TaleWorlds.Core;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace SmeltingOverhaul
{
    public class SubModule : MBSubModuleBase
    {

        private readonly UIExtender _uiExtender = new("SmeltingOverhaul");
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            _uiExtender.Register(typeof(SubModule).Assembly);
            _uiExtender.Enable();
            // Harmony 补丁
            try
            {
                var harmony = new Harmony("smelting_overhaul");
                harmony.PatchAll();
                InformationManager.DisplayMessage(new InformationMessage("[SmeltingOverhaul] Harmony PatchAll completed."));
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage("[SmeltingOverhaul] Harmony PatchAll failed: " + ex.Message));
            }

        }

        protected override void OnGameStart(Game game, IGameStarter starterObject)
        {
            base.OnGameStart(game, starterObject);

            if (game.GameType is Campaign)
            {
                if (starterObject is CampaignGameStarter campaignStarter)
                {
                    campaignStarter.AddBehavior(new SmeltingMechanicBehavior());
                    campaignStarter.AddBehavior(new WorldSmithingStaminaBehavior());
                }
            }
        }

        protected override void OnSubModuleUnloaded()
        {
            base.OnSubModuleUnloaded();
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
        }
    }
}