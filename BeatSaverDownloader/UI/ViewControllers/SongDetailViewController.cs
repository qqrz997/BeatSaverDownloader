﻿using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Notify;
using HMUI;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Threading.Tasks;
using IPA.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace BeatSaverDownloader.UI.ViewControllers
{
    public class SongDetailViewController : BeatSaberMarkupLanguage.ViewControllers.BSMLResourceViewController, INotifiableHost
    {
        public override string ResourceName => "BeatSaverDownloader.UI.BSML.songDetail.bsml";

        private GameObject _levelDetails;
        private bool _detailViewSetup = false;
        private BeatSaverSharp.Models.Beatmap _currentSong;

        private BeatmapDifficultySegmentedControlController _difficultiesSegmentedControllerClone;
        private BeatmapCharacteristicSegmentedControlController _characteristicSegmentedControllerClone;
        private TextSegmentedControl _diffSegmentedControl;
        private IconSegmentedControl _characteristicSegmentedControl;

        private BeatSaverSharp.Models.BeatmapDifficulty.BeatmapCharacteristic _selectedCharacteristic;
        private BeatSaverSharp.Models.BeatmapDifficulty[] _currentDifficulties;

        private TextMeshProUGUI _songNameText;
        private ImageView _coverImage;

        private TextMeshProUGUI _timeText;
        private TextMeshProUGUI _bpmText;
        private TextMeshProUGUI _songSubText;
        private CurvedTextMeshPro _npsText;
        private CurvedTextMeshPro _notesText;
        private CurvedTextMeshPro _obstaclesText;
        private CurvedTextMeshPro _bombsText;
        private CurvedTextMeshPro _upText;
        private CurvedTextMeshPro _downText;
        private bool _downloadInteractable = false;

        public Action<BeatSaverSharp.Models.Beatmap, Sprite> didPressDownload;
        public Action<BeatSaverSharp.Models.User> didPressUploader;
        public Action<string> setDescription;

        private bool _showAutoGeneratedText = false;
        [UIValue("show-auto-generated-text")]
        public bool ShowAutoGeneratedText
        {
            get => _showAutoGeneratedText;
            set
            {
                _showAutoGeneratedText = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue("downloadInteractable")]
        public bool DownloadInteractable
        {
            get => _downloadInteractable;
            set
            {
                _downloadInteractable = value;
                NotifyPropertyChanged();
            }
        }
        private bool _uploaderInteractable = true;
        [UIValue("uploaderInteractable")]
        public bool UploaderInteractable
        {
            get => _uploaderInteractable;
            set
            {
                _uploaderInteractable = value;
                NotifyPropertyChanged();
            }
        }
        [UIAction("#post-parse")]
        internal void Setup()
        {
            (transform as RectTransform).sizeDelta = new Vector2(70, 0);
            (transform as RectTransform).anchorMin = new Vector2(0.5f, 0);
            (transform as RectTransform).anchorMax = new Vector2(0.5f, 1);

            SetupDetailView();
        }

        [UIAction("downloadPressed")]
        internal void DownloadPressed()
        {
            didPressDownload?.Invoke(_currentSong, _coverImage.sprite);
            DownloadInteractable = false;
        }
        [UIAction("uploaderPressed")]
        internal void UploaderPressed()
        {
            didPressUploader?.Invoke(_currentSong.Uploader);
        }

        internal void ClearData()
        {
            if (_detailViewSetup)
            {
                //Clear all the data
                //      _timeText.text = "--";
                //      _bpmText.text = "--";
                _npsText.text = "--";
                _notesText.text = "--";
                _obstaclesText.text = "--";
                _bombsText.text = "--";
                _upText.text = "--";
                _downText.text = "--";
                _songNameText.text = "--";
                _songSubText.text = "--";
                _coverImage.sprite = Misc.Sprites.LoadSpriteFromTexture(Texture2D.blackTexture);
                _diffSegmentedControl.SetTexts(new string[] { });
                _characteristicSegmentedControl.SetData(new IconSegmentedControl.DataItem[] { });
                DownloadInteractable = false;
                UploaderInteractable = false;
                ShowAutoGeneratedText = false;
                PluginUI._songPreviewPlayer.CrossfadeToDefault();
            }
        }

        internal async void Initialize(StrongBox<BeatSaverSharp.Models.Beatmap> song, Sprite cover, Task<AudioClip> clip)
        {
            _currentSong = song.Value;

            _songNameText.text = _currentSong.Metadata.SongName;
            if (cover != null)
                _coverImage.sprite = cover;
            UpdateDownloadButtonStatus();
            SetupCharacteristicDisplay();
            // new beatsaversharp has no characteristics. gotta make do
            var possibleCharacteristics = GetSelectedSongCharacteristics();
            if (possibleCharacteristics.Contains(BeatSaverSharp.Models.BeatmapDifficulty.BeatmapCharacteristic.Standard)) SelectedCharacteristic(BeatSaverSharp.Models.BeatmapDifficulty.BeatmapCharacteristic.Standard);
            else SelectedCharacteristic(possibleCharacteristics[0]);
            UploaderInteractable = true;
            ShowAutoGeneratedText = _currentSong.Automapper == true;
            setDescription?.Invoke(_currentSong.Description);

            // Load song preview
            var preview = await clip;

            // Time may have passed
            if (_currentSong == song.Value)
                PluginUI._songPreviewPlayer.CrossfadeTo(preview, 1f, 0f, 10f, () => { });
        }

        internal void UpdateDownloadButtonStatus()
        {
            DownloadInteractable = !Misc.SongDownloader.Instance.IsSongDownloaded(_currentSong.LatestVersion.Hash);
        }

        protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
        {
            base.DidDeactivate(removedFromHierarchy, screenSystemDisabling);
        }

        internal BeatSaverSharp.Models.BeatmapDifficulty.BeatmapCharacteristic[] GetSelectedSongCharacteristics()
        {
            if(_currentSong != null)
            {
                return _currentSong.LatestVersion.Difficulties.Select(x => x.Characteristic).Distinct().ToArray();
            }
            else
            {
                return new BeatSaverSharp.Models.BeatmapDifficulty.BeatmapCharacteristic[] { BeatSaverSharp.Models.BeatmapDifficulty.BeatmapCharacteristic.Standard };
            }
        }

        internal void SetupDetailView()
        {
            _levelDetails = Instantiate(PluginUI._levelDetailClone, gameObject.transform);
            _levelDetails.gameObject.SetActive(false);

            _characteristicSegmentedControllerClone = _levelDetails.GetComponentInChildren<BeatmapCharacteristicSegmentedControlController>();
            _characteristicSegmentedControl = CreateIconSegmentedControl(_characteristicSegmentedControllerClone.transform as RectTransform, new Vector2(0, 0), new Vector2(0, 0),
                delegate (int value) { SelectedCharacteristic(GetSelectedSongCharacteristics()[value]); });

            _difficultiesSegmentedControllerClone = _levelDetails.GetComponentInChildren<BeatmapDifficultySegmentedControlController>();
            _diffSegmentedControl = CreateTextSegmentedControl(_difficultiesSegmentedControllerClone.transform as RectTransform, new Vector2(0, 0), new Vector2(0, 0),
                delegate (int value) { SelectedDifficulty(_currentDifficulties[value]); }, 3.5f, 1);

            var levelBar = _levelDetails.GetComponentInChildren<LevelBar>();
            levelBar.GetField<GameObject, LevelBar>("_singleLineSongInfoContainer").SetActive(true);
            levelBar.GetField<GameObject, LevelBar>("_multiLineSongInfoContainer").SetActive(false);
            _songNameText = levelBar.GetField<TextMeshProUGUI, LevelBar>("_songNameText");
            _songSubText = levelBar.GetField<TextMeshProUGUI, LevelBar>("_authorNameText");
            _coverImage = levelBar.GetField<ImageView, LevelBar>("_songArtworkImageView");

            _songSubText.overflowMode = TextOverflowModes.Overflow;
            _songSubText.enableWordWrapping = false;

            //   _timeText = _levelDetails.GetComponentsInChildren<TextMeshProUGUI>().First(x => x.gameObject.transform.parent.name == "Time");
            //   _bpmText = _levelDetails.GetComponentsInChildren<TextMeshProUGUI>().First(x => x.gameObject.transform.parent.name == "BPM");

            _npsText = _levelDetails.GetComponentsInChildren<CurvedTextMeshPro>().First(x => x.gameObject.transform.parent.name == "NPS");
            _notesText = _levelDetails.GetComponentsInChildren<CurvedTextMeshPro>().First(x => x.gameObject.transform.parent.name == "NotesCount");
            _obstaclesText = _levelDetails.GetComponentsInChildren<CurvedTextMeshPro>().First(x => x.gameObject.transform.parent.name == "ObstaclesCount");
            _bombsText = _levelDetails.GetComponentsInChildren<CurvedTextMeshPro>().First(x => x.gameObject.transform.parent.name == "BombsCount");

            CreateVoteDisplay();
            //     _timeText.text = "--";
            //      _bpmText.text = "--";
            _songSubText.text = "--";
            _npsText.text = "--";
            _notesText.text = "--";
            _obstaclesText.text = "--";
            _bombsText.text = "--";
            _songNameText.text = "--";
            _upText.text = "--";
            _downText.text = "--";
            _detailViewSetup = true;
            _levelDetails.gameObject.SetActive(true);
        }
        private void CreateVoteDisplay()
        {
            var bombsDisplay = _levelDetails.transform.Find("BeatmapParamsPanel").Find("BombsCount");
            var layout = bombsDisplay.parent.gameObject.AddComponent<HorizontalLayoutGroup>();
            var upDisplay = GameObject.Instantiate(bombsDisplay, _bombsText.transform.parent.parent);
            upDisplay.gameObject.name = "UpVotesCount";
            upDisplay.GetComponentInChildren<ImageView>().sprite = BeatSaverDownloader.Misc.Sprites.ThumbUp;
            _upText = upDisplay.GetComponentInChildren<CurvedTextMeshPro>();
            var downDisplay = GameObject.Instantiate(bombsDisplay, _bombsText.transform.parent.parent);
            downDisplay.gameObject.name = "DownVotesCount";
            downDisplay.GetComponentInChildren<ImageView>().sprite = BeatSaverDownloader.Misc.Sprites.ThumbDown;
            _downText = downDisplay.GetComponentInChildren<CurvedTextMeshPro>();
        }
        public void SelectedDifficulty(BeatSaverSharp.Models.BeatmapDifficulty difficulty)
        {
            //       _timeText.text = $"{Math.Floor((double)difficulty.Length / 60):N0}:{Math.Floor((double)difficulty.Length % 60):00}";
            //       _bpmText.text = _currentSong.Metadata.BPM.ToString();
            _songSubText.text = $"{_currentSong.Metadata.BPM.ToString()} BPM   " + $"{Math.Floor((double)_currentSong.Metadata.Duration / 60):N0}:{Math.Floor((double)_currentSong.Metadata.Duration % 60):00}";
            _npsText.text = difficulty.NPS.ToString("F2");
            _notesText.text = difficulty.Notes.ToString();
            _obstaclesText.text = difficulty.Obstacles.ToString();
            _bombsText.text = difficulty.Bombs.ToString();
            _upText.text = _currentSong.Stats.Upvotes.ToString();
            _downText.text = _currentSong.Stats.Downvotes.ToString();
        }

        public void SelectedCharacteristic(BeatSaverSharp.Models.BeatmapDifficulty.BeatmapCharacteristic characteristic)
        {
            _selectedCharacteristic = characteristic;
            if (_diffSegmentedControl != null)
                SetupDifficultyDisplay();
        }

        internal void SetupDifficultyDisplay()
        {
            var diffs = _currentSong.LatestVersion.Difficulties.Where(x => x.Characteristic == _selectedCharacteristic);
            var diffNames = diffs.Select(x => ToDifficultyString(x.Difficulty)).OrderBy(x => DiffOrder(x)).ToList();

            for (int i = 0; i < diffNames.Count; ++i)
            {
                diffNames[i] = ToDifficultyName(diffNames[i]);
            }

            _currentDifficulties = diffs.ToArray();

            _diffSegmentedControl.SetTexts(diffNames.ToArray());
            foreach (var text in _diffSegmentedControl.GetComponentsInChildren<TextMeshProUGUI>()) text.enableWordWrapping = false;

            if (diffs.Count() > 0)
                _diffSegmentedControl.SelectCellWithNumber(0);
            if (_currentDifficulties != null)
                SelectedDifficulty(_currentDifficulties[0]);
        }

        private void SetupCharacteristicDisplay()
        {
            var possibleCharacteristics = GetSelectedSongCharacteristics();
            List<IconSegmentedControl.DataItem> characteristics = new List<IconSegmentedControl.DataItem>();
            for (int i = 0; i < possibleCharacteristics.Length; i++)
            {
                var c = possibleCharacteristics[i].ToString();
                if (c.StartsWith("_")) c = c.Substring(1);
                BeatmapCharacteristicSO characteristic = SongCore.Loader.beatmapCharacteristicCollection.GetBeatmapCharacteristicBySerializedName(c.ToString());
                if (characteristic.characteristicNameLocalizationKey == "Missing Characteristic")
                {
                    characteristics.Add(new IconSegmentedControl.DataItem(characteristic.icon, $"Missing Characteristic: {c.ToString()}"));
                }
                else
                    characteristics.Add(new IconSegmentedControl.DataItem(characteristic.icon, Polyglot.Localization.Get(characteristic.descriptionLocalizationKey)));
            }

            _characteristicSegmentedControl.SetData(characteristics.ToArray());
        }
        internal static string ToDifficultyString(BeatSaverSharp.Models.BeatmapDifficulty.BeatSaverBeatmapDifficulty difficulty)
        {
            if (difficulty == BeatSaverSharp.Models.BeatmapDifficulty.BeatSaverBeatmapDifficulty.Easy)
                return "easy";
            else if (difficulty == BeatSaverSharp.Models.BeatmapDifficulty.BeatSaverBeatmapDifficulty.Normal)
                return "normal";
            else if (difficulty == BeatSaverSharp.Models.BeatmapDifficulty.BeatSaverBeatmapDifficulty.Hard)
                return "hard";
            else if (difficulty == BeatSaverSharp.Models.BeatmapDifficulty.BeatSaverBeatmapDifficulty.Expert)
                return "expert";
            else if (difficulty == BeatSaverSharp.Models.BeatmapDifficulty.BeatSaverBeatmapDifficulty.ExpertPlus)
                return "expertPlus";
            else
                return "--";
        }

        internal static string ToDifficultyName(string name)
        {
            if (name == "easy")
                return "Easy";
            else if (name == "normal")
                return "Normal";
            else if (name == "hard")
                return "Hard";
            else if (name == "expert")
                return "Expert";
            else if (name == "expertPlus")
                return "Expert+";
            else
                return "--";
        }

        internal static int DiffOrder(string name)
        {
            switch (name)
            {
                case "easy":
                    return 0;

                case "normal":
                    return 1;

                case "hard":
                    return 2;

                case "expert":
                    return 3;

                case "expertPlus":
                    return 4;

                default:
                    return 5;
            }
        }

        public static TextSegmentedControl CreateTextSegmentedControl(RectTransform parent, Vector2 anchoredPosition, Vector2 sizeDelta, Action<int> onValueChanged = null, float fontSize = 4f, float padding = 8f)
        {
            var segmentedControl = new GameObject("CustomTextSegmentedControl", typeof(RectTransform)).AddComponent<TextSegmentedControl>();
            segmentedControl.gameObject.AddComponent<HorizontalLayoutGroup>();

            TextSegmentedControlCell[] _segments = Resources.FindObjectsOfTypeAll<TextSegmentedControlCell>();

            segmentedControl.SetField("_singleCellPrefab", _segments.First(x => x.name == "SingleHorizontalTextSegmentedControlCell"));
            segmentedControl.SetField("_firstCellPrefab", _segments.First(x => x.name == "LeftHorizontalTextSegmentedControlCell"));
            segmentedControl.SetField("_middleCellPrefab", _segments.Last(x => x.name == "MiddleHorizontalTextSegmentedControlCell"));
            segmentedControl.SetField("_lastCellPrefab", _segments.Last(x => x.name == "RightHorizontalTextSegmentedControlCell"));

            segmentedControl.SetField("_container", Resources.FindObjectsOfTypeAll<TextSegmentedControl>().Select(x => x.GetField<DiContainer, TextSegmentedControl>("_container")).First(x => x != null));

            segmentedControl.transform.SetParent(parent, false);
            (segmentedControl.transform as RectTransform).anchorMax = new Vector2(0.5f, 0.5f);
            (segmentedControl.transform as RectTransform).anchorMin = new Vector2(0.5f, 0.5f);
            (segmentedControl.transform as RectTransform).anchoredPosition = anchoredPosition;
            (segmentedControl.transform as RectTransform).sizeDelta = sizeDelta;

            segmentedControl.SetField("_fontSize", fontSize);
            segmentedControl.SetField("_padding", padding);
            if (onValueChanged != null)
                segmentedControl.didSelectCellEvent += (sender, index) => { onValueChanged(index); };

            return segmentedControl;
        }

        public static IconSegmentedControl CreateIconSegmentedControl(RectTransform parent, Vector2 anchoredPosition, Vector2 sizeDelta, Action<int> onValueChanged = null)
        {
            var segmentedControl = new GameObject("CustomIconSegmentedControl", typeof(RectTransform)).AddComponent<IconSegmentedControl>();
            segmentedControl.gameObject.AddComponent<HorizontalLayoutGroup>();

            IconSegmentedControlCell[] _segments = Resources.FindObjectsOfTypeAll<IconSegmentedControlCell>();

            segmentedControl.SetField("_singleCellPrefab", _segments.First(x => x.name == "SingleHorizontalIconSegmentedControlCell"));
            segmentedControl.SetField("_firstCellPrefab", _segments.First(x => x.name == "LeftHorizontalIconSegmentedControlCell"));
            segmentedControl.SetField("_middleCellPrefab", _segments.First(x => x.name == "MiddleHorizontalIconSegmentedControlCell"));
            segmentedControl.SetField("_lastCellPrefab", _segments.First(x => x.name == "RightHorizontalIconSegmentedControlCell"));

            segmentedControl.SetField("_container", Resources.FindObjectsOfTypeAll<IconSegmentedControl>().Select(x => x.GetField<DiContainer, IconSegmentedControl>("_container")).First(x => x != null));

            segmentedControl.transform.SetParent(parent, false);
            (segmentedControl.transform as RectTransform).anchorMax = new Vector2(0.5f, 0.5f);
            (segmentedControl.transform as RectTransform).anchorMin = new Vector2(0.5f, 0.5f);
            (segmentedControl.transform as RectTransform).anchoredPosition = anchoredPosition;
            (segmentedControl.transform as RectTransform).sizeDelta = sizeDelta;

            if (onValueChanged != null)
                segmentedControl.didSelectCellEvent += (sender, index) => { onValueChanged(index); };

            return segmentedControl;
        }
    }
}