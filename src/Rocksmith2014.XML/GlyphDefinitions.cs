﻿using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace Rocksmith2014.XML
{
    [XmlRoot(Namespace = "")]
    public sealed class GlyphDefinitions
    {
        [XmlAttribute]
        public string? Font { get; set; }

        [XmlAttribute]
        public int TextureWidth { get; set; }

        [XmlAttribute]
        public int TextureHeight { get; set; }

        [XmlElement("GlyphDefinition")]
        public List<GlyphDefinition>? Glyphs { get; set; }

        public static GlyphDefinitions Load(string fileName)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(GlyphDefinitions), "");
            using StreamReader file = new StreamReader(fileName);
            return (GlyphDefinitions)serializer.Deserialize(file);
        }
    }

    [XmlRoot(Namespace = "")]
    public sealed class GlyphDefinition
    {
        [XmlAttribute]
        public string? Symbol { get; set; }

        [XmlAttribute]
        public float InnerYMin { get; set; }

        [XmlAttribute]
        public float InnerYMax { get; set; }

        [XmlAttribute]
        public float InnerXMin { get; set; }

        [XmlAttribute]
        public float InnerXMax { get; set; }

        [XmlAttribute]
        public float OuterYMin { get; set; }

        [XmlAttribute]
        public float OuterYMax { get; set; }

        [XmlAttribute]
        public float OuterXMin { get; set; }

        [XmlAttribute]
        public float OuterXMax { get; set; }
    }
}
