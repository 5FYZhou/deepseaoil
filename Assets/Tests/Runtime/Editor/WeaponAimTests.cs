using DeepseaOil.Presentation.Combat;
using NUnit.Framework;
using UnityEngine;

namespace DeepseaOil.Tests
{
    /// <summary>悬浮武器纯数学契约：侧挂、抬升、翻转与上下半球</summary>
    [Category("Presentation")]
    public sealed class WeaponAimTests
    {
        private static readonly Vector2 Side = new Vector2(0.6f, 0.4f);

        private const float MaxLift = 25f;
        private const float Threshold = 3f;

        private static AnimationCurve LinearLift()
        {
            return new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(1f, 0f)
            );
        }

        private static WeaponAimController.AimSolution Solve(Vector2 target)
        {
            return WeaponAimController.Solve(
                Vector2.zero,
                target,
                Side,
                MaxLift,
                Threshold,
                LinearLift()
            );
        }

        [Test]
        public void 右瞄准_无抬升_右侧前方()
        {
            WeaponAimController.AimSolution s = Solve(new Vector2(10f, 0f));

            Assert.IsFalse(s.FacingLeft);
            Assert.IsFalse(s.AimingUpwards);
            Assert.AreEqual(0f, s.Angle, 0.001f);
            Assert.AreEqual(0.6f, s.Position.x, 0.001f);
            Assert.AreEqual(0.4f, s.Position.y, 0.001f);
            Assert.AreEqual(0f, s.LiftAngle, 0.001f);
        }

        [Test]
        public void 左瞄准_翻转_左侧前方()
        {
            WeaponAimController.AimSolution s = Solve(new Vector2(-10f, 0f));

            Assert.IsTrue(s.FacingLeft);
            Assert.IsFalse(s.AimingUpwards);
            Assert.AreEqual(180f, s.Angle, 0.001f);
            Assert.AreEqual(-0.6f, s.Position.x, 0.001f);
            Assert.AreEqual(0.4f, s.Position.y, 0.001f);
        }

        [Test]
        public void 上瞄准_只切层级_不转枪身()
        {
            WeaponAimController.AimSolution s = Solve(new Vector2(0f, 10f));

            Assert.IsFalse(s.FacingLeft);
            Assert.IsTrue(s.AimingUpwards);
            Assert.AreEqual(0f, s.Angle, 0.001f);
            Assert.AreEqual(0.6f, s.Position.x, 0.001f);
            Assert.AreEqual(0.4f, s.Position.y, 0.001f);
        }

        [Test]
        public void 下瞄准_只切层级_不转枪身()
        {
            WeaponAimController.AimSolution s = Solve(new Vector2(0f, -10f));

            Assert.IsFalse(s.FacingLeft);
            Assert.IsFalse(s.AimingUpwards);
            Assert.AreEqual(0f, s.Angle, 0.001f);
            Assert.AreEqual(0.6f, s.Position.x, 0.001f);
            Assert.AreEqual(0.4f, s.Position.y, 0.001f);
        }

        [Test]
        public void 近距离右瞄_抬升但位置不变()
        {
            WeaponAimController.AimSolution s = Solve(new Vector2(0.5f, 0f));

            Assert.Greater(s.LiftAngle, 0f);
            Assert.Greater(s.Angle, 0f);
            Assert.IsFalse(s.AimingUpwards);
            Assert.AreEqual(0.6f, s.Position.x, 0.001f);
            Assert.AreEqual(0.4f, s.Position.y, 0.001f);
        }

        [Test]
        public void 近距离左瞄_抬升仍朝上()
        {
            WeaponAimController.AimSolution s = Solve(new Vector2(-0.5f, 0f));

            Assert.IsTrue(s.FacingLeft);
            Assert.Less(s.Angle, 180f);
            Assert.Greater(s.Angle, 90f);
            Assert.Greater(s.LiftAngle, 0f);
            Assert.AreEqual(-0.6f, s.Position.x, 0.001f);
            Assert.AreEqual(0.4f, s.Position.y, 0.001f);
        }

        [Test]
        public void 超过阈值_抬升归零()
        {
            WeaponAimController.AimSolution s = Solve(new Vector2(5f, 0f));

            Assert.AreEqual(0f, s.LiftAngle, 0.001f);
            Assert.AreEqual(0f, s.Angle, 0.001f);
        }

        [Test]
        public void 侧挂_左右位置与垂直偏移固定()
        {
            WeaponAimController.AimSolution right = Solve(new Vector2(10f, 0f));
            WeaponAimController.AimSolution left = Solve(new Vector2(-10f, 0f));

            Assert.AreEqual(0.6f, right.Position.x, 0.001f);
            Assert.AreEqual(0.4f, right.Position.y, 0.001f);
            Assert.AreEqual(-0.6f, left.Position.x, 0.001f);
            Assert.AreEqual(0.4f, left.Position.y, 0.001f);
        }

        [Test]
        public void 左上瞄准_左侧且层级在上()
        {
            WeaponAimController.AimSolution s = Solve(new Vector2(-10f, 10f));

            Assert.IsTrue(s.FacingLeft);
            Assert.IsTrue(s.AimingUpwards);
            Assert.AreEqual(180f, s.Angle, 0.001f);
            Assert.AreEqual(-0.6f, s.Position.x, 0.001f);
            Assert.AreEqual(0.4f, s.Position.y, 0.001f);
        }

        [Test]
        public void 死区_不产生NaN()
        {
            WeaponAimController.AimSolution s = Solve(Vector2.zero);

            Assert.Greater(s.Distance, 0f);
            Assert.IsFalse(float.IsNaN(s.Position.x));
            Assert.IsFalse(float.IsNaN(s.Position.y));
            Assert.IsFalse(float.IsNaN(s.Angle));
        }

        [Test]
        public void 空曲线_不抬升()
        {
            WeaponAimController.AimSolution s = WeaponAimController.Solve(
                Vector2.zero,
                new Vector2(0.5f, 0f),
                Side,
                MaxLift,
                Threshold,
                null
            );

            Assert.AreEqual(0f, s.LiftAngle, 0.001f);
            Assert.AreEqual(0f, s.Angle, 0.001f);
        }
    }
}
