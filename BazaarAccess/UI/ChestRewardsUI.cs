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
                BonusChests = reward.bonusChest?.Length ?? 0,
                HasCollectible = reward.collectibleItem != null && !string.IsNullOrEmpty(reward.collectibleItem.itemId)
            };

            // Try to load the collectible name
            if (info.HasCollectible)
            {
                try
                {
                    var asset = await Addressables.LoadAssetAsync<CollectibleAssetDataSO>(reward.collectibleItem.itemId).Task;
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
            BazaarInventoryTypes.EChestRarity.Common => "Common",
            BazaarInventoryTypes.EChestRarity.Uncommon => "Uncommon",
            BazaarInventoryTypes.EChestRarity.Rare => "Rare",
            BazaarInventoryTypes.EChestRarity.Epic => "Epic",
            BazaarInventoryTypes.EChestRarity.Legendary => "Legendary",
            _ => "Unknown"
        };
    }

    private string GetCollectionTypeName(BazaarInventoryTypes.ECollectionType type)
    {
        return type switch
        {
            BazaarInventoryTypes.ECollectionType.HeroSkins => "Hero Skin",
            BazaarInventoryTypes.ECollectionType.Boards => "Board",
            BazaarInventoryTypes.ECollectionType.CardSkins => "Card Skin",
            BazaarInventoryTypes.ECollectionType.Carpets => "Carpet",
            BazaarInventoryTypes.ECollectionType.CardBacks => "Card Back",
            BazaarInventoryTypes.ECollectionType.Stash => "Stash",
            BazaarInventoryTypes.ECollectionType.Bank => "Bank",
            BazaarInventoryTypes.ECollectionType.Toys => "Toy",
            BazaarInventoryTypes.ECollectionType.Album => "Album",
            _ => ""
        };
    }

    public override void OnFocus()
    {
        // If still loading, wait for LoadRewardNamesAsync to call AnnounceAllRewards
        if (_isLoading)
        {
            TolkWrapper.Speak("Loading rewards...");
            return;
        }

        AnnounceAllRewards();
    }

    private void AnnounceAllRewards()
    {
        if (_rewardInfos.Count == 0)
        {
            TolkWrapper.Speak("No rewards. Press Enter to continue.");
            return;
        }

        // Build summary announcement
        var parts = new List<string>();

        if (_rewardInfos.Count == 1)
        {
            parts.Add("You received");
            parts.Add(GetRewardDescription(_rewardInfos[0]));
        }
        else
        {
            parts.Add($"You received {_rewardInfos.Count} rewards");

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
                parts.Add($"{kvp.Value} {kvp.Key}");
            }

            if (totalGems > 0)
                parts.Add($"{totalGems} gems total");

            if (totalVouchers > 0)
                parts.Add($"{totalVouchers} ranked vouchers");

            if (totalBonusChests > 0)
                parts.Add($"{totalBonusChests} bonus chests");
        }

        parts.Add("Use arrows to browse, Enter to continue");

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
                    itemDesc = $"{info.Rarity} {info.CollectionType}: {info.ItemName}";
                else
                    itemDesc = $"{info.Rarity}: {info.ItemName}";
            }
            else
            {
                // Fallback without name
                itemDesc = $"{info.Rarity} item";
            }

            if (info.IsDuplicate)
                itemDesc += " (duplicate)";

            parts.Add(itemDesc);
        }

        // Currencies
        if (info.Gems > 0)
            parts.Add($"{info.Gems} gems");

        if (info.DuplicateGems > 0)
            parts.Add($"{info.DuplicateGems} bonus gems");

        if (info.RankedVouchers > 0)
            parts.Add($"{info.RankedVouchers} ranked vouchers");

        if (info.BonusChests > 0)
            parts.Add($"{info.BonusChests} bonus chest{(info.BonusChests > 1 ? "s" : "")}");

        if (parts.Count == 0)
            return "Empty reward";

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
