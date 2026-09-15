using System;
using UnityEngine;
using HoneyBeeRush.Common;

namespace HoneyBeeRush.Managers
{
    public enum BoosterType
    {
        HandPicker = 0,
        Shuffle = 1,
        AddSlot = 2
    }

    public enum UserResourceType
    {
        Coin = 0,
        Heart = 1,
        HandPicker = 2,
        Shuffle = 3,
        AddSlot = 4
    }

    [Serializable]
    public sealed class UserData
    {
        public int currentLevel = 1;
        public int coins;
        public int heartCount = 5;
        public int maxHeartCount = 5;
        public long nextHeartRefillUtcTicks;

        public int handPickerCount;
        public int shuffleCount;
        public int addSlotCount;

        public int winLevel;
        public int amountWinStreak;
        public int bestHoneyPercent;

        public bool soundEnabled = true;
        public bool musicEnabled = true;
        public bool vibrationEnabled = true;

        public void Normalize()
        {
            currentLevel = Mathf.Max(1, currentLevel);
            coins = Mathf.Max(0, coins);
            maxHeartCount = Mathf.Max(1, maxHeartCount);
            heartCount = Mathf.Clamp(heartCount, 0, maxHeartCount);

            handPickerCount = Mathf.Max(0, handPickerCount);
            shuffleCount = Mathf.Max(0, shuffleCount);
            addSlotCount = Mathf.Max(0, addSlotCount);

            winLevel = Mathf.Max(0, winLevel);
            amountWinStreak = Mathf.Clamp(amountWinStreak, 0, 5);
            bestHoneyPercent = Mathf.Clamp(bestHoneyPercent, 0, 100);

            if (heartCount >= maxHeartCount) nextHeartRefillUtcTicks = 0;
        }
    }

    [DefaultExecutionOrder(-900)]
    public sealed class UserManager : SingletonDontDestroyMono<UserManager>
    {
        private const string UserDataKey = "HoneyBeeRush.UserData";

        public event Action<UserResourceType, int, int> OnResourceChanged;
        public event Action OnResourcesChanged;
        public event Action<int, int> OnLevelChanged;

        private UserData m_userData;

        public int CurrentLevel { get { EnsureLoaded(); return m_userData.currentLevel; } }
        public int Coins { get { EnsureLoaded(); return m_userData.coins; } }
        public int HeartCount { get { EnsureLoaded(); return m_userData.heartCount; } }
        public int MaxHeartCount { get { EnsureLoaded(); return m_userData.maxHeartCount; } }
        public int WinLevel { get { EnsureLoaded(); return m_userData.winLevel; } }
        public int AmountWinStreak { get { EnsureLoaded(); return m_userData.amountWinStreak; } }
        public int BestHoneyPercent { get { EnsureLoaded(); return m_userData.bestHoneyPercent; } }
        public long NextHeartRefillUtcTicks { get { EnsureLoaded(); return m_userData.nextHeartRefillUtcTicks; } }

        public bool SoundEnabled { get { EnsureLoaded(); return m_userData.soundEnabled; } }
        public bool MusicEnabled { get { EnsureLoaded(); return m_userData.musicEnabled; } }
        public bool VibrationEnabled { get { EnsureLoaded(); return m_userData.vibrationEnabled; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (HasInstance) return;

            var host = new GameObject(nameof(UserManager));
            host.AddComponent<UserManager>();
        }

        protected override void Awake()
        {
            base.Awake();

            if (Instance != this) return;
            EnsureLoaded();
        }

        public void Load()
        {
            string raw = PlayerPrefs.GetString(UserDataKey, string.Empty);

            if (string.IsNullOrEmpty(raw))
            {
                m_userData = new UserData();
                Save();
                return;
            }

            try
            {
                m_userData = JsonUtility.FromJson<UserData>(raw);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[UserManager] Save could not be read, resetting. " + exception.Message);
                m_userData = null;
            }

            if (m_userData == null) m_userData = new UserData();

            m_userData.Normalize();
            Save();
        }

        public void Save()
        {
            EnsureLoaded();
            m_userData.Normalize();
            PlayerPrefs.SetString(UserDataKey, JsonUtility.ToJson(m_userData));
            PlayerPrefs.Save();
        }

        public void ResetProgress()
        {
            m_userData = new UserData();
            Save();
            OnResourcesChanged?.Invoke();
            OnLevelChanged?.Invoke(0, m_userData.currentLevel);
        }

        public void SetCurrentLevel(int level)
        {
            EnsureLoaded();

            level = Mathf.Max(1, level);
            if (m_userData.currentLevel == level) return;

            int previous = m_userData.currentLevel;
            m_userData.currentLevel = level;
            Save();
            OnLevelChanged?.Invoke(previous, level);
        }

        public void SetWinLevel(int value)
        {
            EnsureLoaded();
            value = Mathf.Max(0, value);
            if (m_userData.winLevel == value) return;

            m_userData.winLevel = value;
            Save();
        }

        public void SetAmountWinStreak(int value)
        {
            EnsureLoaded();
            value = Mathf.Clamp(value, 0, 5);
            if (m_userData.amountWinStreak == value) return;

            m_userData.amountWinStreak = value;
            Save();
        }

        public void ReportHoneyPercent(int percent)
        {
            EnsureLoaded();
            percent = Mathf.Clamp(percent, 0, 100);
            if (percent <= m_userData.bestHoneyPercent) return;

            m_userData.bestHoneyPercent = percent;
            Save();
        }

        public void AddCoins(int amount)
        {
            if (amount == 0) return;
            SetCoins(Coins + amount);
        }

        public bool TrySpendCoins(int amount)
        {
            if (amount <= 0) return true;
            if (Coins < amount) return false;

            SetCoins(Coins - amount);
            EventManager.EmitEvent(EventVariables.SpendCoin, amount);
            return true;
        }

        public int GetBoosterCount(BoosterType type)
        {
            EnsureLoaded();

            switch (type)
            {
                case BoosterType.HandPicker: return m_userData.handPickerCount;
                case BoosterType.Shuffle: return m_userData.shuffleCount;
                case BoosterType.AddSlot: return m_userData.addSlotCount;
                default: return 0;
            }
        }

        public void SetBoosterCount(BoosterType type, int amount)
        {
            EnsureLoaded();

            amount = Mathf.Max(0, amount);
            int previous = GetBoosterCount(type);
            if (previous == amount) return;

            switch (type)
            {
                case BoosterType.HandPicker:
                    m_userData.handPickerCount = amount;
                    NotifyResourceChanged(UserResourceType.HandPicker, previous, amount);
                    break;
                case BoosterType.Shuffle:
                    m_userData.shuffleCount = amount;
                    NotifyResourceChanged(UserResourceType.Shuffle, previous, amount);
                    break;
                case BoosterType.AddSlot:
                    m_userData.addSlotCount = amount;
                    NotifyResourceChanged(UserResourceType.AddSlot, previous, amount);
                    break;
            }

            Save();
        }

        public void AddBooster(BoosterType type, int amount)
        {
            if (amount == 0) return;
            SetBoosterCount(type, GetBoosterCount(type) + amount);
        }

        public bool TrySpendBooster(BoosterType type, int amount = 1)
        {
            if (amount <= 0) return true;

            int current = GetBoosterCount(type);
            if (current < amount) return false;

            SetBoosterCount(type, current - amount);
            EventManager.EmitEvent(EventVariables.UseItem, (int)type);
            return true;
        }

        public void SetHeartCount(int amount)
        {
            EnsureLoaded();

            amount = Mathf.Clamp(amount, 0, MaxHeartCount);
            int previous = m_userData.heartCount;
            if (previous == amount) return;

            m_userData.heartCount = amount;
            NotifyResourceChanged(UserResourceType.Heart, previous, amount);
            Save();
        }

        public bool TrySpendHeart(int amount = 1)
        {
            if (amount <= 0) return true;
            if (HeartCount < amount) return false;

            SetHeartCount(HeartCount - amount);
            return true;
        }

        public void SetNextHeartRefillUtcTicks(long ticks)
        {
            EnsureLoaded();
            if (m_userData.nextHeartRefillUtcTicks == ticks) return;

            m_userData.nextHeartRefillUtcTicks = ticks;
            Save();
            OnResourcesChanged?.Invoke();
        }

        public void SetSoundEnabled(bool value)
        {
            EnsureLoaded();
            if (m_userData.soundEnabled == value) return;

            m_userData.soundEnabled = value;
            Save();
            EventManager.EmitEvent(EventVariables.ChangeSound, value);
        }

        public void SetMusicEnabled(bool value)
        {
            EnsureLoaded();
            if (m_userData.musicEnabled == value) return;

            m_userData.musicEnabled = value;
            Save();
            EventManager.EmitEvent(EventVariables.ChangeMusic, value);
        }

        public void SetVibrationEnabled(bool value)
        {
            EnsureLoaded();
            if (m_userData.vibrationEnabled == value) return;

            m_userData.vibrationEnabled = value;
            Save();
        }

        private void SetCoins(int amount)
        {
            EnsureLoaded();

            amount = Mathf.Max(0, amount);
            int previous = m_userData.coins;
            if (previous == amount) return;

            m_userData.coins = amount;
            NotifyResourceChanged(UserResourceType.Coin, previous, amount);
            Save();
        }

        private void EnsureLoaded()
        {
            if (m_userData == null) Load();
        }

        private void NotifyResourceChanged(UserResourceType type, int previous, int current)
        {
            OnResourceChanged?.Invoke(type, previous, current);
            OnResourcesChanged?.Invoke();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) Save();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) Save();
        }
    }
}
