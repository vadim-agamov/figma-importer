using System.Collections.Generic;
using Newtonsoft.Json;

namespace FigmaImporter.Editor
{
    public class FigmaDocument
    {
        [JsonProperty("id")]
        private string _id;
        
        [JsonProperty("name")]
        private string _name;
        
        [JsonProperty("children")]
        private FigmaNode[] _children;
        
        [JsonIgnore]
        public string Id => _id;
        
        [JsonIgnore]
        public string Name => _name;
        
        [JsonIgnore]
        public IEnumerable<FigmaNode> Children => _children;
    }
    
    public class FigmaNode
    {
        [JsonProperty("id")]
        private string _id;
        
        [JsonProperty("name")]
        private string _name;
        
        [JsonProperty("visible")]
        private bool _visible = true;
        
        [JsonProperty("type")]
        private string _type;

        [JsonProperty("children")]
        private FigmaNode[] _children;
        
        [JsonIgnore]
        public string Id => _id;
        [JsonIgnore]
        public string Name => _name;
        [JsonIgnore]
        public string Type => _type;
        [JsonIgnore]
        public IEnumerable<FigmaNode> Children => _children;
        [JsonIgnore]
        public string Path;
    }
    
    public class FigmaImages
    {
        [JsonProperty("images")]
        private Dictionary<string, string> _images;
        
        [JsonIgnore]
        public Dictionary<string, string> Images => _images;
    }
}
