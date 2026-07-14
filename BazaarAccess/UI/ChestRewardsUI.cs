using System.Collections.Generic;
using System.Threading.Tasks;
using BazaarAccess.Accessibility;
using BazaarAccess.Core;
using BazaarAccess.Screens;
using BazaarGameShared;
using TheBazaar.Assets.Scripts.ScriptableObjectsScripts;
using TheBazaar.Feature.Chest.Scene;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace BazaarAccess.UI;

/// <summary>
/// Accessible UI for displaying chest rewards after opening.
/// Shows rewards as a navigable list. Enter closes and returns to chest selection.
/// </summary>
public class ChestRewardsUI : BaseUI
{
    public override string UIName => "Chest Rewards";

    private List<PlayerChestInventory.ChestRewardResponse> _rewards;
    private List<RewardInfo> _rewardInfos = new List<RewardInfo>();
    private int _currentIndex = 0;
    private bool _isLoading = true;

    private struct RewardInfo
    {
        public string ItemName;
        public string Rarity;
        public string CollectionType;
        public bool IsDuplicate;
        public int Gems;
        public int DuplicateGems;
        public int RankedVouchers;
        public int BonusChests;
        public bool HasCollectible;
    }

    public ChestRewardsUI(Transform root, List<PlayerChestInventory.ChestRewardResponse> rewards) : base(root)
    {
        _rewards = rewards ?? new List<PlayerChestInventory.ChestRewardResponse>();
        // Start loading reward names asynchronously
        _ = LoadRewardNamesAsync();
    }

    protected override void BuildMenu()
    {
        // Menu is not used - we handle navigation manually
    }

    private async Task LoadRewardNamesAsync()
    {
        _rewardInfos.Clear();

        foreach (var reward in _rewards)
        {
            var info = new RewardInfo
            {
                Rarity = GetRarityName(reward.itemRarity),
                IsDuplicate = reward.IsDuplicate,
                Gems = reward.gems,
                DuplicateGems = reward.DuplicateGems,
                RankedVouchers = reward.rankedVouchers,
                BonusChests = reward.bonusChestCount,
                HasCollectible = !string.IsNullOrEmpty(reward.collectibleItemId)
            };

            // Try to load the collectible name
            if (info.HasCollectible)
            {
                try
                {
                    var asset = await Addressables.LoadAssetAsync<CollectibleAssetDataSO>(reward.collectibleItemId).Task;
                    if (asset != null)
                    {
                        info.ItemName = asset.LocalizableName?.GetLocalizedText() ?? asset.Name ?? "Item";
                        info.CollectionType = GetCollectionTypeName(asset.collectionType);
                        Addressables.Release(asset);
                    }
                    else
                    {
                        info.ItemName = "Item";
                    }
                }
                catch
                {
                    info.ItemName = "Item";
                }
            }

            _rewardInfos.Add(info);
        }

        _isLoading = false;

        // Now announce the rewards
        AnnounceAllRewards();
    }

    private string GetRarityName(BazaarInventoryTypes.EChestRarity rarity)
    {
        return rarity switch
        {
            BazaarInventoryTypes.EChestRarity.Common => Loc.T("ui.chest.rarity.common"),
            BazaarInventoryTypes.EChestRarity.Uncommon => Loc.T("ui.chest.rarity.uncommon"),
            BazaarInventoryTypes.EChestRarity.Rare => Loc.T("ui.chest.rarity.rare"),
            BazaarInventoryTypes.EChestRarity.Epic => Loc.T("ui.chest.rarity.epic"),
            BazaarInventoryTypes.EChestRarity.Legendary => Loc.T("ui.chest.rarity.legendary"),
            _ => Loc.T("ui.chest.rarity.unknown")
        };
    }

    private string GetCollectionTypeName(BazaarInventoryTypes.ECollectionType type)
    {
        return type switch
        {
            BazaarInventoryTypes.ECollectionType.HeroSkins => Loc.T("ui.chest.collection_type.hero_skin"),
            BazaarInventoryTypes.ECollectionType.Boards => Loc.T("ui.chest.collection_type.board"),
            BazaarInventoryTypes.ECollectionType.CardSkins => Loc.T("ui.chest.collection_type.card_skin"),
            BazaarInventoryTypes.ECollectionType.Carpets => Loc.T("ui.chest.collection_type.carpet"),
            BazaarInventoryTypes.ECollectionType.CardBacks => Loc.T("ui.chest.collection_type.card_back"),
            BazaarInventoryTypes.ECollectionType.Stash => Loc.T("ui.chest.collection_type.stash"),
            BazaarInventoryTypes.ECollectionType.Bank => Loc.T("ui.chest.collection_type.bank"),
            BazaarInventoryTypes.ECollectionType.Toys => Loc.T("ui.chest.collection_type.toy"),
            BazaarInventoryTypes.ECollectionType.Album => Loc.T("ui.chest.collection_type.album"),
            _ => ""
        };
    }

    public override void OnFocus()
    {
        // If still loading, wait for LoadRewardNamesAsync to call AnnounceAllRewards
        if (_isLoading)
        {
            TolkWrapper.Speak(Loc.T("ui.chest.loading"));
            return;
        }

        AnnounceAllRewards();
    }

    private void AnnounceAllRewards()
    {
        if (_rewardInfos.Count == 0)
        {
            TolkWrapper.Speak($"{Loc.T("ui.chest.no_rewards")} {Loc.T("ui.continue_prompt")}");
            return;
        }

        // Build summary announcement
        var parts = new List<string>();

        if (_rewardInfos.Count == 1)
        {
            parts.Add(Loc.T("ui.chest.you_received"));
            parts.Add(GetRewardDescription(_rewardInfos[0]));
        }
        else
        {
            parts.Add(Loc.Plural("ui.chest.received_count", _rewardInfos.Count, _rewardInfos.Count));

            // Summarize by rarity
            var rarityCounts = new Dictionary<string, int>();
            int totalGems = 0;
            int totalVouchers = 0;
            int totalBonusChests = 0;

            foreach (var info in _rewardInfos)
            {
                if (info.HasCollectible)
                {
                    if (!rarityCounts.ContainsKey(info.Rarity))
                        rarityCounts[info.Rarity] = 0;
                    rarityCounts[info.Rarity]++;
                }
                totalGems += info.Gems + info.DuplicateGems;
                totalVouchers += info.RankedVouchers;
                totalBonusChests += info.BonusChests;
            }

            // Add rarity summary
            foreach (var kvp in rarityCounts)
            {
                parts.Add(Loc.T("ui.chest.rarity_count", kvp.Value, kvp.Key));
            }

            if (totalGems > 0)
                parts.Add(Loc.Plural("ui.chest.gems_total", totalGems, totalGems));

            if (totalVouchers > 0)
                parts.Add(Loc.Plural("ui.chest.vouchers", totalVouchers, totalVouchers));

            if (totalBonusChests > 0)
                parts.Add(Loc.Plural("ui.chest.bonus_chests", totalBonusChests, totalBonusChests));
        }

        parts.Add(Loc.T("ui.chest.browse_instructions"));

        TolkWrapper.Speak(string.Join(". ", parts));
    }

    private string GetRewardDescription(RewardInfo info)
    {
        var parts = new List<string>();

        // Collectible item
        if (info.HasCollectible)
        {
            string itemDesc;
            if (!string.IsNullOrEmpty(info.ItemName) && info.ItemName != "Item")
            {
                // Full description with name
                if (!string.IsNullOrEmpty(info.CollectionType))
                    itemDesc = Loc.T("ui.chest.reward_typed", info.Rarity, info.CollectionType, info.ItemName);
                else
                    itemDesc = Loc.T("ui.chest.reward_named", info.Rarity, info.ItemName);
            }
            else
            {
                // Fallback without name
                itemDesc = Loc.T("ui.chest.reward_item", info.Rarity);
            }

            if (info.IsDuplicate)
                itemDesc = Loc.T("ui.chest.duplicate", itemDesc);

            parts.Add(itemDesc);
        }

        // Currencies
        if (info.Gems > 0)
            parts.Add(Loc.Plural("ui.chest.gems", info.Gems, info.Gems));

        if (info.DuplicateGems > 0)
            parts.Add(Loc.Plural("ui.chest.bonus_gems", info.DuplicateGems, info.DuplicateGems));

        if (info.RankedVouchers > 0)
            parts.Add(Loc.Plural("ui.chest.vouchers", info.RankedVouchers, info.RankedVouchers));

        if (info.BonusChests > 0)
            parts.Add(Loc.Plural("ui.chest.bonus_chests", info.BonusChests, info.BonusChests));

        if (parts.Count == 0)
            return Loc.T("ui.chest.empty_reward");

        return string.Join(", ", parts);
    }

    private void ReadCurrentReward()
    {
        if (_rewardInfos.Count == 0) return;

        if (_currentIndex < 0 || _currentIndex >= _rewardInfos.Count)
            _currentIndex = 0;

        var info = _rewardInfos[_currentIndex];
        string description = GetRewardDescription(info);
        TolkWrapper.Speak(description);
    }

    private void Close()
    {
        AccessibilityMgr.PopUI();

        // Return to chest selection state
        var screen = AccessibilityMgr.GetCurrentScreen() as ChestSceneScreen;
        screen?.ReturnToSelection();
    }

    protected override void OnBack()
    {
        // Only Enter closes, not Escape - do nothing
    }

    public override void HandleInput(AccessibleKey key)
    {
        // If still loading, only allow Enter to close
        if (_isLoading)
        {
            if (key == AccessibleKey.Confirm)
                Close();
            return;
        }

        switch (key)
        {
            case AccessibleKey.Confirm:
                Close();
                break;

            case AccessibleKey.Up:
                if (_rewardInfos.Count > 0)
                {
                    if (_currentIndex > 0)
                    {
                        _currentIndex--;
                        ReadCurrentReward();
                    }
                    else
                    {
                        ReadCurrentReward(); // Re-read first item
                    }
                }
                break;

            case AccessibleKey.Down:
                if (_rewardInfos.Count > 0)
                {
                    if (_currentIndex < _rewardInfos.Count - 1)
                    {
                        _currentIndex++;
                        ReadCurrentReward();
                    }
                    else
                    {
                        ReadCurrentReward(); // Re-read last item
                    }
                }
                break;

            // Ignore all other keys
            default:
                break;
        }
    }
}
