namespace EquipmentManager
{
    internal sealed class PostLootAutoEquipTrigger
    {
        private bool _expectingPostBattleLoot;
        private bool _pendingPostLootAutoEquip;
        private int _postLootTickDelay;

        public bool IsExpectingPostBattleLoot => _expectingPostBattleLoot;
        public bool IsPendingPostLootAutoEquip => _pendingPostLootAutoEquip;

        public void OnBattleEnded(bool playerWonBattle)
        {
            if (!playerWonBattle)
            {
                return;
            }

            _expectingPostBattleLoot = true;
            _pendingPostLootAutoEquip = false;
            _postLootTickDelay = 0;
        }

        public bool OnItemsLooted(bool isMainParty, bool containsEquipment)
        {
            if (!_expectingPostBattleLoot || !isMainParty || !containsEquipment)
            {
                return false;
            }

            _pendingPostLootAutoEquip = true;
            _postLootTickDelay = 1;
            _expectingPostBattleLoot = false;
            return true;
        }

        public bool ShouldRunOnTick()
        {
            if (!_pendingPostLootAutoEquip)
            {
                return false;
            }

            if (_postLootTickDelay > 0)
            {
                _postLootTickDelay--;
                return false;
            }

            _pendingPostLootAutoEquip = false;
            return true;
        }

        public void ClearPending()
        {
            _pendingPostLootAutoEquip = false;
        }
    }
}

