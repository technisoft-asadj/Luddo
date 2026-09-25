using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Ludo.Core;
using Ludo.Game;

namespace Ludo.Online
{
    /// <summary>
    /// The dice collection pop-up: every design in Ludo.Core.DiceSkins as a tile showing its real face picture, what it
    /// takes to get it, and whether it is worn, owned or still locked. Tapping an owned design wears it; tapping one that
    /// is for sale buys it with coins the player earned.
    ///
    /// A design is nothing but a repaint. The model, the physics throw and the number the engine rolled are identical for
    /// every one of them, so no design is luckier than another, and none of them can be bought with real money.
    /// </summary>
    public sealed class DiceCollectionPanel : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] RectTransform grid;
        [SerializeField] Image[] tiles;              // one per design, in DiceSkins.All order
        [SerializeField] RawImage[] previews;        // the design's own "5" face, cut out of its atlas
        [SerializeField] TMP_Text[] names;
        [SerializeField] TMP_Text[] notes;           // "Worn" / "Level 3" / "500 coins"
        [SerializeField] GameObject[] locks;
        [SerializeField] Image[] tabFaces;           // All, Owned, Locked
        [SerializeField] TMP_Text coinsText;
        [SerializeField] TMP_Text statusText;
        [SerializeField] Color wornColor = new Color(1f, 0.82f, 0.15f);
        [SerializeField] Color ownedColor = new Color(0.72f, 0.92f, 0.74f);
        [SerializeField] Color lockedColor = new Color(0.86f, 0.89f, 0.95f);

        static readonly Color Good = new Color(0.12f, 0.52f, 0.24f);
        static readonly Color Refused = new Color(0.78f, 0.38f, 0.06f);

        bool busy;
        int filter;                                  // 0 all, 1 owned, 2 locked

        /// <summary>The All / Owned / Locked tabs (wired with 0, 1, 2).</summary>
        public void SetFilter(int index)
        {
            filter = Mathf.Clamp(index, 0, 2);
            Refresh();
        }

        /// <summary>The status line: green when something happened, amber when the tap was refused.</summary>
        void Say(string text, bool good)
        {
            statusText.text = text;
            statusText.color = good ? Good : Refused;
        }

        public void Open()
        {
            panel.SetActive(true);
            filter = 0;
            Say("", true);
            busy = false;
            Refresh();
        }

        public void Close() => panel.SetActive(false);

        void OnEnable() => StatsService.Changed += Refresh;

        void OnDisable() => StatsService.Changed -= Refresh;

        void Refresh()
        {
            var all = DiceSkins.All;
            var s = StatsService.Mine;
            bool loaded = StatsService.Loaded;
            string worn = loaded ? s.EquippedDice : GameSettings.DiceSkin;
            coinsText.text = loaded ? s.coins.ToString("N0") : "-";

            for (int i = 0; tabFaces != null && i < tabFaces.Length; i++)
                tabFaces[i].color = i == filter ? wornColor : new Color(0.90f, 0.94f, 1f);
            int slot = 0;
            for (int i = 0; i < tiles.Length; i++)
            {
                bool used = i < all.Length;
                if (!used) { tiles[i].gameObject.SetActive(false); continue; }
                var skin = all[i];
                bool owned = loaded && DiceSkins.IsOwned(skin.Id, s.Level, s.rating, s.diceOwned);
                bool visible = filter == 0 || (filter == 1 && owned) || (filter == 2 && !owned);
                tiles[i].gameObject.SetActive(visible);
                if (!visible) continue;
                tiles[i].rectTransform.anchoredPosition = new Vector2((slot % 3 - 1) * 296f, -360f - (slot / 3) * 330f);
                slot++;
                bool isWorn = owned && skin.Id == worn;

                names[i].text = skin.Name;
                tiles[i].color = isWorn ? wornColor : owned ? ownedColor : lockedColor;
                locks[i].SetActive(!owned);
                notes[i].text = isWorn ? "Worn" : owned ? "Tap to wear" : skin.RequirementText();
                ShowFace(previews[i], skin.Id);
            }
            if (!loaded) Say("Connecting...", false);
            else if (statusText.text.StartsWith("Connecting")) statusText.text = "";     // it connected while the list was open
        }

        /// <summary>The design's own "5" face (atlas cell 4: column 0 of the lower row) as its picture in the list.</summary>
        static void ShowFace(RawImage image, string id)
        {
            var icon = DiceSkinLibrary.Icon(id);
            var faces = icon != null ? icon : DiceSkinLibrary.Faces(id);
            image.texture = faces;
            image.uvRect = icon != null ? new Rect(0f, 0f, 1f, 1f) : new Rect(0f, 0f, 0.25f, 0.5f);
            image.enabled = faces != null;
        }

        /// <summary>A tile was tapped: wear it if it is already theirs, buy it if it is for sale, explain it otherwise.</summary>
        public async void Choose(int index)
        {
            var all = DiceSkins.All;
            if (busy || index < 0 || index >= all.Length) return;
            if (!StatsService.Loaded) { Say("Connecting - try again in a moment.", false); return; }
            var skin = all[index];
            var s = StatsService.Mine;

            if (DiceSkins.IsOwned(skin.Id, s.Level, s.rating, s.diceOwned))
            {
                if (skin.Id == s.EquippedDice) return;
                busy = true;
                bool ok = await StatsService.EquipDiceAsync(skin.Id);
                busy = false;
                if (this == null) return;
                Say(ok ? skin.Name + " dice equipped." : "", ok);
                if (ok) AudioService.Play(SfxId.Home);
                Refresh();
                return;
            }

            if (!skin.IsBuyable)                                  // a level or rank design: it opens by itself, it is not for sale
            {
                Say(skin.Name + " unlocks at " + skin.RequirementText() + ".", false);
                AudioService.Play(SfxId.Error);
                return;
            }
            if (!DiceSkins.CanBuy(skin.Id, s.coins, s.Level, s.rating, s.diceOwned, out int price))
            {
                Say("You need " + price.ToString("N0") + " coins for " + skin.Name + ". Keep playing or claim your daily reward!", false);
                AudioService.Play(SfxId.Error);
                return;
            }

            busy = true;
            Say("", true);
            int paid = await StatsService.BuyDiceAsync(skin.Id);
            if (paid > 0) await StatsService.EquipDiceAsync(skin.Id);
            busy = false;
            if (this == null) return;
            Say(paid > 0 ? skin.Name + " unlocked for " + paid.ToString("N0") + " coins!" : "That did not go through.", paid > 0);
            if (paid > 0) AudioService.Play(SfxId.Win);
            Refresh();
        }
    }
}
