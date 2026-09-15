using UnityEngine;
using HoneyBeeRush.Bees;
using HoneyBeeRush.Core;
using HoneyBeeRush.Data;
using HoneyBeeRush.Gameplay.Core;
using Lean.Pool;

namespace HoneyBeeRush.Gameplay.Runtime
{
    public sealed partial class LevelController
    {
        private void BuildSession(LevelData level)
        {
            if (m_camera == null) m_camera = Camera.main;

            BuildEnvironmentOnce();

            int slotCount = Tuning != null
                ? Tuning.ResolveSlotCount(level.launchSlots)
                : Mathf.Max(1, level.launchSlots);

            m_board.ConfigureView(Tuning != null ? Tuning.BoardView : BoardViewSettings.Default);
            m_board.SetViewForward(m_camera != null ? m_camera.transform.forward : Vector3.forward);
            m_board.Build(level, m_colorMaterials, Layout);
            m_gridBoard.Build(level, m_colorMaterials, Layout, HasFreeSlot, OnCrateTapped);
            m_slots.Build(slotCount, Layout);

            m_honey = new HoneyMeter();
            m_honey.Reset(m_board.TotalNectarInitial);

            m_bees = new BeeSim(m_board, m_beeConfig);
            m_bees.CellWorldPosition = m_board.CellWorldPosition;
            m_bees.SetFlightPlane(Layout.beeCruiseZ);
            m_bees.SetHive(m_hiveView.EntrancePosition(Layout), m_hiveView.EntranceForward());

            m_beeSwarm.Init(m_bees, m_colorMaterials, Layout);
            if (m_hiveView != null) m_hiveView.StopDance();

            SubscribeSession();
            PublishHoney();
        }

        private void BuildEnvironmentOnce()
        {
            if (m_environmentBuilt) return;

            if (m_envView != null) m_envView.Build(Layout, m_camera);
            if (m_hiveView != null) m_hiveView.Build(Layout);

            m_environmentBuilt = true;
        }

        private void SubscribeSession()
        {
            m_board.CellDied += OnCellDied;
            m_board.CellUnlocked += OnCellUnlocked;

            m_bees.DrainStarted += OnDrainStarted;
            m_bees.DrainCompleted += OnDrainCompleted;
            m_bees.Returned += OnBeeReturned;

            m_honey.Changed += OnHoneyChanged;
        }

        private void UnsubscribeSession()
        {
            if (m_board != null)
            {
                m_board.CellDied -= OnCellDied;
                m_board.CellUnlocked -= OnCellUnlocked;
            }

            if (m_bees != null)
            {
                m_bees.DrainStarted -= OnDrainStarted;
                m_bees.DrainCompleted -= OnDrainCompleted;
                m_bees.Returned -= OnBeeReturned;
            }

            if (m_honey != null) m_honey.Changed -= OnHoneyChanged;
        }

        private void TearDownSession()
        {
            UnsubscribeSession();
            EndPointer();

            if (m_hiveView != null) m_hiveView.StopDance();
            if (m_bees != null) m_bees.ClearAll();
            if (m_beeSwarm != null) m_beeSwarm.ClearSwarm();
            if (m_slots != null) m_slots.ClearSlots();
            if (m_gridBoard != null) m_gridBoard.ClearBoard();
            if (m_board != null) m_board.ClearBoard();

            m_bees = null;
            m_honey = null;
        }

        private bool HasFreeSlot() => m_slots != null && m_slots.FreeCount > 0;

        public bool CanShuffle()
        {
            if (m_phase != GamePhase.Playing || m_gridBoard == null) return false;

            var eligible = m_gridBoard.GetEligibleShuffleCrates();
            return eligible != null && eligible.Count > 1;
        }

        public void ApplyShuffleBooster()
        {
            if (!CanShuffle()) return;

            m_gridBoard.ApplyPhysicalShuffle(m_gridBoard.GetEligibleShuffleCrates());
            m_loseTimer = 0f;
        }

        private void ProcessSpawners(float dt)
        {
            if (m_slots == null || m_bees == null) return;

            float interval = Tuning != null ? Tuning.BeeSpawnInterval : 0.18f;
            var slots = m_slots.Slots;

            for (int i = 0; i < slots.Count; i++)
            {
                SlotController slot = slots[i];
                if (slot == null || !slot.IsOccupied || slot.ToSpawn <= 0) continue;

                CrateController crate = slot.CurrentCrate;
                if (crate == null) continue;

                if (!slot.ArrivedInSlot)
                {
                    if (crate.IsTravelling) continue;
                    slot.BeginSpawning();
                }

                ColorType colorType = crate.ColorType;
                int nectarLeft = m_board != null ? m_board.RemainingNectarOfColor(colorType) : 0;

                if (nectarLeft <= 0)
                {
                    slot.DiscardRemainingSpawns();
                    continue;
                }

                int collectable = m_board.CountTargetable(colorType);
                int pending = m_bees.PendingDrainCount(colorType);

                slot.IsClogged = collectable <= 0;
                slot.IsThrottled = !slot.IsClogged && pending >= collectable;

                if (slot.IsClogged || slot.IsThrottled)
                {
                    slot.SpawnTimer = 0f;
                    slot.LidWaitTimer += dt;
                    if (slot.LidWaitTimer >= crate.LidCloseDelay) crate.SetLidOpen(false);
                    continue;
                }

                slot.LidWaitTimer = 0f;
                crate.SetLidOpen(true);

                if (!crate.IsLidReadyForRelease)
                {
                    slot.SpawnTimer = 0f;
                    continue;
                }

                slot.SpawnTimer -= dt;

                int guard = 0;
                while (slot.SpawnTimer <= 0f && slot.ToSpawn > 0 && guard < 16)
                {
                    if (pending >= collectable) break;

                    m_bees.Spawn(crate.BeeSpawnPosition(slot.SpawnWorldPos), slot.SlotIndex, colorType,
                        crate.BeeSpawnScatter, crate.BeeGrowDuration);
                    slot.MarkSpawned();
                    pending++;
                    slot.SpawnTimer += interval;
                    guard++;
                }
            }
        }

        private void CloseAllLids()
        {
            if (m_slots == null) return;

            var slots = m_slots.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                SlotController slot = slots[i];
                if (slot == null || slot.CurrentCrate == null) continue;

                slot.CurrentCrate.SetLidOpen(false);
            }
        }

        private void FreeFinishedSlots()
        {
            if (m_slots == null) return;

            var slots = m_slots.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                SlotController slot = slots[i];
                if (slot == null || !slot.IsOccupied) continue;
                if (slot.ToSpawn != 0) continue;

                CrateController crate = slot.CurrentCrate;
                if (crate != null && crate.IsTravelling) continue;

                slot.Free();

                if (crate == null) continue;

                CrateController captured = crate;
                captured.PlayDestroy(() =>
                {
                    if (captured != null && captured.gameObject.activeSelf) LeanPool.Despawn(captured.gameObject);
                });

                Common.EventManager.EmitEvent(Common.EventVariables.SlotFreed, slot.SlotIndex);
            }
        }

        private void RefreshCloggedFlags()
        {
            if (m_slots == null || m_board == null) return;

            var slots = m_slots.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                SlotController slot = slots[i];
                if (slot == null) continue;

                if (!slot.IsOccupied)
                {
                    slot.IsClogged = false;
                    slot.IsThrottled = false;
                    continue;
                }

                ColorType colorType = slot.CurrentCrate.ColorType;
                int collectable = m_board.CountTargetable(colorType);
                slot.IsClogged = collectable <= 0;
                slot.IsThrottled = !slot.IsClogged && m_bees != null &&
                                   m_bees.PendingDrainCount(colorType) >= collectable;
            }
        }
    }
}
