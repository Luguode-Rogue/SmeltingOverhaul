using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace SmeltingOverhaul
{
    /// <summary>
    /// 熔炼机制的存档数据与运行数据
    /// </summary>
    public class SmeltingMechanicBehavior : CampaignBehaviorBase
    {
        public static event Action OnDataUpdated;
        private Dictionary<string, float> _unlockProgress = new Dictionary<string, float>();
        private Dictionary<string, int> _partInventory = new Dictionary<string, int>();

        public float AddProgress(string pieceId, float amount)
        {
            if (!_unlockProgress.ContainsKey(pieceId))
                _unlockProgress[pieceId] = 0f;

            _unlockProgress[pieceId] += amount;
            return _unlockProgress[pieceId];
        }

        public void AddPart(string pieceId, int count)
        {
            if (!_partInventory.ContainsKey(pieceId))
                _partInventory[pieceId] = 0;

            _partInventory[pieceId] += count;
            OnDataUpdated?.Invoke();
        }

        public bool ConsumePart(string pieceId, int count)
        {
            if (GetPartCount(pieceId) < count)
                return false;

            _partInventory[pieceId] -= count;
            OnDataUpdated?.Invoke();
            return true;
        }

        public int GetPartCount(string pieceId)
        {
            return _partInventory.TryGetValue(pieceId, out int v) ? v : 0;
        }

        public override void RegisterEvents()
        {
            InformationManager.DisplayMessage(new InformationMessage(    "SmeltingMechanicBehavior loaded?"));
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("_unlockProgress", ref _unlockProgress);
            dataStore.SyncData("_partInventory", ref _partInventory);
        }
    }
}