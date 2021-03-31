using System.Xml.Serialization;

namespace twitch2dvr.EPG
{
    /// <summary>
    /// Represents a channel entry in an XMLTV EPG
    /// </summary>
    public class Channel
    {
        [XmlElement(ElementName = "display-name")]
        public string DisplayName { get; set; }

        [XmlAttribute(AttributeName = "id")]
        public string Id { get; set; }

        [XmlElement(ElementName = "lcn")]
        public string Lcn { get; set; }

        [XmlElement(ElementName = "icon")]
        public Icon Icon { get; set; }
    }
}
