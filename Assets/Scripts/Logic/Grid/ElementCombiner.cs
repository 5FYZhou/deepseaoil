using cfg.demo;
using DeepseaOil.Data;
using System;
using UnityEngine;

namespace DeepseaOil.Logic.Grid
{
    public static class ElementCombiner
    {
        public static ElementSpec TryCombiner(ElementSpec a, ElementSpec b)
        {
            int temperature = Mathf.Clamp(
                a.Temperature + b.Temperature,
                -6, 6);

            int humidity = Mathf.Clamp(
                a.Wet + b.Wet,
                0, 6);

            int conductivity =
                Mathf.Max(
                    a.Conductivity,
                    b.Conductivity
                );

            ElementTag tags = a.Tags | b.Tags;

            return new ElementSpec(
                ElementType.Earth,
                tags,
                temperature,
                humidity,
                conductivity
            );
        }
    }
}