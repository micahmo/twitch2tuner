using System.Collections.Generic;
using System.Xml.Serialization;

namespace twitch2dvr.EPG
{
    /// <summary>
    /// Represents the top-level node of an XMLTV EPG
    /// </summary>
    [XmlRoot("tv")]
    public class Tv
    {
        [XmlElement("channel")]
        public List<Channel> Channels { get; } = new List<Channel>();

        [XmlElement("programme")]
        public List<Programme> Programmes { get; } = new List<Programme>();
    }
}
