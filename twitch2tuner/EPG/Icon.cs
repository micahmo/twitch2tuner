﻿using System.Xml.Serialization;

namespace twitch2tuner.EPG
{
    /// <summary>
    /// Represents an icon for a XMLTV EPG channel or programme
    /// </summary>
    public class Icon
    {
        [XmlAttribute(AttributeName = "src")]
        public string Source { get; set; }
    }
}
