using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ForestIQ.Domain.DTO
{
    public class GraphResponse
    {
        public List<GraphNode> Nodes { get; set; } = [];
        public List<GraphEdge> Edges { get; set; } = [];

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTimeOffset? GeneratedAt { get; set; }
    }

    public class GraphNode
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Health { get; set; } = "HEALTHY";

        public string? ParentId { get; set; }

        public int Depth { get; set; }

        public List<string> ChildIds { get; set; } = [];

        public object Coordinates { get; set; } = new { x = 0, y = 0 };

        public object RenderHints { get; set; } = new { expanded = true, pinned = false, highlighted = false, onSearchPath = false, selected = false, focused = false };

        public string? ClusterId { get; set; }

        public Dictionary<string, object> Meta { get; set; } = [];
    }

    public class GraphEdge
    {
        public string Id { get; set; } = string.Empty;
        public string SourceId { get; set; } = string.Empty;
        public string TargetId { get; set; } = string.Empty;
        public string RelationshipType { get; set; } = string.Empty;
        public string Style { get; set; } = "solid";
        public bool Selected { get; set; } = false;
        public bool OnSearchPath { get; set; } = false;
        public bool Visible { get; set; } = true;
        public object? ReplicationData { get; set; }
    }
}
