using System;
using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using HoneyBeeRush.Data;
using Lean.Pool;
using UnityEngine;

namespace HoneyBeeRush.Gameplay.Core
{
    public sealed class GridCellController : MonoBehaviour, IPoolable
    {
        [SerializeField] private CrateController m_cratePrefab;
        [SerializeField] private MeshRenderer m_padRenderer;
        [SerializeField] private SerializedDictionary<BlockGridBorderType, GameObject> m_listBorder;
        [SerializeField] private SerializedDictionary<BlockGridCornerType, GameObject> m_listCorner;
        [SerializeField] private Transform m_pieceRoot;

        private readonly List<GameObject> m_activePieces = new List<GameObject>();

        private GridPosition m_gridPosition;
        private GridCellBoardData m_boardData;
        private GridCellDefinition m_definition;
        private CrateController m_crateController;

        public GridPosition GridPosition => m_gridPosition;
        public CrateController CrateController => m_crateController;
        public GridCellBoardData BoardData => m_boardData;
        public GridCellDefinition Definition => m_definition;
        public bool IsDeadCell => m_definition != null && m_definition.cellType == GridCellType.DeadCell;

        public void Initialize(GridPosition position, GridCellDefinition cellDefinition, GridCellBoardData boardData, ColorMaterialMapping materials, Action<CrateController> onTapped, int nextCrateId)
        {
            m_gridPosition = position;
            m_definition = cellDefinition;
            m_boardData = boardData;

            if (cellDefinition != null && cellDefinition.crate != null && cellDefinition.cellType != GridCellType.Empty && cellDefinition.cellType != GridCellType.DeadCell)
            {
                EnsureCrateController();
                m_crateController.Initialize(nextCrateId, cellDefinition.crate, position.Column, position.Row, materials);
                m_crateController.SetGridPosition(position);
                if (onTapped != null)
                {
                    m_crateController.Tapped += onTapped;
                }
            }
            else if (m_crateController != null)
            {
                m_crateController.gameObject.SetActive(false);
            }

            CheckBorder();
        }

        public void SetCrateController(CrateController newCrate)
        {
            m_crateController = newCrate;
            if (m_crateController != null)
            {
                m_crateController.transform.SetParent(transform, true);
                m_crateController.SetGridPosition(m_gridPosition);
            }
        }

        public void DetachCrateController()
        {
            m_crateController = null;
        }

        private void EnsureCrateController()
        {
            if (m_crateController != null || m_cratePrefab == null)
            {
                return;
            }

            m_crateController = LeanPool.Spawn(m_cratePrefab, transform);
            m_crateController.transform.localPosition = Vector3.zero;
        }

        private void CheckBorder()
        {
            ClearPieces();

            bool dead = IsDeadCell;
            if (m_padRenderer != null) m_padRenderer.enabled = !dead;

            if (!dead || m_boardData == null)
            {
                return;
            }

            int col = m_gridPosition.Column;
            int row = m_gridPosition.Row;

            bool checkLeft = col - 1 < 0 || !m_boardData.IsDeadCell(new GridPosition(row, col - 1));
            bool checkRight = col + 1 >= m_boardData.width || !m_boardData.IsDeadCell(new GridPosition(row, col + 1));
            bool checkBot = row + 1 >= m_boardData.height || !m_boardData.IsDeadCell(new GridPosition(row + 1, col));
            bool checkTop = row - 1 < 0 || !m_boardData.IsDeadCell(new GridPosition(row - 1, col));

            bool checkTopLeft = row - 1 < 0 || col - 1 < 0
                || !m_boardData.IsDeadCell(new GridPosition(row - 1, col - 1));
            bool checkTopRight = row - 1 < 0 || col + 1 >= m_boardData.width
                || !m_boardData.IsDeadCell(new GridPosition(row - 1, col + 1));
            bool checkBotLeft = row + 1 >= m_boardData.height || col - 1 < 0
                || !m_boardData.IsDeadCell(new GridPosition(row + 1, col - 1));
            bool checkBotRight = row + 1 >= m_boardData.height || col + 1 >= m_boardData.width
                || !m_boardData.IsDeadCell(new GridPosition(row + 1, col + 1));

            SetBorder(ResolveBorder(checkLeft, checkRight, checkTop, checkBot));

            if (!checkTop && !checkLeft && checkTopLeft)
            {
                SetCorner(BlockGridCornerType.TopLeftOut);
            }

            if (!checkTop && !checkRight && checkTopRight)
            {
                SetCorner(BlockGridCornerType.TopRightOut);
            }

            if (!checkBot && !checkRight && checkBotRight)
            {
                SetCorner(BlockGridCornerType.BottomRightOut);
            }

            if (!checkBot && !checkLeft && checkBotLeft)
            {
                SetCorner(BlockGridCornerType.BottomLeftOut);
            }
        }

        private static BlockGridBorderType ResolveBorder(bool checkLeft, bool checkRight, bool checkTop, bool checkBot)
        {
            if (checkLeft && checkRight && checkTop && checkBot) return BlockGridBorderType.ClosedBorder;
            if (!checkLeft && !checkRight && !checkTop && !checkBot) return BlockGridBorderType.Wall;
            if (!checkLeft && checkRight && checkTop && checkBot) return BlockGridBorderType.TopRightBottom;
            if (checkLeft && !checkRight && checkTop && checkBot) return BlockGridBorderType.BottomLeftTop;
            if (checkLeft && checkRight && !checkTop && checkBot) return BlockGridBorderType.RightBottomLeft;
            if (checkLeft && checkRight && checkTop && !checkBot) return BlockGridBorderType.LeftTopRight;
            if (!checkLeft && checkRight && !checkTop && checkBot) return BlockGridBorderType.BottomRight;
            if (checkLeft && !checkRight && !checkTop && checkBot) return BlockGridBorderType.BottomLeft;
            if (!checkLeft && checkRight && checkTop && !checkBot) return BlockGridBorderType.TopRight;
            if (checkLeft && !checkRight && checkTop && !checkBot) return BlockGridBorderType.TopLeft;
            if (!checkLeft && !checkRight && checkTop && checkBot) return BlockGridBorderType.TopBottom;
            if (checkLeft && checkRight && !checkTop && !checkBot) return BlockGridBorderType.LeftRight;
            if (checkLeft) return BlockGridBorderType.Left;
            if (checkRight) return BlockGridBorderType.Right;
            if (checkTop) return BlockGridBorderType.Top;
            return BlockGridBorderType.Bottom;
        }

        private void ClearPieces()
        {
            for (int i = 0; i < m_activePieces.Count; i++)
            {
                if (m_activePieces[i] != null)
                {
                    LeanPool.Despawn(m_activePieces[i]);
                }
            }

            m_activePieces.Clear();
        }

        private void SetBorder(BlockGridBorderType borderType)
        {
            GameObject prefab;
            if (m_listBorder == null || !m_listBorder.TryGetValue(borderType, out prefab) || prefab == null)
            {
                return;
            }

            SpawnPiece(prefab);
        }

        private void SetCorner(BlockGridCornerType cornerType)
        {
            GameObject prefab;
            if (m_listCorner == null || !m_listCorner.TryGetValue(cornerType, out prefab) || prefab == null)
            {
                return;
            }

            SpawnPiece(prefab);
        }

        private void SpawnPiece(GameObject prefab)
        {
            Transform parent = m_pieceRoot != null ? m_pieceRoot : transform;
            GameObject piece = LeanPool.Spawn(prefab, parent);
            piece.transform.localPosition = prefab.transform.localPosition;
            piece.transform.localRotation = prefab.transform.localRotation;
            piece.transform.localScale = prefab.transform.localScale;
            m_activePieces.Add(piece);
        }

        public void Clear()
        {
            if (m_crateController != null)
            {
                m_crateController.transform.SetParent(null, false);
                LeanPool.Despawn(m_crateController.gameObject);
                m_crateController = null;
            }
        }

        public void OnSpawn()
        {
            gameObject.SetActive(true);
        }

        public void OnDespawn()
        {
            Clear();
            ClearPieces();
            if (m_padRenderer != null) m_padRenderer.enabled = true;
            m_definition = null;
            m_boardData = null;
        }

        private void OnDisable()
        {
            StopAllCoroutines();
        }
    }
}
