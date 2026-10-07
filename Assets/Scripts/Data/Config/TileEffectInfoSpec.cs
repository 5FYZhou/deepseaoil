using cfg.demo;
using System.Collections.Generic;

namespace DeepseaOil.Data
{

    /// <summary>
    /// 多个效果和级别集合，直接读表的、未处理的数据
    /// </summary>
    public readonly struct TileEffectInfoSpec
    {
        public readonly IReadOnlyList<TileEffectType> Effects;
        public readonly IReadOnlyList<int> ValuePos;

        public TileEffectInfoSpec(IReadOnlyList<TileEffectType> tileEffects, IReadOnlyList<int> valuePos)
        {
            Effects = tileEffects;
            ValuePos = valuePos;
        }
    }

}
