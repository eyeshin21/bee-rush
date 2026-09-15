using System.Collections.Generic;
using HoneyBeeRush.Gameplay.Core;
using HoneyBeeRush.Gameplay.Domain;

namespace HoneyBeeRush.Gameplay
{
    public static class CongestionService
    {
        public static bool IsLost(BoardController board, GridBoardController gridBoard, SlotBoardController slots)
        {
            if (board == null || gridBoard == null || slots == null) return false;
            if (board.IsCleared) return false;

            int idle = IdleSlotCount(slots);
            if (idle < 0) return false;

            if (gridBoard.NoCratesInColumns) return true;
            if (!gridBoard.AnyPickableLaunchable(idle)) return true;
            if (!gridBoard.AnyProductive(board)) return true;
            return false;
        }

        public static bool IsLost(BoardController board, CrateQueueController queue, SlotBoardController slots)
        {
            if (board == null || queue == null || slots == null) return false;
            if (board.IsCleared) return false;

            int idle = IdleSlotCount(slots);
            if (idle < 0) return false;

            if (queue.NoCratesInColumns) return true;
            if (!queue.AnyFrontLaunchable(idle)) return true;
            if (!AnyProductive(board, queue)) return true;
            return false;
        }

        private static int IdleSlotCount(SlotBoardController slots)
        {
            IReadOnlyList<SlotController> list = slots.Slots;
            if (list == null) return slots.SlotCount;

            int idle = 0;
            for (int i = 0; i < list.Count; i++)
            {
                SlotController slot = list[i];
                if (slot == null) continue;
                if (slot.HasWork && !slot.IsClogged) return -1;
                if (slot.IsFree) idle++;
            }
            return idle;
        }

        private static bool AnyProductive(BoardController board, CrateQueueController queue)
        {
            for (int c = 0; c < queue.ColumnCount; c++)
            {
                CrateController front = queue.Front(c);
                if (front == null) continue;
                if (board.HasTargetableColor(front.ColorType)) return true;
            }
            return false;
        }
    }
}
