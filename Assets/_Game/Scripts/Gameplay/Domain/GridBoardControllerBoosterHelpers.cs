using System.Collections.Generic;
using HoneyBeeRush.Gameplay.Core;
using UnityEngine;

namespace HoneyBeeRush.Gameplay.Domain
{
    public sealed partial class GridBoardController
    {
        public List<CrateController> GetEligibleShuffleCrates()
        {
            var result = new List<CrateController>();
            for (int i = 0; i < m_cells.Count; i++)
            {
                var cell = m_cells[i];
                var crate = cell.CrateController;
                if (crate == null || crate.InSlot || crate.RemainingBees <= 0) continue;
                if (crate.Hidden || crate.DelayLocked) continue;
                result.Add(crate);
            }
            return result;
        }

        public void ApplyPhysicalShuffle(List<CrateController> eligible)
        {
            if (eligible == null || eligible.Count <= 1) return;

            var currentCells = new List<GridCellController>(eligible.Count);
            for (int i = 0; i < eligible.Count; i++)
            {
                var crate = eligible[i];
                currentCells.Add(GetCell(crate.GridPosition));
            }

            for (int i = 0; i < currentCells.Count; i++)
            {
                var cell = currentCells[i];
                if (cell != null) cell.DetachCrateController();
            }

            var shuffledCells = new List<GridCellController>(currentCells);
            for (int i = shuffledCells.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var temp = shuffledCells[i];
                shuffledCells[i] = shuffledCells[j];
                shuffledCells[j] = temp;
            }

            for (int i = 0; i < eligible.Count; i++)
            {
                var crate = eligible[i];
                var newCell = shuffledCells[i];

                newCell.SetCrateController(crate);
                crate.SetGridPosition(newCell.GridPosition);
                crate.PlaySlideTo(Vector3.zero, 0.25f);
            }

            RefreshPickableCrates();
        }

        public void ForceMarkCrateAsInSlot(int crateId)
        {
            var crate = CrateById(crateId);
            if (crate != null)
            {
                DetachAndRegister(crate);
            }
        }
    }
}
