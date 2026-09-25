using System;

namespace Ludo.Core
{
    /// <summary>How a dice design is earned.</summary>
    public enum DiceUnlock
    {
        Free = 0,      // everybody has it from the start
        Level = 1,     // reach a player level
        Rank = 2,      // reach a rank (Rank Points)
        Coins = 3      // buy it with coins earned by playing
    }

    public enum DiceRarity { Common = 0, Rare = 1, Epic = 2 }

    /// <summary>One collectable dice design. Purely a picture: see DiceSkins for why that matters.</summary>
    public readonly struct DiceSkin
    {
        public readonly string Id;          // also the texture name: dice_faces_<id>.png ("classic" = dice_faces.png)
        public readonly string Name;
        public readonly DiceRarity Rarity;
        public readonly DiceUnlock Unlock;
        public readonly int Requirement;    // level, Rank Points or price in coins, depending on Unlock

        public DiceSkin(string id, string name, DiceRarity rarity, DiceUnlock unlock, int requirement)
        {
            Id = id; Name = name; Rarity = rarity; Unlock = unlock; Requirement = requirement;
        }

        public bool IsBuyable => Unlock == DiceUnlock.Coins;

        /// <summary>One line under the name in the collection ("Level 5", "1,000 coins", ...).</summary>
        public string RequirementText()
        {
            switch (Unlock)
            {
                case DiceUnlock.Level: return "Level " + Requirement;
                case DiceUnlock.Rank: return Rating.Tier(Requirement);
                case DiceUnlock.Coins: return Requirement.ToString("N0") + " coins";
                default: return "Always yours";
            }
        }
    }

    /// <summary>
    /// The dice collection: a set of designs a player earns by playing and can equip. A skin repaints the faces and nothing
    /// else - the model, the physics throw and the number the engine rolled are identical for every skin, so no design can
    /// ever be luckier than another. Nothing here can be bought with real money.
    /// </summary>
    public static class DiceSkins
    {
        public const string Default = "classic";

        static DiceSkin[] all;

        /// <summary>
        /// Built on first use, not in a field initialiser: the Ruby design asks for a rank, and the rank numbers live in the
        /// configurable RewardSettings, which may not be loaded yet while this class is being initialised.
        /// </summary>
        public static DiceSkin[] All => all ??= new[]
        {
            new DiceSkin(Default,   "Classic",  DiceRarity.Common, DiceUnlock.Free,  0),
            new DiceSkin("midnight","Midnight", DiceRarity.Common, DiceUnlock.Level, 3),
            new DiceSkin("emerald", "Emerald",  DiceRarity.Rare,   DiceUnlock.Coins, 500),
            new DiceSkin("ruby",    "Ruby",     DiceRarity.Rare,   DiceUnlock.Rank,  Rating.Start + 500),
            new DiceSkin("ice",     "Ice",      DiceRarity.Epic,   DiceUnlock.Coins, 2000),
            new DiceSkin("gold",    "Gold",     DiceRarity.Epic,   DiceUnlock.Level, 10),
            new DiceSkin("rainbow", "Rainbow",  DiceRarity.Rare,   DiceUnlock.Coins, 800),      // every number in its own colour
            new DiceSkin("carnival","Carnival", DiceRarity.Epic,   DiceUnlock.Coins, 1500)
        };

        /// <summary>Tests only: forget the built list so changed reward settings are picked up.</summary>
        public static void Reset() => all = null;

        public static int IndexOf(string id)
        {
            for (int i = 0; i < All.Length; i++) if (All[i].Id == id) return i;
            return -1;
        }

        public static bool Exists(string id) => IndexOf(id) >= 0;

        public static DiceSkin Get(string id)
        {
            int i = IndexOf(id);
            return All[i < 0 ? 0 : i];
        }

        /// <summary>
        /// Does this player have the skin? Level and rank designs open by themselves as the player grows; a coins design has
        /// to be bought, and 'owned' is the list of ids already paid for (comma separated, as it is saved).
        /// </summary>
        public static bool IsOwned(string id, int level, int rankPoints, string owned)
        {
            int i = IndexOf(id);
            if (i < 0) return false;
            var skin = All[i];
            switch (skin.Unlock)
            {
                case DiceUnlock.Free: return true;
                case DiceUnlock.Level: return level >= skin.Requirement;
                case DiceUnlock.Rank: return rankPoints >= skin.Requirement;
                default: return Contains(owned, id);
            }
        }

        /// <summary>The skin to actually roll with: the chosen one if the player still has it, otherwise the default.</summary>
        public static string Equipped(string chosen, int level, int rankPoints, string owned) =>
            IsOwned(chosen, level, rankPoints, owned) ? chosen : Default;

        public static bool Contains(string owned, string id)
        {
            if (string.IsNullOrEmpty(owned) || string.IsNullOrEmpty(id)) return false;
            foreach (var part in owned.Split(','))
                if (part.Trim() == id) return true;
            return false;
        }

        /// <summary>Adds an id to the saved list (never twice). Returns the new list.</summary>
        public static string Add(string owned, string id)
        {
            if (!Exists(id) || Contains(owned, id)) return owned ?? "";
            return string.IsNullOrEmpty(owned) ? id : owned + "," + id;
        }

        /// <summary>
        /// Can this player buy the skin right now? A design that is not for sale, one already owned and one they cannot
        /// afford all answer no - the price itself is returned so the caller can take exactly that many coins.
        /// </summary>
        public static bool CanBuy(string id, int coins, int level, int rankPoints, string owned, out int price)
        {
            price = 0;
            int i = IndexOf(id);
            if (i < 0) return false;
            var skin = All[i];
            if (!skin.IsBuyable || IsOwned(id, level, rankPoints, owned)) return false;
            price = skin.Requirement;
            return coins >= price;
        }
    }
}
