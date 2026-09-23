using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ludo.Game
{
    /// <summary>
    /// The pop-up where a player sets their profile: a name and a picture, either one of the avatars or (for the phone's
    /// owner) a photo from the gallery. It only edits what it is given and hands the result back through a callback;
    /// saving the name and avatar is the caller's job, the photo is saved here (see ProfilePhoto).
    /// </summary>
    public sealed class ProfileEditor : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] TMP_InputField input;
        [SerializeField] Image preview;                // big picture of the current choice
        [SerializeField] Button[] avatarButtons;       // the grid
        [SerializeField] Image[] avatarImages;
        [SerializeField] GameObject[] avatarSelected;  // outline shown on the chosen one
        [SerializeField] Button galleryButton;         // "From Gallery" (phone owner only)
        [SerializeField] Button removePhotoButton;     // "Remove photo" (only while a photo is set)
        [SerializeField] TMP_Text photoHint;           // one line under the buttons ("Could not open the gallery", ...)
        [SerializeField] Button countryButton;         // phone owner only: opens the country list
        [SerializeField] Image countryFlag;            // the chosen country's flag on that button (hidden if none)
        [SerializeField] TMP_Text countryLabel;        // "Pakistan" / "Choose your country"
        [SerializeField] CountryPicker countryPicker;

        Action<string, int> onDone;
        int selected;
        int player;
        bool photoMode;                                // the preview shows a photo (current or just picked), no avatar outline
        byte[] pickedPhoto;                            // chosen from the gallery, saved when OK is pressed
        bool removePhoto;                              // "Remove photo" pressed, applied when OK is pressed
        bool picking;
        string country = "";                           // the country shown; saved when OK is pressed

        void Awake()
        {
            for (int i = 0; i < avatarButtons.Length; i++)
            {
                int index = i;      // each button needs its own copy of the number
                avatarButtons[i].onClick.AddListener(() => Select(index));
            }
            if (galleryButton != null) galleryButton.onClick.AddListener(ChooseFromGallery);
            if (removePhotoButton != null) removePhotoButton.onClick.AddListener(RemovePhoto);
            if (countryButton != null) countryButton.onClick.AddListener(ChooseCountry);
        }

        public void Open(int playerNumber, string currentName, int currentAvatar, Action<string, int> done)
        {
            onDone = done;
            player = playerNumber;
            pickedPhoto = null;
            removePhoto = false;
            picking = false;
            input.text = currentName;
            for (int i = 0; i < avatarImages.Length; i++)
            {
                avatarImages[i].sprite = AvatarLibrary.Get(i);
                avatarButtons[i].gameObject.SetActive(i < AvatarLibrary.Count);
            }
            Select(currentAvatar);
            removePhoto = false;                                     // (Select marks it for removal; opening changes nothing yet)
            photoMode = player == 0 && ProfilePhoto.Has;
            if (photoMode) ShowPhoto(ProfilePhoto.Mine);
            bool photosAllowed = player == 0 && GalleryPicker.Supported;
            if (galleryButton != null) galleryButton.gameObject.SetActive(photosAllowed);
            RefreshRemove();
            SetHint("");
            country = player == 0 ? GameSettings.Country : "";
            if (countryButton != null) countryButton.gameObject.SetActive(player == 0 && countryPicker != null);   // online players only use player 1's profile
            RefreshCountry();
            panel.SetActive(true);
        }

        void Select(int index)
        {
            selected = index;
            photoMode = false;
            pickedPhoto = null;
            if (player == 0 && ProfilePhoto.Has) removePhoto = true;      // choosing an avatar replaces the photo
            preview.sprite = AvatarLibrary.Get(index);
            for (int i = 0; i < avatarSelected.Length; i++) avatarSelected[i].SetActive(i == index);
            RefreshRemove();
        }

        void ShowPhoto(Sprite sprite)
        {
            preview.sprite = sprite;
            for (int i = 0; i < avatarSelected.Length; i++) avatarSelected[i].SetActive(false);
        }

        void RefreshRemove()
        {
            if (removePhotoButton != null) removePhotoButton.gameObject.SetActive(player == 0 && photoMode);
        }

        void SetHint(string text)
        {
            if (photoHint != null) photoHint.text = text;
        }

        void ChooseFromGallery()
        {
            if (picking) return;
            picking = true;
            SetHint("");
            GalleryPicker.Pick(bytes =>
            {
                picking = false;
                if (this == null || !panel.activeSelf) return;
                if (bytes == null) return;                                   // cancelled
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(bytes, false)) { Destroy(tex); SetHint("That picture could not be used."); return; }
                pickedPhoto = bytes;
                removePhoto = false;
                photoMode = true;
                var square = ProfilePhoto.CropSquare(tex, ProfilePhoto.Size);
                Destroy(tex);
                ProfilePhoto.RoundCorners(square);
                if (preview.sprite != null && preview.sprite.name == "PickedPreview")
                {
                    var old = preview.sprite.texture;
                    Destroy(preview.sprite);
                    Destroy(old);
                }
                var sprite = Sprite.Create(square, new Rect(0, 0, square.width, square.height), new Vector2(0.5f, 0.5f), 100f);
                sprite.name = "PickedPreview";
                ShowPhoto(sprite);
                RefreshRemove();
            });
        }

        void RemovePhoto()
        {
            pickedPhoto = null;
            removePhoto = true;
            photoMode = false;
            Select(selected);
            removePhoto = true;
            RefreshRemove();
        }

        void ChooseCountry()
        {
            if (countryPicker == null) return;
            countryPicker.Open(country, code => { country = code; RefreshCountry(); });
        }

        void RefreshCountry()
        {
            if (countryLabel != null)
                countryLabel.text = country.Length > 0 ? Ludo.Core.Countries.NameOf(country) : "Choose your country";
            if (countryFlag != null)
            {
                countryFlag.sprite = FlagLibrary.Get(country);
                countryFlag.gameObject.SetActive(countryFlag.sprite != null);
            }
        }

        /// <summary>OK button.</summary>
        public void Confirm()
        {
            string typed = input.text;      // read BEFORE closing: deactivating a text field can reset its text
            panel.SetActive(false);
            if (player == 0)
            {
                if (pickedPhoto != null) ProfilePhoto.SetMine(pickedPhoto);
                else if (removePhoto && ProfilePhoto.Has) ProfilePhoto.ClearMine();
                GameSettings.Country = country;
            }
            onDone?.Invoke(typed, selected);
        }

        /// <summary>Cancel button.</summary>
        public void Cancel()
        {
            if (countryPicker != null && countryPicker.IsOpen) countryPicker.Close();
            panel.SetActive(false);
        }
    }
}
