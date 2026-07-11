using System.Collections.Generic;
using BazaarGameClient.Domain.Models.Cards;

namespace BazaarAccess.Gameplay.CombatEncounterPreview;

internal sealed class CombatEncounterPreviewModel
{
    public string EnemyName { get; set; }
    public int Health { get; set; }
    public List<Card> Skills { get; } = new List<Card>();
    public List<Card> Items { get; } = new List<Card>();
}
