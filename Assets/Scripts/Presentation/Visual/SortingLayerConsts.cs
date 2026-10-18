using UnityEngine;

namespace DeepseaOil.Presentation.Visual
{
    /// <summary>排序图层名与受光掩码</summary>
    /// <remarks>ID 用属性不用 static readonly（静态字段会缓存成 0）；未登记层 NameToID 为 -1，须剔除。</remarks>
    public static class SortingLayerConsts
    {
        public const string Default = "Default";
        public const string Ground = "Ground";
        public const string Actor = "Actor";
        public const string Aim = "Aim";
        public const string Effect = "Effect";
        public const string UI = "UI";

        public static int GroundId => SortingLayer.NameToID(Ground);
        public static int ActorId => SortingLayer.NameToID(Actor);
        public static int AimId => SortingLayer.NameToID(Aim);
        public static int EffectId => SortingLayer.NameToID(Effect);
        public static int UIId => SortingLayer.NameToID(UI);

        /// <summary>受光掩码：仅 Ground 与 Actor 接收光照</summary>
        public static int LitLayersMask
        {
            get
            {
                int groundId = GroundId;
                int actorId = ActorId;

                return (groundId >= 0 ? 1 << groundId : 0) | (actorId >= 0 ? 1 << actorId : 0);
            }
        }
    }
}
