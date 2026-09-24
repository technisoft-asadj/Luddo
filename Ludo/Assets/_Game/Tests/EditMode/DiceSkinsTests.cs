using System.Linq;
using NUnit.Framework;
using Ludo.Core;

namespace Ludo.Tests
{
    /// <summary>
    /// The dice collection: who owns what, what can be bought, and - most importantly - that a design is nothing but a
    /// picture. Nothing here may ever touch a dice value.
    /// </summary>
    public class DiceSkinsTests
    {
        [Test]
        public void TheCatalogueIsSaneAndTheDefaultIsAlwaysFree()
        {
            var all = DiceSkins.All;
            Assert.Greater(all.Length, 1);
            CollectionAssert.AllItemsAreUnique(all.Select(s => s.Id).ToList(), "two designs share an id");
            Assert.AreEqual(DiceSkins.Default, all[0].Id, "the default has to be first, it is the fallback");
            Assert.AreEqual(DiceUnlock.Free, all[0].Unlock);
            foreach (var s in all)
            {
                Assert.IsNotEmpty(s.Id);
                Assert.IsNotEmpty(s.Name);
                Assert.IsNotEmpty(s.RequirementText());
            }
        }

        [Test]
        public void EverybodyOwnsTheDefaultFromTheStart()
        {
            Assert.IsTrue(DiceSkins.IsOwned(DiceSkins.Default, level: 1, rankPoints: 0, owned: ""));
        }

        [Test]
        public void ALevelDesignOpensByItselfAtThatLevel()
        {
            var skin = DiceSkins.All.First(s => s.Unlock == DiceUnlock.Level);
            Assert.IsFalse(DiceSkins.IsOwned(skin.Id, skin.Requirement - 1, 0, ""));
            Assert.IsTrue(DiceSkins.IsOwned(skin.Id, skin.Requirement, 0, ""));
            Assert.IsTrue(DiceSkins.IsOwned(skin.Id, skin.Requirement + 5, 0, ""));
        }

        [Test]
        public void ARankDesignOpensByItselfAtThatRank()
        {
            var skin = DiceSkins.All.First(s => s.Unlock == DiceUnlock.Rank);
            Assert.IsFalse(DiceSkins.IsOwned(skin.Id, 99, skin.Requirement - 1, ""));
            Assert.IsTrue(DiceSkins.IsOwned(skin.Id, 1, skin.Requirement, ""));
        }

        [Test]
        public void ADesignForSaleHasToBeBoughtHoweverHighTheLevel()
        {
            var skin = DiceSkins.All.First(s => s.IsBuyable);
            Assert.IsFalse(DiceSkins.IsOwned(skin.Id, 99, 99999, ""), "a high level does not hand out shop designs");
            Assert.IsTrue(DiceSkins.IsOwned(skin.Id, 1, 0, skin.Id));
        }

        [Test]
        public void BuyingNeedsEnoughCoinsAndNeverHappensTwice()
        {
            var skin = DiceSkins.All.First(s => s.IsBuyable);
            Assert.IsFalse(DiceSkins.CanBuy(skin.Id, skin.Requirement - 1, 1, 0, "", out int price));
            Assert.AreEqual(skin.Requirement, price, "the price is reported even when it cannot be paid");
            Assert.IsTrue(DiceSkins.CanBuy(skin.Id, skin.Requirement, 1, 0, "", out price));
            Assert.IsFalse(DiceSkins.CanBuy(skin.Id, 99999, 1, 0, skin.Id, out _), "already owned");
        }

        [Test]
        public void ADesignThatIsNotForSaleCannotBeBought()
        {
            foreach (var skin in DiceSkins.All.Where(s => !s.IsBuyable))
                Assert.IsFalse(DiceSkins.CanBuy(skin.Id, 999999, 1, 0, "", out _), skin.Id);
        }

        [Test]
        public void TheOwnedListNeverGrowsDuplicatesAndRefusesNonsense()
        {
            var skin = DiceSkins.All.First(s => s.IsBuyable);
            string owned = DiceSkins.Add("", skin.Id);
            Assert.AreEqual(skin.Id, owned);
            Assert.AreEqual(owned, DiceSkins.Add(owned, skin.Id), "added twice");
            Assert.AreEqual(owned, DiceSkins.Add(owned, "no-such-design"));
            Assert.IsTrue(DiceSkins.Contains(owned, skin.Id));
            Assert.IsFalse(DiceSkins.Contains(owned, "no-such-design"));
        }

        [Test]
        public void WearingSomethingYouDoNotOwnFallsBackToTheDefault()
        {
            var locked = DiceSkins.All.First(s => s.IsBuyable);
            Assert.AreEqual(DiceSkins.Default, DiceSkins.Equipped(locked.Id, 1, 0, ""));
            Assert.AreEqual(locked.Id, DiceSkins.Equipped(locked.Id, 1, 0, locked.Id));
            Assert.AreEqual(DiceSkins.Default, DiceSkins.Equipped("no-such-design", 99, 99999, ""));
        }

        [Test]
        public void AnUnknownIdIsNeverOwnedAndFallsBackWhenLookedUp()
        {
            Assert.IsFalse(DiceSkins.Exists("no-such-design"));
            Assert.IsFalse(DiceSkins.IsOwned("no-such-design", 99, 99999, "no-such-design"));
            Assert.AreEqual(DiceSkins.Default, DiceSkins.Get("no-such-design").Id);
        }

        /// <summary>
        /// The whole point of the collection: designs are cosmetic. The engine takes its numbers from IDiceSource and no
        /// part of the catalogue can reach it, so two players with different designs must roll identically from one seed.
        /// </summary>
        [Test]
        public void TheDesignCannotChangeWhatTheDiceRolls()
        {
            int[] Play(string _)
            {
                var g = T.Game(2, new ScriptedDice(6, 3, 5, 2, 6, 4, 1, 1, 2, 3));
                var rolled = new System.Collections.Generic.List<int>();
                g.Rolled += r => rolled.Add(r.Value);
                for (int i = 0; i < 5; i++)
                {
                    g.Roll();
                    while (g.Phase == TurnPhase.WaitingForMove) g.Play(g.LegalMoves[0]);
                }
                return rolled.ToArray();
            }
            CollectionAssert.AreEqual(Play(DiceSkins.Default), Play(DiceSkins.All.Last().Id));
        }
    }
}
