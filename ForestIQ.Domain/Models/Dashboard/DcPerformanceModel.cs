using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ForestIQ.Domain.Models.Dashboard
{
    public class DcPerformanceResponseModel
    {
        [JsonPropertyName("ServerName")]
        public string? ServerName { get; set; }

        [JsonPropertyName("LiveStats")]
        public DcPerformanceLiveModel? LiveStats { get; set; }

        [JsonPropertyName("History")]
        public DcPerformanceHistoryModel? History { get; set; }
    }

    public class DcPerformanceLiveModel
    {
        [JsonPropertyName("CpuLoad")]
        public double? CpuLoad { get; set; }

        [JsonPropertyName("MemoryUsage")]
        public double? MemoryUsage { get; set; }

        [JsonPropertyName("DiskCFree")]
        public double? DiskCFree { get; set; }

        [JsonPropertyName("UptimeDays")]
        public double? UptimeDays { get; set; }

        [JsonPropertyName("LastBoot")]
        public DateTime? LastBoot { get; set; }
        
        [JsonPropertyName("OsVersion")]
        public string? OsVersion { get; set; }

        [JsonPropertyName("Environment")]
        public string? Environment { get; set; }

        [JsonPropertyName("Site")]
        public string? Site { get; set; }

        [JsonPropertyName("TopProcesses")]
        public List<ProcessData>? TopProcesses { get; set; }
    }

    public class ProcessData
    {
        [JsonPropertyName("ProcessName")]
        public string? ProcessName { get; set; }

        [JsonPropertyName("CpuPercent")]
        public double? CpuPercent { get; set; }

        [JsonPropertyName("MemoryMB")]
        public double? MemoryMB { get; set; }

        [JsonPropertyName("Handles")]
        public int? Handles { get; set; }

        [JsonPropertyName("Threads")]
        public int? Threads { get; set; }
    }

    public class DcPerformanceHistoryModel
    {
        [JsonPropertyName("Timestamps")]
        public List<string> Timestamps { get; set; } = new List<string>();

        [JsonPropertyName("CpuLoad")]
        public List<double> CpuLoad { get; set; } = new List<double>();

        [JsonPropertyName("MemoryUsage")]
        public List<double> MemoryUsage { get; set; } = new List<double>();

        [JsonPropertyName("NetworkIo")]
        public List<double> NetworkIo { get; set; } = new List<double>();
    }
}
