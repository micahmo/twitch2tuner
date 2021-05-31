namespace twitch2tuner
{
    /// <summary>
    /// Defines a lineup item according to the HDHomeRun scheme
    /// </summary>
    /// <remarks>
    /// HT: https://github.com/marklieberman/iptvtuner/blob/master/Model/HDHomeRunLineupItem.cs
    /// </remarks>
    public class HDHomeRunLineupItem
    {
        public string GuideName { get; set; }
        public string GuideNumber { get; set; }
        public bool HD { get; set; }
        public string URL { get; set; }
    }
}
