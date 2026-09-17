using System;
using UnityEngine;
using HoneyBeeRush.Core;

namespace HoneyBeeRush.View
{
    [Serializable]
    public sealed class LayoutConfig
    {
        public float hexSize = 0.5f;
        public float tileDepth = 0.8227f;
        public float tileGapScale = 0.935f;
        public float layerSpacing = 1f;
        public bool cullEnclosedCells = true;

        public Vector2 boardCentre = new Vector2(0f, 1.15f);
        public float boardFitWidth = 5.75f;
        public float boardFitHeight = 5.75f;
        public float referenceGridScale = 1.25f;
        public float boardRotateFitScale = 1.3f;
        public float boardPanelClearance = 0.1f;

        public float panelSize = 6.95f;
        public float panelHeight;
        public float boardPanelPadding = 0.25f;
        public float panelZ = 0.95f;

        public float slotRowY = -3.05f;
        public float slotZ = -0.55f;
        public float slotSpacing = 1.40f;
        public float slotRadius = 0.478f;

        public float crateRadius = 0.478f;

        public float queueTopY = -4.80f;
        public float queueRowSpacing = 1.22f;
        public float queueColSpacing = 1.42f;
        public float queueZ = -0.50f;
        public int queueVisibleRows = 3;

        public float gridCellPitch = 1.10f;
        public float gridBoardFitWidth = 8.00f;
        public float gridBoardTopY = -3.95f;
        public float gridBoardZ = -0.50f;

        public Vector3 hivePosition = new Vector3(0f, 6.30f, -3.45f);
        public float hiveScale = 2.20f;
        public Vector3 hiveRotation;

        public float beeCruiseZ = -1.62f;
        public float beeScale = 0.324f;

        public float backgroundZ = 9f;
        public float canopyZ = -3.2f;
        public float canopyYOffset;
        public float groundDecorZ = -0.2f;

        public Vector3 SlotPosition(int index, int slotCount)
        {
            float span = (slotCount - 1) * slotSpacing;
            float x = -span * 0.5f + index * slotSpacing;
            return new Vector3(x, slotRowY, slotZ);
        }

        public float PanelWidth => panelSize > 0f ? panelSize : 6.95f;

        public float PanelHeight => panelHeight > 0f ? panelHeight : PanelWidth;

        public float BoardPanelPadding => Mathf.Max(0f, boardPanelPadding);

        public float BoardFitWidth => Mathf.Min(boardFitWidth, PanelWidth - BoardPanelPadding * 2f);

        public float BoardFitHeight => Mathf.Min(boardFitHeight, PanelHeight - BoardPanelPadding * 2f);

        public float CellPitch => HexLayout.Sqrt3 * Mathf.Abs(hexSize);

        public float CellWidth => CellPitch * (tileGapScale > 0f ? tileGapScale : 1f);

        public float CellDepth => tileDepth > 0f ? tileDepth : CellWidth;

        public float LayerSpacing => layerSpacing > 0f ? layerSpacing : 1f;

        public float BoardRotateFitDiameter => Mathf.Min(BoardFitWidth, BoardFitHeight) * (boardRotateFitScale > 0f ? boardRotateFitScale : 1.3f);

        public float BoardPanelClearance => Mathf.Max(0f, boardPanelClearance);

        public float CrateRadius => crateRadius > 0f ? crateRadius : 0.478f;

        public float SlotRadius => slotRadius > 0f ? slotRadius : 0.478f;

        public float BeeLength => beeScale > 0f ? beeScale : 0.324f;

        public float HiveHeight => hiveScale > 0f ? hiveScale : 2.20f;

        public float GridCellPitch => gridCellPitch > 0f ? gridCellPitch : 1.10f;

        public float GridBoardFitWidth => gridBoardFitWidth > 0f ? gridBoardFitWidth : 8.00f;

        public float GridBoardTopY => Mathf.Approximately(gridBoardTopY, 0f) ? -3.95f : gridBoardTopY;

        public float GridBoardZ => Mathf.Approximately(gridBoardZ, 0f) ? -0.50f : gridBoardZ;

        public float GridBoardScale(int columnCount)
        {
            float span = Mathf.Max(1, columnCount) * GridCellPitch;
            return span <= 0f ? 1f : GridBoardFitWidth / span;
        }

        public Vector3 QueuePosition(int column, int columnCount, int depth)
        {
            float span = (columnCount - 1) * queueColSpacing;
            float x = -span * 0.5f + column * queueColSpacing;
            float y = queueTopY - depth * queueRowSpacing;
            float z = queueZ + depth * 0.02f;
            return new Vector3(x, y, z);
        }
    }
}
