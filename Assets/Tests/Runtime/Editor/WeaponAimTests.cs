using DeepseaOil.Presentation.Combat;
using NUnit.Framework;
using UnityEngine;

namespace DeepseaOil.Tests
{
    /// <summary>武器瞄准纯数学契约：椭圆轨道、rawAngle 层级、透视挤压与垂直抬升归零</summary>
    [Category("Presentation")]
    public sealed class WeaponAimTests
    {
        private static readonly Vector2 Orbit = new Vector2(0.25f, 0.2f);

        private static readonly AnimationCurve Lift = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        private static readonly WeaponAimController.AimSettings Settings =
            new WeaponAimController.AimSettings(
                Vector2.zero,
                Orbit,
                0.7f,
                1f,
                28f,
                45f,
                12f,
                Lift
            );

        private static WeaponAimController.AimSolution Solve(Vector2 target)
        {
            return WeaponAimController.Solve(Vector2.zero, target, in Settings);
        }

        private static Vector2 AtAngle(float degrees, float distance)
        {
            float radians = degrees * Mathf.Deg2Rad;

            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * distance;
        }

        [Test]
        public void 右水平_身前_满尺寸()
        {
            WeaponAimController.AimSolution s = Solve(new Vector2(10f, 0f));

            Assert.IsFalse(s.FacingLeft);
            Assert.IsFalse(s.IsBehind);
            Assert.AreEqual(0f, s.Angle, 0.001f);
            Assert.AreEqual(0.25f, s.Position.x, 0.001f);
            Assert.AreEqual(0f, s.Position.y, 0.001f);
            Assert.AreEqual(1f, s.Scale.x, 0.001f);
            Assert.AreEqual(1f, s.Scale.y, 0.001f);
        }

        [Test]
        public void 左水平_翻转_身前()
        {
            WeaponAimController.AimSolution s = Solve(new Vector2(-10f, 0f));

            Assert.IsTrue(s.FacingLeft);
            Assert.IsFalse(s.IsBehind);
            Assert.AreEqual(180f, s.Angle, 0.001f);
            Assert.AreEqual(-0.25f, s.Position.x, 0.001f);
            Assert.AreEqual(0f, s.Position.y, 0.001f);
            Assert.AreEqual(1f, s.Scale.x, 0.001f);
            Assert.AreEqual(1f, s.Scale.y, 0.001f);
        }

        [Test]
        public void 正上_人后_最短长度_厚度不变()
        {
            WeaponAimController.AimSolution s = Solve(new Vector2(0f, 10f));

            Assert.IsTrue(s.IsBehind);
            Assert.AreEqual(90f, s.Angle, 0.001f);
            Assert.AreEqual(0f, s.Position.x, 0.001f);
            Assert.AreEqual(0.2f, s.Position.y, 0.001f);
            Assert.AreEqual(0.7f, s.Scale.x, 0.001f);
            Assert.AreEqual(1f, s.Scale.y, 0.001f);
        }

        [Test]
        public void 正下_人前_最短长度_厚度不变()
        {
            WeaponAimController.AimSolution s = Solve(new Vector2(0f, -10f));

            Assert.IsFalse(s.IsBehind);
            Assert.AreEqual(-90f, s.Angle, 0.001f);
            Assert.AreEqual(0f, s.Position.x, 0.001f);
            Assert.AreEqual(-0.2f, s.Position.y, 0.001f);
            Assert.AreEqual(0.7f, s.Scale.x, 0.001f);
            Assert.AreEqual(1f, s.Scale.y, 0.001f);
        }

        [Test]
        public void 近距离正上_抬升归零_严格垂直()
        {
            WeaponAimController.AimSolution s = Solve(new Vector2(0f, 0.5f));

            Assert.AreEqual(0f, s.LiftAngle, 0.001f);
            Assert.AreEqual(90f, s.Angle, 0.001f);
            Assert.AreEqual(0.2f, s.Position.y, 0.001f);
            Assert.AreEqual(0.7f, s.Scale.x, 0.001f);
            Assert.AreEqual(1f, s.Scale.y, 0.001f);
            Assert.IsTrue(s.IsBehind);
        }

        [Test]
        public void 近距离正下_抬升归零_严格垂直()
        {
            WeaponAimController.AimSolution s = Solve(new Vector2(0f, -0.5f));

            Assert.AreEqual(0f, s.LiftAngle, 0.001f);
            Assert.AreEqual(-90f, s.Angle, 0.001f);
            Assert.AreEqual(-0.2f, s.Position.y, 0.001f);
            Assert.AreEqual(0.7f, s.Scale.x, 0.001f);
            Assert.AreEqual(1f, s.Scale.y, 0.001f);
            Assert.IsFalse(s.IsBehind);
        }

        [Test]
        public void 斜上方27度_人前()
        {
            WeaponAimController.AimSolution s = Solve(AtAngle(27f, 10f));

            Assert.IsFalse(s.IsBehind);
            Assert.AreEqual(0.9720f, s.Scale.x, 0.001f);
            Assert.AreEqual(1f, s.Scale.y, 0.001f);
        }

        [Test]
        public void 斜上方29度_人后()
        {
            WeaponAimController.AimSolution s = Solve(AtAngle(29f, 10f));

            Assert.IsTrue(s.IsBehind);
        }

        [Test]
        public void 斜上方20度_长度收缩()
        {
            WeaponAimController.AimSolution s = Solve(AtAngle(20f, 10f));

            Assert.AreEqual(0.9846f, s.Scale.x, 0.001f);
            Assert.AreEqual(1f, s.Scale.y, 0.001f);
        }

        [Test]
        public void 左上30度_人后_左半侧()
        {
            WeaponAimController.AimSolution s = Solve(AtAngle(150f, 10f));

            Assert.IsTrue(s.FacingLeft);
            Assert.IsTrue(s.IsBehind);
            Assert.AreEqual(150f, s.Angle, 0.01f);
            Assert.Less(s.Position.x, 0f);
            Assert.Greater(s.Position.y, 0f);
            Assert.AreEqual(0.9655f, s.Scale.x, 0.001f);
            Assert.AreEqual(1f, s.Scale.y, 0.001f);
        }

        [Test]
        public void 左上20度_人前()
        {
            WeaponAimController.AimSolution s = Solve(AtAngle(160f, 10f));

            Assert.IsTrue(s.FacingLeft);
            Assert.IsFalse(s.IsBehind);
            Assert.AreEqual(160f, s.Angle, 0.01f);
            Assert.Less(s.Position.x, 0f);
            Assert.Greater(s.Position.y, 0f);
            Assert.AreEqual(0.9846f, s.Scale.x, 0.001f);
            Assert.AreEqual(1f, s.Scale.y, 0.001f);
        }

        [Test]
        public void 斜45度_长度收缩()
        {
            WeaponAimController.AimSolution s = Solve(new Vector2(10f, 10f));

            Assert.IsTrue(s.IsBehind);
            Assert.AreEqual(0.9235f, s.Scale.x, 0.001f);
            Assert.AreEqual(1f, s.Scale.y, 0.001f);
        }

        [Test]
        public void 近距离右水平_抬升但仍在身前()
        {
            WeaponAimController.AimSolution s = Solve(new Vector2(0.5f, 0f));

            Assert.AreEqual(43.125f, s.LiftAngle, 0.001f);
            Assert.AreEqual(43.125f, s.Angle, 0.001f);
            Assert.IsFalse(s.IsBehind);
            Assert.AreEqual(0.25f, s.Position.x, 0.001f);
            Assert.AreEqual(1f, s.Scale.x, 0.001f);
            Assert.AreEqual(1f, s.Scale.y, 0.001f);
        }

        [Test]
        public void 近距离左水平_抬升后仍在身前()
        {
            WeaponAimController.AimSolution s = Solve(new Vector2(-0.5f, 0f));

            Assert.AreEqual(43.125f, s.LiftAngle, 0.001f);
            Assert.AreEqual(136.875f, s.Angle, 0.001f);
            Assert.IsTrue(s.FacingLeft);
            Assert.IsFalse(s.IsBehind);
            Assert.AreEqual(-0.25f, s.Position.x, 0.001f);
            Assert.AreEqual(1f, s.Scale.x, 0.001f);
            Assert.AreEqual(1f, s.Scale.y, 0.001f);
        }

        [Test]
        public void 近距离右上_抬升后仍按rawAngle判人后()
        {
            WeaponAimController.AimSolution s = Solve(new Vector2(0.5f, 0.5f));

            Assert.IsTrue(s.IsBehind);
            Assert.Greater(s.Angle, 45f);
            Assert.Less(s.Angle, 90f);
            Assert.AreEqual(0.9235f, s.Scale.x, 0.001f);
            Assert.AreEqual(1f, s.Scale.y, 0.001f);
        }

        [Test]
        public void 肩部偏移_平移轨道中心()
        {
            var settings = new WeaponAimController.AimSettings(
                new Vector2(0f, 0.25f),
                Orbit,
                0.7f,
                1f,
                28f,
                45f,
                12f,
                Lift
            );

            WeaponAimController.AimSolution s =
                WeaponAimController.Solve(Vector2.zero, new Vector2(10f, 0.25f), in settings);

            Assert.AreEqual(0.25f, s.Position.x, 0.001f);
            Assert.AreEqual(0.25f, s.Position.y, 0.001f);
            Assert.AreEqual(0f, s.Angle, 0.001f);
            Assert.AreEqual(1f, s.Scale.x, 0.001f);
            Assert.AreEqual(1f, s.Scale.y, 0.001f);
        }

        [Test]
        public void 死区_不产生NaN()
        {
            WeaponAimController.AimSolution s = Solve(Vector2.zero);

            Assert.Greater(s.Distance, 0f);
            Assert.IsFalse(float.IsNaN(s.Position.x));
            Assert.IsFalse(float.IsNaN(s.Position.y));
            Assert.IsFalse(float.IsNaN(s.Angle));
            Assert.IsFalse(float.IsNaN(s.Scale.x));
            Assert.IsFalse(float.IsNaN(s.Scale.y));
            Assert.IsFalse(s.IsBehind);
        }

        [Test]
        public void 空曲线_只关闭抬升()
        {
            var settings = new WeaponAimController.AimSettings(
                Vector2.zero,
                Orbit,
                0.7f,
                1f,
                28f,
                45f,
                12f,
                null
            );

            WeaponAimController.AimSolution s =
                WeaponAimController.Solve(Vector2.zero, new Vector2(0.5f, 0f), in settings);

            Assert.AreEqual(0f, s.LiftAngle, 0.001f);
            Assert.AreEqual(0f, s.Angle, 0.001f);
            Assert.AreEqual(1f, s.Scale.x, 0.001f);
            Assert.AreEqual(1f, s.Scale.y, 0.001f);
            Assert.IsFalse(s.IsBehind);
        }
    }
}
