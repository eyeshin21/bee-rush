using System.Collections.Generic;
using UnityEngine;
using HoneyBeeRush.Core;
using HoneyBeeRush.Gameplay.Core;
using HoneyBeeRush.Managers;
using Lean.Pool;

namespace HoneyBeeRush.Gameplay.Runtime
{
    public sealed partial class LevelController
    {
        private void EvaluateOutcomes(float dt)
        {
            if (m_sessionEnded) return;

            if (m_board.IsCleared)
            {
                EnterWon();
                return;
            }

            if (m_bees != null && m_bees.AliveCount > 0)
            {
                m_loseTimer = 0f;
                return;
            }

            if (CongestionService.IsLost(m_board, m_gridBoard, m_slots))
            {
                m_loseTimer += dt;

                float threshold = Tuning != null ? Tuning.LoseDetectDelay : 2.5f;
                if (m_loseTimer >= threshold) EnterLost();
                return;
            }

            m_loseTimer = 0f;
        }

        private void EnterWon()
        {
            if (m_phase == GamePhase.Won) return;

            m_phase = GamePhase.Won;
            CloseAllLids();
            m_loseTimer = 0f;
            m_winRaised = false;
            m_winDelay = Tuning != null ? Tuning.WinSettleDelay : 0.95f;

            if (m_bees != null) m_bees.SetFinale(true);
            if (m_hiveView != null) m_hiveView.StartDance();

            if (UserManager.HasInstance && m_honey != null)
            {
                UserManager.Instance.ReportHoneyPercent(m_honey.Percent);
            }
        }

        private void TickWinDelay(float dt)
        {
            if (m_winRaised) return;

            m_winDelay -= dt;
            if (m_winDelay > 0f) return;

            m_winRaised = true;
            RaiseSessionEnded(true);
        }

        private void EnterLost()
        {
            if (m_phase == GamePhase.Lost) return;

            m_phase = GamePhase.Lost;
            CloseAllLids();
            m_loseTimer = 0f;
            RaiseSessionEnded(false);
        }

        private void RaiseSessionEnded(bool win)
        {
            if (m_sessionEnded) return;

            m_sessionEnded = true;
            m_inputLocked = true;

            OnSessionEnded?.Invoke(win);
        }

        public void ReopenAfterRevive(int clearedCrates)
        {
            if (m_phase != GamePhase.Lost) return;

            m_sessionEnded = false;
            m_inputLocked = false;
            m_loseTimer = 0f;
            m_phase = GamePhase.Playing;

            ClearCratesForRevive(clearedCrates);
        }

        private void ClearCratesForRevive(int count)
        {
            if (count <= 0 || m_gridBoard == null) return;

            List<CrateController> eligible = m_gridBoard.GetEligibleShuffleCrates();
            if (eligible == null || eligible.Count == 0) return;

            int cleared = Mathf.Min(count, eligible.Count);
            for (int i = 0; i < cleared; i++)
            {
                CrateController crate = eligible[i];
                if (crate == null) continue;

                m_gridBoard.ForceMarkCrateAsInSlot(crate.CrateId);

                CrateController captured = crate;
                captured.PlayDestroy(() =>
                {
                    if (captured != null) LeanPool.Despawn(captured.gameObject);
                });
            }

            m_gridBoard.RefreshPickableCrates();
        }

        private void OnDrainStarted(int cellId)
        {
            CellController cell = m_board != null ? m_board.CellById(cellId) : null;
            if (cell != null) cell.PlayDrainPulse();
        }

        private void OnDrainCompleted(int cellId)
        {
            if (m_board != null) m_board.ApplyDrain(cellId);
        }

        private void OnBeeReturned(int slotIndex, bool hadCargo)
        {
            SlotController slot = m_slots != null ? m_slots.SlotByIndex(slotIndex) : null;
            if (slot != null) slot.OnBeeReturned(hadCargo);

            if (!hadCargo || m_honey == null) return;

            m_honey.AddLoad(1);

            if (m_hiveView == null) return;
            m_hiveView.Bounce();
            m_hiveView.SplashHoney(m_hiveView.EntrancePosition(Layout));
        }

        private void OnCellDied(CellController cell)
        {
        }

        private void OnCellUnlocked(CellController cell)
        {
        }

        private void OnHoneyChanged() => PublishHoney();

        private void PublishHoney()
        {
            if (m_honey == null) return;

            Common.EventManager.SetData(Common.EventVariables.HoneyChanged, m_honey.Percent);
            Common.EventManager.EmitEvent(Common.EventVariables.HoneyChanged);
        }
    }
}
