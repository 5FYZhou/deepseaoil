using System;
using DeepseaOil.Logic.Combat;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Presentation.Grid;
using NUnit.Framework;
using UnityEngine;
using cfg.dso;

namespace DeepseaOil.Tests
{
    /// <summary>电格常驻粒子的带电判据、借还池契约与格心落点</summary>
    /// <remarks>不拉场景：几何直接构造；预制体缺失时由 DeepseaOil ▸ 特效 菜单烘焙。</remarks>
    [Category("Presentation")]
    public sealed class 电格常驻粒子Tests
    {
        private const string PrefabKey = "effects/Vfx_Tile_Electricity";

        [Test]
        public void 带电状态_判据必须与元素表的Electric一致()
        {
            TileStateType[] charged =
            {
                TileStateType.BasicElectricity,
                TileStateType.ConductZone,
                TileStateType.ChargedThorn,
            };

            for (int i = 0; i < charged.Length; i++)
            {
                Assert.IsTrue(TileVfxDirector.IsChargedState(charged[i]), $"{charged[i]} 必须出常驻粒子");
            }

            TileStateType[] silent =
            {
                TileStateType.Normal,
                TileStateType.Mud,
                TileStateType.BasicFire,
                TileStateType.FlameField,
                TileStateType.Freeze,
            };

            for (int i = 0; i < silent.Length; i++)
            {
                Assert.IsFalse(TileVfxDirector.IsChargedState(silent[i]), $"{silent[i]} 不该出电格粒子");
            }

            foreach (TileStateType state in Enum.GetValues(typeof(TileStateType)))
            {
                bool chargedByElement = Damage.ElementFromTile(state) == ElementKind.Electric;

                Assert.AreEqual(chargedByElement, TileVfxDirector.IsChargedState(state),
                    $"{state} 的带电判据与元素表不一致：元素表新增带电地貌后必须同步 TileVfxDirector.IsChargedState");
            }
        }

        [Test]
        public void 池契约_未装配时零活动粒子且清空幂等()
        {
            var go = new GameObject("TestTileVfxDirector");
            var director = go.AddComponent<TileVfxDirector>();

            try
            {
                Assert.AreEqual(0, director.ActiveVfxCount, "未 Attach 时不该有借出的粒子");

                director.ClearAll();
                director.ClearAll();

                Assert.AreEqual(0, director.ActiveVfxCount, "ClearAll 必须幂等且把活动粒子归零");
            }
            finally
            {
                director.Detach();
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void 调度_进入电格借出粒子离开即刻归还()
        {
            if (Resources.Load<GameObject>(PrefabKey) == null)
            {
                Assert.Ignore($"未生成 {PrefabKey}：先跑 DeepseaOil ▸ 特效 ▸ 一键构建单对象球状闪电与场景接线");
            }

            var go = new GameObject("TestTileVfxDirector");
            var director = go.AddComponent<TileVfxDirector>();

            try
            {
                var cell = new Vector3Int(2, 3, 0);
                var geometry = new GridGeometry(Vector2.zero, 1f);

                director.Initialize(in geometry);
                director.Attach();

                EventBus<TileStateChanged>.Publish(
                    new TileStateChanged(cell, TileStateType.Normal, TileStateType.BasicElectricity));

                Assert.AreEqual(1, director.ActiveVfxCount, "带电格必须借出一件常驻粒子");
                Assert.AreEqual(1, go.transform.childCount, "借出的粒子必须挂在管理器节点下");

                Transform vfx = go.transform.GetChild(0);
                Vector2 center = geometry.CellCenter(cell);

                Assert.AreEqual(center.x, vfx.position.x, 1e-3f, "粒子必须落在格心 X");
                Assert.AreEqual(center.y, vfx.position.y, 1e-3f, "粒子必须落在格心 Y");

                EventBus<TileStateChanged>.Publish(
                    new TileStateChanged(cell, TileStateType.BasicElectricity, TileStateType.Normal));

                Assert.AreEqual(0, director.ActiveVfxCount, "回到普通格必须立刻归还");
            }
            finally
            {
                director.Detach();
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
