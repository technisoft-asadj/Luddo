using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Ludo.Core;

namespace Ludo.Game
{
    /// <summary>
    /// The pop-up where the player picks their country: a search box and a scrolling list (flag, name, code), with "No country"
    /// at the top. It only hands the chosen ISO code back; saving is the caller's job. The rows are made the first time it
    /// opens (one copy of the template per country) and the search just shows/hides them, so typing stays fast on a phone.
    /// </summary>
    public sealed class CountryPicker : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] TMP_InputField search;
        [SerializeField] ScrollRect scroll;
        [SerializeField] RectTransform listRoot;
        [SerializeField] GameObject rowTemplate;            // children: Flag (Image), Name (TMP), Code (TMP), Selected (GameObject); a Button on the root
        [SerializeField] TMP_Text emptyText;                // "No country matches"
        [SerializeField] Color normalColor = new Color(0.94f, 0.97f, 1f);
        [SerializeField] Color selectedColor = new Color(0.86f, 1f, 0.88f);

        readonly List<(string code, string name, GameObject row, Image body)> rows = new List<(string, string, GameObject, Image)>();
        Action<string> onDone;
        string current = "";

        void Awake()
        {
            if (search != null) search.onValueChanged.AddListener(Filter);
        }

        public bool IsOpen => panel != null && panel.activeSelf;

        /// <summary>Show the list with the current choice marked. 'done' gets the chosen code ("" = no country) - not called on Close.</summary>
        public void Open(string currentCode, Action<string> done)
        {
            onDone = done;
            current = Countries.Normalize(currentCode);
            if (rows.Count == 0) Build();
            panel.SetActive(true);
            search.SetTextWithoutNotify("");
            Filter("");
            Mark();
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 1f;
            ScrollToCurrent();
        }

        public void Close() => panel.SetActive(false);

        void Build()
        {
            rowTemplate.SetActive(false);
            AddRow("", "No country (hide my flag)");
            foreach (var c in Countries.All) AddRow(c.Code, c.Name);
        }

        void AddRow(string code, string name)
        {
            var row = Instantiate(rowTemplate, listRoot);
            row.name = code.Length > 0 ? code : "None";
            row.SetActive(true);
            var flag = row.transform.Find("Flag").GetComponent<Image>();
            flag.sprite = FlagLibrary.Get(code);
            flag.enabled = flag.sprite != null;
            flag.preserveAspect = true;
            row.transform.Find("Name").GetComponent<TMP_Text>().text = name;
            row.transform.Find("Code").GetComponent<TMP_Text>().text = code;
            row.GetComponent<Button>().onClick.AddListener(() => Choose(code));
            rows.Add((code, name, row, row.GetComponent<Image>()));
        }

        void Filter(string text)
        {
            int shown = 0;
            foreach (var r in rows)
            {
                bool on = string.IsNullOrWhiteSpace(text)
                    ? true
                    : r.code.Length > 0 && Countries.Matches(new Countries.Country(r.code, r.name), text);
                r.row.SetActive(on);
                if (on) shown++;
            }
            if (emptyText != null) emptyText.gameObject.SetActive(shown == 0);
            scroll.verticalNormalizedPosition = 1f;
        }

        void Mark()
        {
            foreach (var r in rows)
            {
                bool on = r.code == current;
                if (r.body != null) r.body.color = on ? selectedColor : normalColor;
                r.row.transform.Find("Selected").gameObject.SetActive(on);
            }
        }

        /// <summary>Open the list near the country already chosen, so it is visible without scrolling.</summary>
        void ScrollToCurrent()
        {
            if (current.Length == 0) return;
            int index = rows.FindIndex(r => r.code == current);
            if (index < 0) return;
            float content = listRoot.rect.height, view = ((RectTransform)scroll.viewport).rect.height;
            if (content <= view) return;
            float rowY = -((RectTransform)rows[index].row.transform).anchoredPosition.y;
            scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01((rowY - view * 0.4f) / (content - view));
        }

        void Choose(string code)
        {
            current = code;
            panel.SetActive(false);
            onDone?.Invoke(code);
        }
    }
}
