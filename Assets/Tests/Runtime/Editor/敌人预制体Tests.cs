// ---------------------------------------------------------------------------
// 敌人预制体与大脑策略 · 运行期测试
//
// 【为什么在这里】本文件守三条「改错了不报错」的约定：
//   ① 寻址约定：CombatDirector / CombatDummyHarness 按 `enemies/Enemy_{enemy.id}` 找预制体，
//      找不到只打一条 LogError 并取消生成 —— 预制体放错目录 / 改错文件名，编译期完全无感。
//   ② 预制体装配：物理参数、碰撞体、View 三件全归预制体，代码只抓不补挂
//      —— 少挂一个组件要到 Play 才炸。
//   ③ 两处半径：Gizmo 读 EnemyTuning 资产、物理读预制体碰撞体；不一致不报错，只表现成两个尺寸。
//   另加一条策略缝：EnemyLogic 只认 IEnemyBrain，状态层只读 EnemyLogic.Intent。
//
// 【覆盖边界，写在明处】只用 Resources.Load 读资产 ＋ 纯逻辑装一次 EnemyLogic：
//   实例化是否干净、追踪 / 受击变色 / 击退滑停 / 碎裂死亡仍须人工 PlayMode 看
//   （步骤见 Docs/美术装配指南.md 第六节）。
//
// 跑法：Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All
// ---------------------------------------------------------------------------

using DeepseaOil.Data;
using DeepseaOil.Data.Settings;
using DeepseaOil.Logic;
using DeepseaOil.Presentation.Actor;
using DeepseaOil.Presentation.Adapters;
using DeepseaOil.Presentation.Visual;
using NUnit.Framework;
using UnityEngine;

namespace DeepseaOil.Tests
{
    public class 敌人预制体Tests
    {
        /// <summary>假大脑：记录被问了几次，并吐一条固定意图（用来证明 EnemyLogic 只认接口）。</summary>
        private sealed class BrainProbe : IEnemyBrain
        {
            public int Calls;

            public EnemyIntent Last;

            public EnemyIntent Decide(in EnemyBrainContext ctx)
            {
                Calls++;
                Last = new EnemyIntent(Vector2.right, 3f);

                return Last;
            }
        }

        /// <summary>调参条目：只喂不读资产的用例；半径以资产为准，由 P3 校验。</summary>
        private static EnemyTuningEntry Tuning()
        {
            return TuningFactory.EnemyEntry();
        }

        /// <summary>按生产代码的同一条约定寻址预制体（不写死路径，免得两处约定漂开）。</summary>
        private static GameObject LoadPrefab()
        {
            string key = $"enemies/Enemy_{RowFactory.EnemyRow().Id}";

            return Resources.Load<GameObject>(key);
        }

        [Test]
        public void P1_预制体按enemy表id寻址能加载到()
        {
            Assert.IsNotNull(
                LoadPrefab(),
                "Assets/Resources/enemies/Enemy_1.prefab 必须存在：两条生成路径都按 enemies/Enemy_{id} 寻址，缺件只报错不拼白模");
        }

        [Test]
        public void P2_Root挂齐组合件与物理真值()
        {
            GameObject prefab = LoadPrefab();

            Assert.IsNotNull(prefab, "先要有预制体");

            Assert.IsNotNull(prefab.GetComponent<EnemyActor>(), "Root 必须挂 EnemyActor（CombatDirector 靠它判定本次生成是否作废）");
            Assert.IsNotNull(prefab.GetComponent<EnemyMotor>(), "Root 必须挂 EnemyMotor，否则 EnemyActor.Initialize 判定生成作废");

            var body = prefab.GetComponent<Rigidbody2D>();

            Assert.IsNotNull(body, "EnemyMotor 带 [RequireComponent(Rigidbody2D)]");
            Assert.AreEqual(0f, body.gravityScale, "俯视角：重力 0");
            Assert.IsTrue(body.freezeRotation, "俯视角：冻结旋转");
            Assert.AreEqual(RigidbodyInterpolation2D.Interpolate, body.interpolation, "动起来的刚体必须 Interpolate");
            Assert.AreEqual(CollisionDetectionMode2D.Continuous, body.collisionDetectionMode, "敌人侧连续检测：被击退成高速时单帧位移不可控");
        }

        [Test]
        public void P3_碰撞体半径与调参资产的radius一致()
        {
            GameObject prefab = LoadPrefab();

            Assert.IsNotNull(prefab, "先要有预制体");

            var collider = prefab.GetComponent<CircleCollider2D>();

            Assert.IsNotNull(collider, "Root 必须有 CircleCollider2D：碰撞体半径以预制体为准，代码读调参资产画 Gizmo");

            // 比的是真资产，不是类默认值：默认值只兜"资产没配"，比它等于什么都没守
            var asset = Resources.Load<EnemyTuning>(EnemyTuning.ResourceKey);

            Assert.IsNotNull(asset, "Assets/Resources/tuning/EnemyTuning.asset 必须在：Gizmo 圈读的是它");

            int id = RowFactory.EnemyRow().Id;

            Assert.IsTrue(
                asset.TryGet(id, out EnemyTuningEntry entry),
                $"调参资产必须为 enemy.id={id} 配一行：缺行会静默退回第一条并只发一次警告");

            Assert.AreEqual(
                entry.radius,
                collider.radius,
                1e-4f,
                "EnemyTuning.asset 的 radius 与预制体碰撞体半径必须一致，否则 Gizmo 圈与实际碰撞体是两个答案且不报错");
        }

        [Test]
        public void P4_View上有渲染与动画件()
        {
            GameObject prefab = LoadPrefab();

            Assert.IsNotNull(prefab, "先要有预制体");
            Assert.IsNotNull(
                prefab.GetComponentInChildren<SpriteRenderer>(true),
                "View 上必须有 SpriteRenderer：EnemyActor 的颜色与 Y-Sort 都写它");
            Assert.IsNotNull(
                prefab.GetComponentInChildren<ActorAnimationView>(true),
                "View 上必须有 ActorAnimationView：受击 / 死亡触发器与运动学快照的落点");
            Assert.IsNotNull(
                prefab.GetComponentInChildren<Animator>(true),
                "View 上必须有 Animator（controller 可以留空：HasValidAnimator 为假时全部调用是安全 no-op）");
        }

        [Test]
        public void P5_工厂默认给连续追逐且无目标吐空意图()
        {
            var spec = new EnemySpec(RowFactory.EnemyRow(), Tuning());
            IEnemyBrain brain = EnemyBrainFactory.Create(spec);

            Assert.IsTrue(brain is ChaseBrain, "当前只有一种怪：默认策略是连续追逐");

            EnemyIntent idle = brain.Decide(EnemyBrainContext.WithoutTarget(Vector2.zero));

            Assert.IsTrue(idle.IsIdle, "没有目标必须吐空意图（站住），不能沿用上一帧方向");
        }

        [Test]
        public void P6_注入的大脑被采用且意图转发给状态层()
        {
            var spec = new EnemySpec(RowFactory.EnemyRow(), Tuning());
            var probe = new BrainProbe();
            var logic = new EnemyLogic(new MotorProbe(), spec, probe);

            logic.SetTarget(new Vector2(10f, 0f));
            logic.Tick(0f, 0.02f);

            Assert.AreSame(probe, logic.Brain, "Brain 必须是注入的那一个：换策略不该改 EnemyLogic");
            Assert.AreEqual(1, probe.Calls, "每个物理帧必须问且只问一次大脑");
            Assert.AreEqual(probe.Last.Speed, logic.Intent.Speed, 1e-4f, "意图必须原样落到 EnemyLogic.Intent（状态层读的是它）");
        }

        [Test]
        public void P7_站桩大脑无视目标恒空意图()
        {
            IEnemyBrain brain = EnemyBrainFactory.CreateStand();

            Assert.IsTrue(brain is StandBrain, "打靶木桩必须拿到 StandBrain，不能被 spec 换回追逐");

            EnemyIntent intent = brain.Decide(new EnemyBrainContext(Vector2.zero, new Vector2(0.5f, 0f), true));

            Assert.IsTrue(intent.IsIdle, "玩家贴脸也要吐空意图：木桩原地站桩，永不追击");
        }
    }
}
