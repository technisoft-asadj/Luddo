using NUnit.Framework;
using UnityEngine;
using Ludo.Core;
using Ludo.Game;
using Ludo.Online;

namespace Ludo.Tests
{
    /// <summary>
    /// The 3D dice only ever SHOWS the engine's number: whatever side physics leaves on top, relabelling puts the given value
    /// there. And country codes: only real ISO codes survive, nothing is guessed, stale data cannot leak between accounts.
    /// </summary>
    public class DiceAndCountryTests
    {
        [Test]
        public void RelabelPutsEveryValueOnEverySide()
        {
            for (int value = 1; value <= 6; value++)
                for (int up = 0; up < DiceSimulator.Axes.Length; up++)
                {
                    var r = DiceMesh.Relabel(value, up);
                    Vector3 side = r * DiceMesh.FaceNormal[value - 1];
                    Assert.Greater(Vector3.Dot(side, DiceSimulator.Axes[up]), 0.999f, "value " + value + " up " + up);
                    // a cube symmetry: every axis still lands on an axis, so the dice lies exactly as physics left it
                    foreach (var axis in DiceSimulator.Axes)
                    {
                        Vector3 turned = r * axis;
                        float m = Mathf.Max(Mathf.Abs(turned.x), Mathf.Abs(turned.y), Mathf.Abs(turned.z));
                        Assert.Greater(m, 0.999f);
                    }
                }
        }

        [Test]
        public void TheValueOnTopIsTheEngineValueForAnyLandingRotation()
        {
            var random = new System.Random(7);
            for (int i = 0; i < 500; i++)
            {
                // any flat landing: one of the 24 cube orientations, turned any amount about the vertical
                var landing = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f)
                    * Quaternion.Euler(90f * random.Next(4), 90f * random.Next(4), 90f * random.Next(4));
                int up = DiceSimulator.UpAxis(landing, out float flat);
                Assert.Greater(flat, 0.999f);
                int value = 1 + random.Next(6);
                var shown = landing * DiceMesh.Relabel(value, up);
                int top = 0; float best = -2f;
                for (int v = 1; v <= 6; v++)
                {
                    float d = Vector3.Dot(shown * DiceMesh.FaceNormal[v - 1], Vector3.up);
                    if (d > best) { best = d; top = v; }
                }
                Assert.AreEqual(value, top);
            }
        }

        [Test]
        public void OppositeSidesAddUpToSeven()
        {
            for (int v = 1; v <= 6; v++)
                Assert.Less(Vector3.Dot(DiceMesh.FaceNormal[v - 1], DiceMesh.FaceNormal[7 - v - 1]), -0.999f);
        }

        [Test]
        public void DiceModelIsClosedAndFacesOutwards()
        {
            var mesh = DiceMesh.Build();
            var v = mesh.vertices; var t = mesh.triangles;
            Assert.AreEqual(6 * 2 + 12 * 2 + 8, t.Length / 3);           // 6 faces, 12 bevels, 8 corners
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                Vector3 centre = (a + b + c) / 3f;
                Assert.Greater(Vector3.Dot(Vector3.Cross(b - a, c - a), centre), 0f, "triangle " + i / 3 + " faces inwards");
            }
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void CountryCodesAreNormalisedAndUnknownOnesDropped()
        {
            Assert.AreEqual("PK", Countries.Normalize("pk"));
            Assert.AreEqual("GB", Countries.Normalize(" gb "));
            Assert.AreEqual("", Countries.Normalize("UK"));             // not an ISO code
            Assert.AreEqual("", Countries.Normalize("Pakistan"));
            Assert.AreEqual("", Countries.Normalize(null));
            Assert.AreEqual("", Countries.Normalize("<b>"));
            Assert.AreEqual("Pakistan", Countries.NameOf("PK"));
            Assert.IsTrue(Countries.Matches(new Countries.Country("AE", "United Arab Emirates"), "arab"));
            Assert.IsTrue(Countries.Matches(new Countries.Country("US", "United States"), "us"));
            Assert.IsFalse(Countries.Matches(new Countries.Country("US", "United States"), "pak"));
        }

        [Test]
        public void EveryCountryHasAFlag()
        {
            foreach (var c in Countries.All)
                Assert.IsNotNull(FlagLibrary.Get(c.Code), c.Code + " has no flag");
            Assert.IsNull(FlagLibrary.Get(""));
            Assert.IsNull(FlagLibrary.Get("ZZ"));
        }

        [Test]
        public void ScoreMetadataCountryIsReadSafely()
        {
            Assert.AreEqual("PK", LeaderboardService.CountryIn("{\"country\":\"PK\"}"));
            Assert.AreEqual("", LeaderboardService.CountryIn("{\"country\":\"XX\"}"));
            Assert.AreEqual("", LeaderboardService.CountryIn("not json"));
            Assert.AreEqual("", LeaderboardService.CountryIn(null));
        }

        [Test]
        public void LoggingOutClearsTheCountry()
        {
            string saved = PlayerPrefs.HasKey("ludo.country") ? PlayerPrefs.GetString("ludo.country") : null;
            string savedName = PlayerPrefs.HasKey("ludo.name.0") ? PlayerPrefs.GetString("ludo.name.0") : null;
            int savedAvatar = PlayerPrefs.HasKey("ludo.avatar.0") ? PlayerPrefs.GetInt("ludo.avatar.0") : -1;
            try
            {
                GameSettings.Country = "PK";
                GameSettings.CountryAsked = true;
                Assert.AreEqual("PK", GameSettings.Country);
                GameSettings.Country = "nonsense";
                Assert.AreEqual("", GameSettings.Country);             // garbage is never stored
                GameSettings.Country = "US";
                GameSettings.ResetPlayer(0);                           // what logout / account deletion does
                Assert.AreEqual("", GameSettings.Country);
                Assert.IsFalse(GameSettings.CountryAsked);
            }
            finally
            {
                if (saved != null) PlayerPrefs.SetString("ludo.country", saved); else PlayerPrefs.DeleteKey("ludo.country");
                if (savedName != null) PlayerPrefs.SetString("ludo.name.0", savedName);
                if (savedAvatar >= 0) PlayerPrefs.SetInt("ludo.avatar.0", savedAvatar);
                PlayerPrefs.Save();
            }
        }
    }
}
