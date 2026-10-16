// ---------------------------------------------------------------------------
// 场景碰撞体烘焙 · 运行期测试
//
// 烘焙产的是**到碰撞体的距离**（每格一排排采样点），可通行判据在逻辑层按体半径算；触发器、IManagedActor、
//   IActorMotor、阻挡地貌池、动态体都不算障碍；重复烘焙幂等（SetUp 把格域推到场景碰撞体之外见 SafeOrigin）。
//
// 覆盖边界：绕障/卡死换步的用例在 寻路Tests.cs。
// 跑法：Test Runner ▸ EditMode ▸ Run All
// ---------------------------------------------------------------------------

using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Logic;
using DeepseaOil.Logic.Element;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Logic.Grid.States;
using DeepseaOil.Logic.Movement;
using DeepseaOil.Presentation.Grid;
using NUnit.Framework;
using UnityEngine;
using cfg.dso;

namespace DeepseaOil.Tests
{
    public class 场景碰撞体烘焙Tests
    {
        /// <summary>格域左下角；SetUp 会把它推到场景道具之外，故不可当只读常量用</summary>
        private static Vector2 TestOrigin = new(0.5f, 0.5f);

        private const int Width = 6;
        private const int Height = 6;

        /// <summary>烘焙解析上限：测试一律用 1 格，够判 radius ≤ 0.5 的怪</summary>
        private const float Cap = 1f;

        private readonly List<GameObject> _spawned = new();

        private GridLogic _grid;

        [SetUp]
        public void SetUp()
        {
            TestOrigin = SafeOrigin();
            _grid = NewGrid();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null) Object.DestroyImmediate(_spawned[i]);
            }

            _spawned.Clear();
        }

        /// <summary>把格域推到场景全部 2D 碰撞体之外：EditMode 的 Physics2D 查询是全局的，场景道具会白占格子</summary>
        /// <remarks>必须在造测试件之前调用：只按场景里已存在的碰撞体算边界</remarks>
        private static Vector2 SafeOrigin()
        {
            Collider2D[] existing = Object.FindObjectsOfType<Collider2D>();

            if (existing.Length == 0) return TestOrigin;

            float maxX = existing[0].bounds.max.x;
            float maxY = existing[0].bounds.max.y;

            for (int i = 1; i < existing.Length; i++)
            {
                maxX = Mathf.Max(maxX, existing[i].bounds.max.x);
                maxY = Mathf.Max(maxY, existing[i].bounds.max.y);
            }

            return new Vector2(maxX + 10f, maxY + 10f);
        }

        /// <summary>与 CombatRoot.Assemble 同一条装配：清单与工厂都来自表</summary>
        private static GridLogic NewGrid()
        {
            ReactionResolver.Initialize(ConfigModule.GetElementRules());
            TileChainReactor.Clear();

            var grid = new GridLogic(
                new GridGeometry(TestOrigin, 1f),
                ConfigModule.GetAllTileStates(),
                CreateState,
                ConfigModule.GetDuoReactions());

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++) grid.RegisterCell(new Vector3Int(x, y, 0));
            }

            return grid;
        }

        private static ITileState CreateState(TileStateType id)
        {
            TileStateSpec spec = ConfigModule.TryGetTileState(id);

            return spec != null ? new TableTileState(spec) : null;
        }

        private GameObject NewBox(string name, Vector2 center, Vector2 size, bool trigger = false, float yOffset = 0f)
        {
            var go = new GameObject(name);

            _spawned.Add(go);
            go.transform.position = new Vector3(center.x, center.y, 0f);

            var box = go.AddComponent<BoxCollider2D>();

            box.size = size;
            box.offset = new Vector2(0f, yOffset);
            box.isTrigger = trigger;

            return go;
        }

        /// <summary>置件口：所有造件都按格心定位，避免"格坐标"与"世界坐标"两套口径</summary>
        private Vector2 Center(int x, int y)
        {
            return _grid.Geometry.CellCenter(new Vector3Int(x, y, 0));
        }

        private static void Sync()
        {
            Physics2D.SyncTransforms();
        }

        /// <summary>烘焙口：断言一律读净空值与 IsWalkable</summary>
        private static NavMap Bake(GridLogic grid, float cap)
        {
            NavMap map = SceneObstacleBaker.Bake(grid, cap);

            grid.SetNavMap(map);

            return map;
        }

        /// <summary>净空未达上限的格数：只用来消掉场景折叠道具造成的基准差</summary>
        private static int Affected(NavMap map, float cap)
        {
            int count = 0;

            for (int i = 0; i < map.CellCount; i++)
            {
                if (map.CellClearance(i) < cap) count++;
            }

            return count;
        }

        /// <summary>低于上限的格与净空清单：断言失败时随消息带出，免掉再猜一轮几何</summary>
        private string Clearances(float cap)
        {
            var text = new System.Text.StringBuilder();

            NavMap map = _grid.Nav;

            for (int y = Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < Width; x++)
                {
                    float clearance = map.CellClearance(new Vector3Int(x, y, 0));

                    if (clearance >= cap) continue;

                    text.Append('(').Append(x).Append(',').Append(y).Append(")=").Append(clearance.ToString("F2"));
                }
            }

            return text.ToString();
        }

        /// <summary>格内某个采样点的索引（i 沿 +X、j 沿 +Y）</summary>
        private static int Sample(NavMap map, int x, int y, int i, int j)
        {
            map.TryIndex(new Vector3Int(x, y, 0), out int cellIndex);

            return map.SampleIndexOf(cellIndex, i, j);
        }

        /// <summary>① 按身体半径判距：0.1 厚的墙只该挡住贴墙那排采样点，不该整格外扩</summary>
        [Test]
        public void 薄栅栏按体半径挡住采样点()
        {
            // 墙轴压在 (1,1)(2,1) 的列心上、再往垂直方向让开 0.25 格：
            // 于是贴墙那排采样点距墙面 0.075，最远那排 0.575 —— "按半径判距"与"点采样"在这块几何上答案不同
            Vector2 a = Center(1, 1);
            Vector2 b = Center(2, 1);
            Vector2 axis = (a + b) * 0.5f;

            NewBox("薄墙", new Vector2(axis.x, axis.y + 0.25f), new Vector2(Mathf.Abs(b.x - a.x), 0.1f));
            Sync();

            NavMap map = Bake(_grid, Cap);

            // 几何距离是 0.075 / 0.575；实测小约 0.01（Physics2D 接触偏移把最近点算在略微外扩的形状上），故断言给带宽
            float near = map.SampleClearance(Sample(map, 1, 1, 2, 2));

            Assert.AreEqual(near, map.SampleClearance(Sample(map, 2, 1, 1, 2)), 0.02f, "(1,1) 与 (2,1) 对称位置的净空不一致");
            Assert.Greater(near, 0.02f, $"(1,1) 贴墙那排采样点的净空远小于 0.075。已标={Clearances(Cap)}");
            Assert.Less(near, 0.13f, $"(1,1) 贴墙那排采样点的净空超过到墙面的距离：判据没按最近点算。已标={Clearances(Cap)}");

            // 半径 0.5 的身体在那排站不住，但同一格最底下那排站得住 —— 判据落在采样点上，不是"整格能不能站"
            Assert.IsFalse(map.IsSampleFree(Sample(map, 1, 1, 2, 2), 0.5f, 0.05f), "半径 0.5 的身体被判成能站在贴墙那排采样点上");
            Assert.IsFalse(map.IsSampleFree(Sample(map, 2, 1, 1, 2), 0.3f, 0.05f), "半径 0.3 的身体被判成能站在贴墙那排采样点上");
            Assert.IsTrue(map.IsSampleFree(Sample(map, 1, 1, 2, 0), 0.1f, 0.05f), "半径 0.1 的身体在最远那排也被挡住了：净空判成了点采样");

            // 整格判据 = 格内最宽处（这格是离墙最远的那个角，0.676）：0.5 的怪站得下，0.65 的站不下
            Assert.IsTrue(map.IsWalkable(new Vector3Int(1, 1, 0), 0.5f, 0.05f), $"格内最宽处有 0.676，0.5 的怪却被判成整格不可走。已标={Clearances(Cap)}");
            Assert.IsFalse(map.IsWalkable(new Vector3Int(1, 1, 0), 0.65f, 0.05f), "0.65 的怪在这格里也站得下：整格判据没取最宽处");

            // 距墙面 0.70 格的那排：半径 0.5 + 皮厚 0.05 仍有余量
            Assert.IsTrue(map.IsWalkable(new Vector3Int(2, 2, 0), 0.5f, 0.05f), $"距墙 0.70 格的排被误判：半径算成了整格外扩。已标={Clearances(Cap)}");
            Assert.IsTrue(map.IsWalkable(new Vector3Int(1, 3, 0), 0.5f, 0.05f), "距墙 1.70 格的排被误标");

            // 超过解析上限的距离一律饱和成上限，不是 0
            Assert.AreEqual(Cap, map.SampleClearance(Sample(map, 4, 4, 1, 1)), 0.001f, "远格的净空没有饱和到解析上限");
        }

        /// <summary>② 预制体实尺寸：1×4 且 offset y=-0.3 的长条必须按 offset 后的真实范围判</summary>
        [Test]
        public void 带偏移的长条碰撞体按真实范围占用()
        {
            NewBox("栅栏", new Vector2(Center(3, 0).x + 0.5f, Center(3, 2).y), new Vector2(1f, 4f), yOffset: -0.3f);
            Sync();

            var fence = (BoxCollider2D)_spawned[_spawned.Count - 1].GetComponent<Collider2D>();
            NavMap map = Bake(_grid, Cap);

            // 期望占用由碰撞体自己量出（与烘焙同一套按半径判距），逐采样点对
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    for (int j = 0; j < NavMap.Subdivision; j++)
                    {
                        for (int i = 0; i < NavMap.Subdivision; i++)
                        {
                            int index = Sample(map, x, y, i, j);
                            Vector2 point = map.SampleCenter(index);

                            if (Vector2.Distance(point, fence.ClosestPoint(point)) > 0.5f) continue;

                            Assert.IsFalse(map.IsSampleFree(index, 0.5f, 0f), $"栅栏实占采样点 {point} 被判成可走。已标={Clearances(Cap)}");
                        }
                    }
                }
            }

            // 负偏移必须体现在包围盒上：1×4 居中在中心行时下沿应正好压住第 0 行，实际必须更低
            Assert.Less(fence.bounds.min.y, Center(3, 0).y, "offset y=-0.3 没落到包围盒上");
            Assert.IsTrue(map.IsWalkable(new Vector3Int(3, 5, 0), 0.5f, 0f), "长条上方第 5 排被误判：offset 没算进去");
            Assert.IsTrue(map.IsWalkable(new Vector3Int(0, 2, 0), 0.5f, 0f), "距长条 3 列外被误判：半径算成了整格外扩");
        }

        /// <summary>③ 排除口径：触发器、角色标记、动态体都不算障碍</summary>
        [Test]
        public void 触发器角色与动态体都不算障碍()
        {
            int baseline = Affected(Bake(NewGrid(), Cap), Cap);

            // 占满接近全域的触发器：任何一格净空被压下来都说明触发器没被排除
            NewBox("地图边界", new Vector2(3f, 3f), new Vector2(60f, 30f), trigger: true);

            GameObject actor = NewBox("角色", Center(2, 2), new Vector2(1f, 1f));

            actor.AddComponent<ManagedActorStub>();

            GameObject crate = NewBox("木箱", Center(5, 5), new Vector2(1f, 1f));

            Rigidbody2D body = crate.AddComponent<Rigidbody2D>();

            body.bodyType = RigidbodyType2D.Dynamic;
            Sync();

            NavMap map = Bake(_grid, Cap);

            Assert.AreEqual(baseline, Affected(map, Cap), "触发器/角色/动态体被当成了障碍");
            Assert.IsTrue(map.IsWalkable(new Vector3Int(2, 2, 0), 0.5f, 0f), "整张地图的边界触发器把格子压成了墙");
            Assert.IsTrue(map.IsWalkable(new Vector3Int(4, 4, 0), 0.5f, 0f), "可推动的木箱把格子压成了墙");
            Assert.IsTrue(map.IsWalkable(new Vector3Int(1, 1, 0), 0.5f, 0f), "挂了 IManagedActor 的角色把格子压成了墙");
        }

        /// <summary>④ 幂等：重复烘焙不能越积越多</summary>
        [Test]
        public void 重复烘焙结果一致()
        {
            int baseline = Affected(Bake(NewGrid(), Cap), Cap);

            NewBox("薄墙", new Vector2(Center(2, 0).x, Center(2, 0).y + 0.5f), new Vector2(2f, 0.1f));
            Sync();

            NavMap first = Bake(_grid, Cap);
            float clearance = first.CellClearance(new Vector3Int(2, 0, 0));

            NavMap second = Bake(_grid, Cap);

            Assert.AreEqual(clearance, second.CellClearance(new Vector3Int(2, 0, 0)), 0.001f, "重复烘焙的净空变了");
            Assert.Greater(Affected(first, Cap), baseline, "薄墙一格都没影响");

            // 拆掉墙再烘焙：受影响格必须全部回到上限
            Object.DestroyImmediate(_spawned[0]);

            _spawned.Clear();
            Sync();

            Assert.AreEqual(baseline, Affected(Bake(_grid, Cap), Cap), "障碍已拆，旧净空没清干净");
        }

        /// <summary>⑤ 真实预制体：玩家与敌人的碰撞体绝不能被烘成墙，否则角色脚下长出隐形障碍</summary>
        [Test]
        public void 真实角色预制体不被烘焙()
        {
            var player = (GameObject)Object.Instantiate(Resources.Load<GameObject>("scane_prebs/Player"));

            _spawned.Add(player);
            player.transform.position = Center(2, 2);

            var enemy = (GameObject)Object.Instantiate(Resources.Load<GameObject>("enemies/Enemy_1"));

            _spawned.Add(enemy);
            enemy.transform.position = Center(4, 4);

            Sync();

            NavMap map = Bake(_grid, Cap);

            Assert.IsTrue(map.IsWalkable(new Vector3Int(2, 2, 0), 0.5f, 0f), "玩家所在格被压成了墙");
            Assert.IsTrue(map.IsWalkable(new Vector3Int(4, 4, 0), 0.5f, 0f), "敌人所在格被压成了墙");

            // 反面锚点：同位置换成没有角色的静态碰撞体，必须判得出来
            GameObject staticProp = NewBox("静态道具", Center(2, 2), new Vector2(1f, 1f));

            Sync();

            NavMap blocked = Bake(_grid, Cap);

            Assert.IsFalse(blocked.IsWalkable(new Vector3Int(2, 2, 0), 0.5f, 0f), "同位置的静态碰撞体也没判出来：判据本身失效了");
            Assert.IsNotNull(staticProp);
        }

        /// <summary>只挂标记接口，用来验证 IManagedActor 排除</summary>
        private sealed class ManagedActorStub : MonoBehaviour, IManagedActor
        {
        }
    }
}
