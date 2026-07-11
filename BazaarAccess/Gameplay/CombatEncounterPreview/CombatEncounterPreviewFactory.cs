using System;
using System.Collections.Generic;
using System.Linq;
using BazaarAccess.Gameplay.CardReading;
using BazaarGameClient.Domain.Models.Cards;
using BazaarGameShared.Domain.Cards;
using BazaarGameShared.Domain.Cards.Item;
using BazaarGameShared.Domain.Cards.Skill;
using BazaarGameShared.Domain.Core.Types;
using BazaarGameShared.Domain.Players;
using TheBazaar;

namespace BazaarAccess.Gameplay.CombatEncounterPreview;

internal static class CombatEncounterPreviewFactory
{
    public static CombatEncounterPreviewModel Create(Card card)
    {
        if (card is not CombatEncounterCard combatEncounter)
            return null;

        try
        {
            TMonster monster = combatEncounter.GetMonsterTemplate();
            if (monster?.Player == null)
                return null;

            var model = new CombatEncounterPreviewModel
            {
                EnemyName = GetEnemyName(card, monster),
                Health = GetPreviewHealth(monster)
            };

            if (monster.Player.Skills != null)
            {
                foreach (var skill in monster.Player.Skills)
                {
                    var previewSkill = PreviewCardFactory.Create(skill, ECardType.Skill);
                    if (previewSkill != null)
                        model.Skills.Add(previewSkill);
                }
            }

            if (monster.Player.Hand?.Items != null)
            {
                IEnumerable<TCardInstanceItem> orderedItems = monster.Player.Hand.Items
                    .OrderBy(item => item?.SocketId.HasValue == true ? (int)item.SocketId.Value : int.MaxValue)
                    .ThenBy(item => item?.InstanceId ?? string.Empty);

                foreach (var item in orderedItems)
                {
                    var previewItem = PreviewCardFactory.Create(item, ECardType.Item);
                    if (previewItem != null)
                        model.Items.Add(previewItem);
                }
            }

            return model;
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"CombatEncounterPreviewFactory.Create error: {ex.Message}");
            return null;
        }
    }

    private static string GetEnemyName(Card encounterCard, TMonster monster)
    {
        string encounterName = ItemReader.GetCardName(encounterCard);
        if (!string.IsNullOrWhiteSpace(encounterName))
            return encounterName;

        if (!string.IsNullOrWhiteSpace(monster.InternalName))
            return monster.InternalName;

        return "Enemy";
    }

    private static int GetPreviewHealth(TMonster monster)
    {
        if (monster.Player.Attributes.TryGetValue(EPlayerAttributeType.HealthMax, out int healthMax) && healthMax > 0)
            return healthMax;

        if (monster.Player.Attributes.TryGetValue(EPlayerAttributeType.Health, out int health))
            return health;

        return 0;
    }

    private static class PreviewCardFactory
    {
        public static Card Create(TCardInstance instance, ECardType type)
        {
            if (instance == null) return null;

            try
            {
                var staticData = Data.GetStatic();
                var template = staticData.GetCardById(instance.TemplateId);
                if (template == null)
                    return null;

                Card previewCard = DTOUtils.CreateCard(instance.InstanceId, instance.TemplateId.ToString(), type);
                previewCard.Template = template;
                previewCard.Type = type;
                previewCard.Tier = instance.Tier;
                previewCard.Size = template.Size;
                previewCard.Tags = template.Tags != null
                    ? new HashSet<ECardTag>(template.Tags)
                    : new HashSet<ECardTag>();
                previewCard.Attributes = instance.Attributes != null
                    ? new Dictionary<ECardAttributeType, int>(instance.Attributes)
                    : new Dictionary<ECardAttributeType, int>();

                Dictionary<ECardAttributeType, int> previewAttributes =
                    TheBazaar.CardExtensions.BuildAttributeDictionaryForTier(previewCard, template, instance.Tier);

                if (previewCard is ItemCard previewItem &&
                    template is TCardItem itemTemplate &&
                    instance is TCardInstanceItem itemInstance)
                {
                    TheBazaar.CardExtensions.ApplyPreviewEnchantment(itemTemplate, previewItem, previewAttributes, itemInstance.EnchantmentType);
                }

                previewCard.Attributes = previewAttributes;
                return previewCard;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"PreviewCardFactory.Create error: {ex.Message}");
                return null;
            }
        }
    }
}
