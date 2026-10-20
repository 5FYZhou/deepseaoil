using System;
using DeepseaOil.Data;
using DeepseaOil.Logic.Combat;
using DeepseaOil.Logic.Events;
using UnityEngine;
using cfg.dso;

namespace DeepseaOil.Logic.Player
{
    public sealed class PlayerStats : IAlivable
    {
        private readonly PlayerSpec _spec;

        private float _current;

        private int _seedCapacity;

        private int _seedCount;

        private float _invulnerableUntil = float.NegativeInfinity;

        public PlayerStats(PlayerSpec spec)
        {
            _spec = spec;
            _current = spec.MaxHp;
            Water = Clamp(spec.WaterStart, spec.WaterCapacity);
            Earth = Clamp(spec.EarthStart, spec.EarthCapacity);
            Seed = SeedType.None;
            _seedCapacity = spec.SeedCapacity < 0 ? 0 : spec.SeedCapacity;
            _seedCount = 0;
        }

        public PlayerSpec Spec => _spec;

        public int Water { get; private set; }

        public int Earth { get; private set; }

        public SeedType Seed { get; private set; }

        public int SeedCount => _seedCount;

        public int SeedCapacity => _seedCapacity;

        public int WaterCapacity => _spec.WaterCapacity;

        public int EarthCapacity => _spec.EarthCapacity;

        public bool IsAlive => _current > 0f;

        public float Current => _current;

        // 必须写 !(now < until)：否则 NaN 时永久无敌
        public static bool CanTakeDamage(float now, float invulnerableUntil)
        {
            return !(now < invulnerableUntil);
        }

        // 被无敌帧挡掉时不扣血也不写无敌
        public bool ApplyDamage(float amount, float now)
        {
            if (amount <= 0f) return false;

            if (!CanTakeDamage(now, _invulnerableUntil)) return false;

            _current = Mathf.Max(0f, _current - amount);

            _invulnerableUntil = now + _spec.InvulnerableDuration;

            EventBus<PlayerHealthChanged>.Publish(new PlayerHealthChanged(_current, _spec.MaxHp));

            return true;
        }

        // 满血返回 false
        public bool TryHeal(float amount = 1f)
        {
            if (amount <= 0f) return false;

            if (_current >= _spec.MaxHp) return false;

            _current = Mathf.Min(_spec.MaxHp, _current + amount);

            EventBus<PlayerHealthChanged>.Publish(new PlayerHealthChanged(_current, _spec.MaxHp));

            return true;
        }

        public void ResetToFull()
        {
            _current = _spec.MaxHp;
            _invulnerableUntil = float.NegativeInfinity;

            EventBus<PlayerHealthChanged>.Publish(new PlayerHealthChanged(_current, _spec.MaxHp));
        }

        /// <summary>存档导出；位置与朝向由调用方填</summary>
        public PlayerSaveData ExportSave()
        {
            return new PlayerSaveData
            {
                hp = _current,
                maxHp = _spec.MaxHp,
                water = Water,
                earth = Earth,
                seedType = (int)Seed,
                seedCount = _seedCount
            };
        }

        /// <summary>读盘回填；越界夹取</summary>
        public void ImportSave(PlayerSaveData data)
        {
            if (data == null) return;

            _current = Mathf.Clamp(data.hp, 0f, _spec.MaxHp);

            _invulnerableUntil = float.NegativeInfinity;

            Water = Clamp(data.water, WaterCapacity);
            Earth = Clamp(data.earth, EarthCapacity);

            Seed = Enum.IsDefined(typeof(SeedType), data.seedType) ? (SeedType)data.seedType : SeedType.None;

            _seedCount = Seed == SeedType.None ? 0 : Clamp(data.seedCount, _seedCapacity);

            Announce();
        }

        public int AmountOf(ResourceKind kind)
        {
            return kind == ResourceKind.Water ? Water : Earth;
        }

        public int CapacityOf(ResourceKind kind)
        {
            return kind == ResourceKind.Water ? WaterCapacity : EarthCapacity;
        }

        // 扣成功才发 PlayerAmmoChanged
        public bool TryConsume(ResourceKind kind, int amount = 1)
        {
            if (amount <= 0) return false;

            if (AmountOf(kind) < amount) return false;

            if (kind == ResourceKind.Water) Water -= amount;
            else Earth -= amount;

            PublishAmmo();

            return true;
        }

        public bool Refill(ResourceKind kind)
        {
            return TryAddAmmo(kind, CapacityOf(kind));
        }

        // 返回值 = "确实涨了"；满额也照发事件
        public bool TryAddAmmo(ResourceKind kind, int amount = 1)
        {
            if (amount <= 0) return false;

            return AdvanceAmmo(kind, amount);
        }

        private bool AdvanceAmmo(ResourceKind kind, int amount)
        {
            int capacity = CapacityOf(kind);

            int next = Clamp(AmountOf(kind) + amount, capacity);

            if (next == AmountOf(kind)) return false;

            if (kind == ResourceKind.Water) Water = next;
            else Earth = next;

            PublishAmmo();

            return true;
        }

        // 种子单槽：GrantSeed 只在空仓时成功
        public bool CanPickupSeed()
        {
            return !(_seedCount > 0 && Seed != SeedType.None);
        }

        public bool GrantSeed(SeedType seed)
        {
            if (seed == SeedType.None) return false;

            if (!CanPickupSeed()) return false;

            Seed = seed;
            _seedCount = _seedCapacity;

            PublishAmmo();

            return true;
        }

        // 每次播种只扣 1 点，扣空才清类型
        public bool TryConsumeSeed()
        {
            if (_seedCount <= 0) return false;

            _seedCount--;

            if (_seedCount == 0) Seed = SeedType.None;

            PublishAmmo();

            return true;
        }

        // 无种子返回 false；out 参数恒被赋值
        public bool TryDiscardSeed(out SeedType discardedType, out int remainingCount)
        {
            discardedType = Seed;
            remainingCount = _seedCount;

            if (_seedCount <= 0) return false;

            Seed = SeedType.None;
            _seedCount = 0;

            PublishAmmo();

            return true;
        }

        public void Announce()
        {
            EventBus<PlayerHealthChanged>.Publish(new PlayerHealthChanged(_current, _spec.MaxHp));

            PublishAmmo();
        }

        private void PublishAmmo()
        {
            EventBus<PlayerAmmoChanged>.Publish(
                new PlayerAmmoChanged(Water, WaterCapacity, Earth, EarthCapacity, Seed, _seedCount, _seedCapacity));
        }

        private static int Clamp(int value, int capacity)
        {
            if (capacity < 0) capacity = 0;

            if (value < 0) return 0;

            return value > capacity ? capacity : value;
        }
    }
}
