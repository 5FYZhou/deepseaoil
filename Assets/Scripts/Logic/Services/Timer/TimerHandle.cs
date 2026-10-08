using System;

namespace DeepseaOil.Logic.Services.Time
{
    public readonly struct TimerHandle : IEquatable<TimerHandle>
    {
        public static readonly TimerHandle Invalid = new(0);

        public int Id { get; }

        public bool IsValid => Id != 0;

        public TimerHandle(int id)
        {
            Id = id;
        }

        public bool Equals(TimerHandle other) => Id == other.Id;
        public override bool Equals(object obj) => (obj is TimerHandle other && Equals(other));

        public override int GetHashCode() => Id;

        public static bool operator ==(TimerHandle left, TimerHandle right)
        {
            return left.Equals(right);
        }
        public static bool operator !=(TimerHandle left,TimerHandle right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return IsValid ? $"TimerHandle({Id})": "TimerHandle(Invalid)";
        }
    }
}
