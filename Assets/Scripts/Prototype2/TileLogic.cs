using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace DeepseaOil.Prototype2
{
    public sealed class TileLogic : MonoBehaviour
    {
        [SerializeField]
        private Tilemap tilemap; 
        [SerializeField]
        private Tilemap showEffrctTilemap;

        [SerializeField]
        private TileBase slowTile;

        [SerializeField]
        private EnemyDirector enemyDirector;

        private Vector3Int _aimCell;
        private bool _hasAimCell;
        private TileBase _aimOriginalTile;

        // 当前正在减速的 Tile，以及它原本是什么 Tile
        private readonly Dictionary<Vector3Int, TileBase> _changedTiles = new();

        /// <summary>
        /// 球落地时调用。
        /// </summary>
        public void OnBallHit(Vector2 worldPosition, BallType type)
        {
            if (type != BallType.Water)
                return;

            Vector3Int cell = tilemap.WorldToCell(worldPosition);

            TileBase originalTile = tilemap.GetTile(cell);

            if (originalTile == null)
                return;

            // TODO: 临时机制：
            // 有球落到地块上，对该地块上的敌人造成 1 点伤害。
            DamageEnemiesOnTile(cell);

            // 暂定
            // 如果这个 Tile 已经处于减速状态
            if (_changedTiles.ContainsKey(cell))
                return;

            // 记录原 Tile
            _changedTiles[cell] = originalTile;

            // showEffrctTilemap替换成减速 Tile
            showEffrctTilemap.SetTile(cell, slowTile);

            // 开始计时恢复
            StartCoroutine(RestoreTile(cell));
        }

        /// <summary>
        /// TODO: 临时机制。
        /// 对当前站在指定地块上的敌人造成 1 点伤害。
        /// </summary>
        private void DamageEnemiesOnTile(Vector3Int cell)
        {
            if (enemyDirector == null)
            {
                enemyDirector = FindObjectOfType<EnemyDirector>();
            }

            enemyDirector.DamageEnemiesOnCell(cell, 1);
        }

        private IEnumerator RestoreTile(Vector3Int cell)
        {
            yield return new WaitForSeconds(
                ThrowConstants.MUD_DURATION
            );

            if (_changedTiles.TryGetValue(cell, out TileBase originalTile))
            {
                showEffrctTilemap.SetTile(cell, originalTile);
                _changedTiles.Remove(cell);
            }
        }

        public float GetSlowMultiplier(Vector2 worldPosition)
        {
            Vector3Int cell = tilemap.WorldToCell(worldPosition);

            if (_changedTiles.ContainsKey(cell))
            {
                return ThrowConstants.MUD_SLOW_FACTOR;
            }

            return 1f;
        }

        public Vector3Int WorldToCell(Vector2 worldPosition)
        {
            return tilemap.WorldToCell(worldPosition);
        }

        /// <summary>
        /// 设置当前瞄准的地块。
        /// </summary>
        public void SetAimCell(Vector3Int cell)
        {
            if (_hasAimCell && cell == _aimCell)
                return;

            ClearAim();

            _aimCell = cell;
            _hasAimCell = true;

            _aimOriginalTile = tilemap.GetTile(cell);

            if (_aimOriginalTile != null)
            {
                tilemap.SetColor(cell, Color.yellow);
            }
        }


        public void ClearAim()
        {
            if (!_hasAimCell)
                return;

            if (_aimOriginalTile != null)
            {
                tilemap.SetColor(_aimCell, Color.white);
            }

            _hasAimCell = false;
            _aimOriginalTile = null;
        }

        public Vector2 GetAimPoint()
        {
            if (!_hasAimCell)
                return Vector2.zero;

            return tilemap.GetCellCenterWorld(_aimCell);
        }

        public bool TryGetAimPoint(
    Vector2 origin,
    Vector2 mouseWorldPosition,
    float maxDistance,
    out Vector2 aimPoint)
        {
            Vector2 direction = mouseWorldPosition - origin;

            if (direction.sqrMagnitude < 0.0001f)
            {
                aimPoint = origin;
                ClearAim();
                return false;
            }

            direction.Normalize();

            // 先尝试鼠标所在 Tile
            Vector3Int mouseCell = tilemap.WorldToCell(mouseWorldPosition);
            Vector2 mouseTileCenter = tilemap.GetCellCenterWorld(mouseCell);

            if (Vector2.Distance(origin, mouseTileCenter) <= maxDistance)
            {
                aimPoint = mouseTileCenter;
                SetAimCell(mouseCell);
                return true;
            }

            // 鼠标超出范围，沿玩家 -> 鼠标方向寻找最远 Tile
            const float step = 0.1f;

            for (float distance = maxDistance; distance >= 0f; distance -= step)
            {
                Vector2 point = origin + direction * distance;

                Vector3Int cell = tilemap.WorldToCell(point);
                Vector2 tileCenter = tilemap.GetCellCenterWorld(cell);

                if (Vector2.Distance(origin, tileCenter) <= maxDistance)
                {
                    aimPoint = tileCenter;
                    SetAimCell(cell);
                    return true;
                }
            }

            aimPoint = origin;
            ClearAim();
            return false;
        }
    }
}