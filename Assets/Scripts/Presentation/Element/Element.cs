using System;
using UnityEngine;

namespace DeepseaOil.Presentation.Element
{
    public enum ElementType
    {
        None = 0,
        Water,
        Soil,
        Environment
    }

    [Flags]
    public enum ElementTag
    {
        None = 0,
        Soil = 1 << 0,
        Sand = 1 << 1,
        Plant = 1 << 2,
    }

    public enum Conductivity
    {
        None = 0,
        Medium = 1,
        Strong = 2
    }

    public class Element
    {
        public ElementType Type { get; }
        public ElementTag Tags { get; }
        public int Temperature { get; }
        public int Humidity { get; }
        public Conductivity Conductivity { get; }

        public Element(ElementType type, ElementTag tag, int temperature, int humidity, Conductivity conductivity)
        {
            Type = type;
            Tags = tag;
            Temperature = temperature;
            Humidity = humidity;
            Conductivity = conductivity;
        }
    }

    public static class ElementCombiner
    {
        public static Element TryCombiner(Element a, Element b)
        {
            int temperature = Mathf.Clamp(
                a.Temperature + b.Temperature,
                -6, 6);

            int humidity = Mathf.Clamp(
                a.Humidity + b.Humidity,
                0, 6);

            Conductivity conductivity =
                (Conductivity)Mathf.Max(
                    (int)a.Conductivity,
                    (int)b.Conductivity
                );

            ElementTag tags = a.Tags | b.Tags;

            return new Element(
                ElementType.None,
                tags,
                temperature,
                humidity,
                conductivity
            );
        }
    }
}