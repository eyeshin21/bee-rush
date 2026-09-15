using System.Collections.Generic;
using HoneyBeeRush.Core;
using HoneyBeeRush.Data;
using HoneyBeeRush.Gameplay.Core;
using HoneyBeeRush.Gameplay.Runtime;
using UnityEngine;

namespace HoneyBeeRush.Gameplay
{
    [RequireComponent(typeof(LevelController))]
    public sealed class AutoPlayer : MonoBehaviour
    {
        public float firstMoveDelay = 1.35f;
        public float decisionInterval = 0.32f;
        public bool reserveSlotForStarvedColor = true;

        private LevelController _loop;
        private float _timer;
        private readonly Dictionary<ColorType, int> _inFlightByColor = new Dictionary<ColorType, int>(16);

        private void Awake()
        {
            _loop = GetComponent<LevelController>();
            _timer = -firstMoveDelay;
        }

        private void Update()
        {
            if (_loop == null || _loop.Phase != GamePhase.Playing) return;

            _timer += Time.deltaTime * Mathf.Max(0f, _loop.GlobalTimeScale);
            if (_timer < decisionInterval) return;
            _timer = 0f;

            if (_loop.Slots.FreeCount <= 0) return;
            CrateController best = ChooseCrate();
            if (best != null) _loop.TryLaunch(best);
        }

        private CrateController ChooseCrate()
        {
            var board = _loop.Board;
            var gridBoard = _loop.GridBoard;
            var slots = _loop.Slots;
            int freeSlots = slots.FreeCount;

            _inFlightByColor.Clear();
            var live = slots.Slots;
            for (int i = 0; i < live.Count; i++)
            {
                if (!live[i].IsOccupied) continue;
                ColorType c = live[i].CurrentCrate.ColorType;
                int had;
                _inFlightByColor.TryGetValue(c, out had);
                _inFlightByColor[c] = had + live[i].ToSpawn + live[i].LiveBees;
            }

            CrateController best = null;
            float bestScore = float.NegativeInfinity;
            CrateController fallback = null;

            var candidates = gridBoard != null ? gridBoard.PickableCrates : null;
            int count = candidates != null ? candidates.Count : 0;
            for (int i = 0; i < count; i++)
            {
                CrateController crate = candidates[i];
                if (crate == null) continue;
                if (!gridBoard.CanLaunch(crate, freeSlots)) continue;

                if (fallback == null) fallback = crate;

                int targetable = board.CountTargetable(crate.ColorType);
                if (targetable <= 0) continue;

                int inFlight;
                _inFlightByColor.TryGetValue(crate.ColorType, out inFlight);
                int headroom = targetable - inFlight;

                float score = headroom * 4f;
                score += Mathf.Min(targetable, 40) * 0.6f;
                score -= inFlight * 0.35f;
                if (headroom <= 0) score -= 60f;

                int remainingNectar = board.RemainingNectarOfColor(crate.ColorType);
                if (remainingNectar <= 0) score -= 500f;

                if (reserveSlotForStarvedColor && freeSlots == 1 && headroom <= 0) score -= 200f;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = crate;
                }
            }

            if (best != null) return best;

            if (fallback != null && AllSlotsIdleOrStarved()) return fallback;
            return null;
        }

        private bool AllSlotsIdleOrStarved()
        {
            var slots = _loop.Slots.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].IsOccupied && !slots[i].IsClogged) return false;
            }
            return true;
        }
    }
}
