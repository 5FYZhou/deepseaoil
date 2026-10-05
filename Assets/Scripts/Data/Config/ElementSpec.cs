using cfg.demo;
using DeepseaOil.Presentation.Element;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Data
{
    /// <summary>
    /// 投掷物的元素属性
    /// </summary>

    public readonly struct ElementSpec
    {
        public readonly ElementType Type;
        public readonly ElementTag Tags;
        public readonly int Temperature;
        public readonly int Wet;
        public readonly int Conductivity;

        public ElementSpec(ElementType type, ElementTag tag, int temperature, int wet, int conductivity)
        {
            Type = type;
            Tags = tag;
            Temperature = temperature;
            Wet = wet;
            Conductivity = conductivity;
        }
    }
}
