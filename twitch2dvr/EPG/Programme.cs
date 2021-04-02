using System.Xml.Serialization;

namespace twitch2dvr.EPG
{
    /// <summary>
    /// Represents a programme entry in an XMLTV EPG
    /// </summary>
    public class Programme
    {
        [XmlAttribute(AttributeName = "start")]
        public string Start { get; set; }

        [XmlAttribute(AttributeName = "stop")]
        public string Stop { get; set; }

        [XmlAttribute(AttributeName = "channel")]
        public string Channel { get; set; }

        [XmlElement(ElementName = "title")]
        public string Title { get; set; }

        [XmlElement(ElementName = "desc")]
        public string Description { get; set; }

        [XmlElement(ElementName = "icon")]
        public Icon Icon { get; set; }
    }
}
