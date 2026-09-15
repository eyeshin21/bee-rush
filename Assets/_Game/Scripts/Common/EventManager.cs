using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoneyBeeRush.Common
{
    public static class EventManager
    {
        private sealed class EventEntry
        {
            public Action Callbacks;
            public int IntData;
            public float FloatData;
            public bool BoolData;
            public string StringData;
            public object ObjectData;
        }

        private static readonly Dictionary<string, EventEntry> s_events = new Dictionary<string, EventEntry>();
        private static readonly List<Action> s_dispatchBuffer = new List<Action>(8);

        public static void StartListening(string eventName, Action callback)
        {
            if (string.IsNullOrEmpty(eventName) || callback == null) return;

            EventEntry entry = GetOrCreate(eventName);
            entry.Callbacks -= callback;
            entry.Callbacks += callback;
        }

        public static void StopListening(string eventName, Action callback)
        {
            if (string.IsNullOrEmpty(eventName) || callback == null) return;
            if (!s_events.TryGetValue(eventName, out EventEntry entry)) return;

            entry.Callbacks -= callback;
        }

        public static void EmitEvent(string eventName)
        {
            if (string.IsNullOrEmpty(eventName)) return;
            if (!s_events.TryGetValue(eventName, out EventEntry entry)) return;
            if (entry.Callbacks == null) return;

            Delegate[] targets = entry.Callbacks.GetInvocationList();

            s_dispatchBuffer.Clear();
            for (int i = 0; i < targets.Length; i++) s_dispatchBuffer.Add((Action)targets[i]);

            for (int i = 0; i < s_dispatchBuffer.Count; i++)
            {
                try
                {
                    s_dispatchBuffer[i].Invoke();
                }
                catch (Exception exception)
                {
                    Debug.LogError("[EventManager] Listener of '" + eventName + "' threw: " + exception);
                }
            }

            s_dispatchBuffer.Clear();
        }

        public static void EmitEvent(string eventName, int data)
        {
            GetOrCreate(eventName).IntData = data;
            EmitEvent(eventName);
        }

        public static void EmitEvent(string eventName, bool data)
        {
            GetOrCreate(eventName).BoolData = data;
            EmitEvent(eventName);
        }

        public static void EmitEvent(string eventName, string data)
        {
            GetOrCreate(eventName).StringData = data;
            EmitEvent(eventName);
        }

        public static void SetData(string eventName, int data) => GetOrCreate(eventName).IntData = data;

        public static void SetData(string eventName, float data) => GetOrCreate(eventName).FloatData = data;

        public static void SetData(string eventName, bool data) => GetOrCreate(eventName).BoolData = data;

        public static void SetData(string eventName, string data) => GetOrCreate(eventName).StringData = data;

        public static void SetData(string eventName, object data) => GetOrCreate(eventName).ObjectData = data;

        public static int GetInt(string eventName) =>
            s_events.TryGetValue(eventName, out EventEntry entry) ? entry.IntData : 0;

        public static float GetFloat(string eventName) =>
            s_events.TryGetValue(eventName, out EventEntry entry) ? entry.FloatData : 0f;

        public static bool GetBool(string eventName) =>
            s_events.TryGetValue(eventName, out EventEntry entry) && entry.BoolData;

        public static string GetString(string eventName) =>
            s_events.TryGetValue(eventName, out EventEntry entry) ? entry.StringData : string.Empty;

        public static T GetObject<T>(string eventName) where T : class =>
            s_events.TryGetValue(eventName, out EventEntry entry) ? entry.ObjectData as T : null;

        public static void ClearAll()
        {
            s_events.Clear();
            s_dispatchBuffer.Clear();
        }

        private static EventEntry GetOrCreate(string eventName)
        {
            if (!s_events.TryGetValue(eventName, out EventEntry entry))
            {
                entry = new EventEntry();
                s_events[eventName] = entry;
            }
            return entry;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnDomainReload() => ClearAll();
    }
}
