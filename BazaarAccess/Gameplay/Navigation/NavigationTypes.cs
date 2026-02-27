using BazaarGameClient.Domain.Models.Cards;

namespace BazaarAccess.Gameplay;

/// <summary>
/// Current navigation section.
/// </summary>
public enum NavigationSection
{
    Selection,  // What the game offers (encounters, shop, rewards) + actions
    Board,      // Your equipped items
    Stash,      // Your storage
    Skills,     // Your skills
    Hero        // Hero stats
}

/// <summary>
/// Subsections within Hero.
/// </summary>
public enum HeroSubsection
{
    Stats,      // Hero stats (health, gold, level, etc.)
    Skills      // Equipped hero skills
}

/// <summary>
/// Subsections within enemy mode.
/// </summary>
public enum EnemySubsection
{
    Items,      // Enemy items
    Skills      // Enemy skills
}

/// <summary>
/// Sections in recap mode (post-combat with E key).
/// </summary>
public enum RecapSection
{
    None,           // Not in any recap section
    HeroStats,      // Own hero stats (V)
    HeroSkills,     // Own hero skills (V + Right)
    EnemyStats,     // Enemy hero stats (F)
    EnemySkills,    // Enemy hero skills (F + Right)
    EnemyBoard,     // Enemy board (G)
    PlayerBoard,    // Own board (B)
    CombatStats     // Per-card combat stats (H)
}

/// <summary>
/// Types of navigable items (cards or actions).
/// </summary>
public enum NavItemType
{
    Card,       // A normal card
    Exit,       // Exit action
    Reroll      // Refresh action
}

/// <summary>
/// Navigable item (can be card or action).
/// </summary>
public class NavItem
{
    public NavItemType Type { get; set; }
    public Card Card { get; set; }
    public int RerollCost { get; set; }

    public static NavItem FromCard(Card card) => new NavItem { Type = NavItemType.Card, Card = card };
    public static NavItem CreateExit() => new NavItem { Type = NavItemType.Exit };
    public static NavItem CreateReroll(int cost) => new NavItem { Type = NavItemType.Reroll, RerollCost = cost };
}
